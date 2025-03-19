using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class Win32Connection : UiDomProviderBase
    {
        public Win32Connection(UiDomRoot root)
        {
            Root = root;
            root.AddGlobalProvider(this);

            MainContext = SynchronizationContext.Current;

            EventCallback = new UiaEventCallback(OnUiaEvent);

            event_callbacks.Add(EventCallback);

            var eventprocdelegate = new WINEVENTPROC(OnMsaaEvent);

            event_proc_delegates.Add(eventprocdelegate);

            SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_UNCLOAKED, IntPtr.Zero,
                eventprocdelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            Utils.RunIdle(UpdateToplevels);
            Utils.RunIdle(UpdateGuiThreadInfo);
        }

        private HashSet<IntPtr> toplevel_hwnds = new HashSet<IntPtr>();

        private Dictionary<string, UiDomElement> elements_by_id = new Dictionary<string, UiDomElement>();

        private int id_counter;

        public UiDomRoot Root { get; }

        private Dictionary<int, (int, CommandThread)> background_threads = new Dictionary<int, (int, CommandThread)>();

        private CommandThread uia_event_thread;
        private CommandThread UiaEventThread
        {
            get
            {
                if (uia_event_thread is null)
                    uia_event_thread = new CommandThread(ApartmentState.MTA);
                return uia_event_thread;
            }
        }

        public SynchronizationContext MainContext { get; }

        private struct UiaEventInfo
        {
            public int eventid;
            public IntPtr handle;
            public int refcount;

            public UiaEventInfo(int eventid)
            {
                this.eventid = eventid;
                handle = IntPtr.Zero;
                refcount = 0;
            }
        }

        private UiaEventInfo[] uia_events = new UiaEventInfo[] {
            new UiaEventInfo(UIA_StructureChangedEventId),
            new UiaEventInfo(UIA_AutomationPropertyChangedEventId),
        };

        public enum UiaEvent
        {
            StructureChanged,
            PropertyChanged,
            Count
        }

        private GUITHREADINFO guithreadinfo;

        static List<WINEVENTPROC> event_proc_delegates = new List<WINEVENTPROC>(); // to make sure delegates aren't GC'd while in use

        private UiaEventCallback EventCallback;

        static List<UiaEventCallback> event_callbacks = new List<UiaEventCallback>();

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "active", "win32_gui_active" },
            { "focused", "win32_gui_focus" },
            { "capture", "win32_gui_capture" },
            { "menuowner", "win32_gui_menuowner" },
            { "inmovesize", "win32_gui_movesize" },
        };

        public string GetElementName(IntPtr hwnd, int idObject=OBJID_CLIENT, int idChild=CHILDID_SELF)
        {
            switch (idObject)
            {
                case OBJID_WINDOW:
                    return $"hwnd-{hwnd.ToInt64().ToString("x", CultureInfo.InvariantCulture)}";
                case OBJID_CLIENT:
                    if (idChild == CHILDID_SELF)
                        goto case OBJID_WINDOW;
                    return $"hwnd-{hwnd.ToInt64().ToString("x", CultureInfo.InvariantCulture)}-{idChild.ToString(CultureInfo.InvariantCulture)}";
                case OBJID_VSCROLL:
                    return $"NonclientVScroll-{hwnd.ToInt64().ToString("x", CultureInfo.InvariantCulture)}";
                case OBJID_HSCROLL:
                    return $"NonclientHScroll-{hwnd.ToInt64().ToString("x", CultureInfo.InvariantCulture)}";
            }
            return null;
        }

        public string GetElementName(ElementIdentifier id)
        {
            if (id.is_root_hwnd)
                return GetElementName(id.root_hwnd);
            if (!(id.runtime_id is null))
                return GetElementName(id.runtime_id, id.root_hwnd);
            if (!(id.acc2 is null))
                return GetElementName(id.root_hwnd, OBJID_CLIENT, id.acc2_uniqueId);
            if (!(id.punk == IntPtr.Zero))
                return $"punk-{id.root_hwnd.ToInt64().ToString("x", CultureInfo.InvariantCulture)}-{id.punk.ToInt64().ToString("x", CultureInfo.InvariantCulture)}";
            return null;
        }

        public string GetElementName(int[] runtime_id, IntPtr hwnd = default)
        {
            if (runtime_id.Length == 2 && runtime_id[0] == 42)
            {
                // HWND element
                return GetElementName((IntPtr)runtime_id[1]);
            }

            if (runtime_id.Length == 0)
                return null;

            if (runtime_id[0] == UiaAppendRuntimeId)
            {
                var new_runtime_id = new int[runtime_id.Length + 2];
                new_runtime_id[0] = 42;
                new_runtime_id[1] = Utils.TruncatePtr(hwnd);
                new_runtime_id[2] = 4;
                Array.Copy(runtime_id, 1, new_runtime_id, 3, runtime_id.Length - 1);
                runtime_id = new_runtime_id;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("uia");
            foreach (int i in runtime_id)
            {
                sb.Append('-');
                sb.Append(i.ToString("x", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private void UpdateToplevels()
        {
            HashSet<IntPtr> toplevels_to_remove = new HashSet<IntPtr>(toplevel_hwnds);

            foreach (var hwnd in EnumWindows())
            {
                if (toplevels_to_remove.Contains(hwnd))
                {
                    // already known
                    toplevels_to_remove.Remove(hwnd);
                    continue;
                }

                var element = CreateElement(hwnd);
                Root.AddChild(Root.Children.Count, element);
                toplevel_hwnds.Add(hwnd);
            }

            foreach (var hwnd in toplevels_to_remove)
            {
                toplevel_hwnds.Remove(hwnd);
                var element = elements_by_id[GetElementName(hwnd)];
                Root.RemoveChild(Root.Children.IndexOf(element));
            }
        }

        public UiDomElement LookupElement(string element_name)
        {
            if (elements_by_id.TryGetValue(element_name, out var element))
                return element;
            return null;
        }

        public UiDomElement LookupElement(IntPtr hwnd, int idObject=OBJID_CLIENT, int idChild=CHILDID_SELF)
        {
            var element_name = GetElementName(hwnd, idObject, idChild);
            if (element_name is null)
                return null;
            var result = LookupElement(element_name);
            if (result is null && idChild != CHILDID_SELF)
            {
                // Could be an MSAA item with mutable child id
                var parent_element = LookupElement(hwnd, idObject);
                if (!(parent_element is null))
                {
                    var container = parent_element.ProviderByType<IWin32Container>();
                    if (!(container is null))
                        return container.GetMsaaChild(idChild);
                }
            }
            return result;
        }

        public UiDomElement LookupElement(ElementIdentifier id)
        {
            var element_name = GetElementName(id);
            if (element_name is null)
                return null;

            return LookupElement(element_name);
        }

        public UiDomElement LookupElement(int[] runtime_id, IntPtr hwnd = default)
        {
            var element_name = GetElementName(runtime_id, hwnd);
            if (element_name is null)
                return null;

            return LookupElement(element_name);
        }

        internal UiDomElement CreateElement(IntPtr hwnd, int idObject = OBJID_CLIENT, int idChild = CHILDID_SELF)
        {
            var element_name = GetElementName(hwnd, idObject, idChild);
            if (element_name is null)
                throw new InvalidOperationException($"cannot create element for {idObject.ToString(CultureInfo.InvariantCulture)}/{idChild.ToString(CultureInfo.InvariantCulture)}");

            var element = new UiDomElement(element_name, Root);
            switch (idObject)
            {
                case OBJID_WINDOW:
                    element.AddProvider(new HwndProvider(hwnd, element, this));
                    break;
                case OBJID_CLIENT:
                    {
                        if (idChild == CHILDID_SELF)
                            goto case OBJID_WINDOW;

                        var hwnd_ancestor = LookupElement(hwnd)?.ProviderByType<HwndProvider>();
                        if (hwnd_ancestor is null)
                            throw new InvalidOperationException("hwnd element must be created before child element");

                        element.AddProvider(new HwndMsaaChildProvider(element, hwnd_ancestor, idChild));
                        break;
                    }
                case OBJID_VSCROLL:
                case OBJID_HSCROLL:
                    {
                        var hwnd_ancestor = LookupElement(hwnd)?.ProviderByType<HwndProvider>();
                        if (hwnd_ancestor is null)
                            throw new InvalidOperationException("hwnd element must be created before child element");

                        var scroll_provider = new NonclientScrollProvider(hwnd_ancestor, element, idObject == OBJID_VSCROLL);

                        element.AddProvider(scroll_provider);

                        // Apply any customizations
                        var scrollable_provider = hwnd_ancestor.Element.ProviderByType<IWin32Scrollable>();
                        if (!(scrollable_provider is null))
                        {
                            var custom_provider = scrollable_provider.GetScrollBarProvider(scroll_provider);
                            if (!(custom_provider is null))
                                element.AddProvider(custom_provider, 0);
                        }

                        break;
                    }
            }

            elements_by_id.Add(element_name, element);

            return element;
        }

        internal UiDomElement CreateElement(ElementIdentifier id)
        {
            if (id.is_root_hwnd)
                return CreateElement(id.root_hwnd);

            var name = GetElementName(id);

            if (name is null)
            {
                id_counter++;
                name = $"msaa-{id.root_hwnd.ToInt64().ToString("x", CultureInfo.InvariantCulture)}-{id_counter.ToString(CultureInfo.InvariantCulture)}";
            }
            
            var result = new UiDomElement(name, Root);

            var root_hwnd = LookupElement(id.root_hwnd)?.ProviderByType<HwndProvider>();
            if (root_hwnd is null)
                throw new InvalidOperationException("hwnd element must be created before child element");

            if (!(id.prov is null) && !(id.runtime_id is null))
            {
                result.AddProvider(new UiaProvider(root_hwnd, result, id.prov));
            }

            if (!(id.acc2 is null))
            {
                result.AddProvider(new AccessibleProvider(root_hwnd, result, id.acc, id.acc2_uniqueId));
            }
            else if (!(id.acc is null))
            {
                result.AddProvider(new AccessibleProvider(root_hwnd, result, id.acc, id.child_id));
            }

            if (!(id.prov is null) && (id.runtime_id is null))
            {
                result.AddProvider(new UiaProvider(root_hwnd, result, id.prov));
            }

            elements_by_id.Add(name, result);

            return result;
        }

        public override void NotifyElementRemoved(UiDomElement element)
        {
            elements_by_id.Remove(element.DebugId);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "win32_gui_inmovesize":
                    depends_on.Add((Root, new IdentifierExpression(identifier)));
                    return UiDomBoolean.FromBool((guithreadinfo.flags & GUI_INMOVESIZE) != 0);
                case "win32_gui_inmenumode":
                    depends_on.Add((Root, new IdentifierExpression(identifier)));
                    return UiDomBoolean.FromBool((guithreadinfo.flags & GUI_INMENUMODE) != 0);
                case "win32_gui_popupmenumode":
                    depends_on.Add((Root, new IdentifierExpression(identifier)));
                    return UiDomBoolean.FromBool((guithreadinfo.flags & GUI_POPUPMENUMODE) != 0);
                case "win32_gui_systemmenumode":
                    depends_on.Add((Root, new IdentifierExpression(identifier)));
                    return UiDomBoolean.FromBool((guithreadinfo.flags & GUI_SYSTEMMENUMODE) != 0);
                case "win32_gui_hwndactive":
                    depends_on.Add((Root, new IdentifierExpression(identifier)));
                    return new UiDomInt((int)guithreadinfo.hwndActive);
                case "win32_gui_hwndfocus":
                    depends_on.Add((Root, new IdentifierExpression(identifier)));
                    return new UiDomInt((int)guithreadinfo.hwndFocus);
                case "win32_gui_hwndcapture":
                    depends_on.Add((Root, new IdentifierExpression(identifier)));
                    return new UiDomInt((int)guithreadinfo.hwndCapture);
                case "win32_gui_hwndmenuowner":
                    depends_on.Add((Root, new IdentifierExpression(identifier)));
                    return new UiDomInt((int)guithreadinfo.hwndMenuOwner);
                case "win32_gui_hwndmovesize":
                    depends_on.Add((Root, new IdentifierExpression(identifier)));
                    return new UiDomInt((int)guithreadinfo.hwndMoveSize);
                case "win32_gui_active":
                    {
                        var hwnd = element.ProviderByType<HwndProvider>();
                        if (!(hwnd is null))
                        {
                            depends_on.Add((element, new IdentifierExpression(identifier)));
                            return UiDomBoolean.FromBool(hwnd.Hwnd == guithreadinfo.hwndActive);
                        }
                        break;
                    }
                case "win32_gui_focus":
                    {
                        var hwnd = element.ProviderByType<HwndProvider>();
                        if (!(hwnd is null))
                        {
                            depends_on.Add((element, new IdentifierExpression(identifier)));
                            return UiDomBoolean.FromBool(hwnd.Hwnd == guithreadinfo.hwndFocus);
                        }
                        break;
                    }
                case "win32_gui_capture":
                    {
                        var hwnd = element.ProviderByType<HwndProvider>();
                        if (!(hwnd is null))
                        {
                            depends_on.Add((element, new IdentifierExpression(identifier)));
                            return UiDomBoolean.FromBool(hwnd.Hwnd == guithreadinfo.hwndCapture);
                        }
                        break;
                    }
                case "win32_gui_menuowner":
                    {
                        var hwnd = element.ProviderByType<HwndProvider>();
                        if (!(hwnd is null))
                        {
                            depends_on.Add((element, new IdentifierExpression(identifier)));
                            return UiDomBoolean.FromBool(hwnd.Hwnd == guithreadinfo.hwndMenuOwner);
                        }
                        break;
                    }
                case "win32_gui_movesize":
                    {
                        var hwnd = element.ProviderByType<HwndProvider>();
                        if (!(hwnd is null))
                        {
                            depends_on.Add((element, new IdentifierExpression(identifier)));
                            return UiDomBoolean.FromBool(hwnd.Hwnd == guithreadinfo.hwndMoveSize);
                        }
                        break;
                    }
            }
            return UiDomUndefined.Instance;
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return UiDomUndefined.Instance;
        }

        public override void DumpProperties(UiDomElement element)
        {
            if (element is UiDomRoot)
            {
                if ((guithreadinfo.flags & GUI_INMOVESIZE) != 0)
                    Utils.DebugWriteLine($"  win32_gui_inmovesize: true");
                if ((guithreadinfo.flags & GUI_INMENUMODE) != 0)
                    Utils.DebugWriteLine($"  win32_gui_inmenumode: true");
                if ((guithreadinfo.flags & GUI_POPUPMENUMODE) != 0)
                    Utils.DebugWriteLine($"  win32_gui_popupmenumode: true");
                if ((guithreadinfo.flags & GUI_SYSTEMMENUMODE) != 0)
                    Utils.DebugWriteLine($"  win32_gui_systemmenumode: true");
                if (guithreadinfo.hwndActive != IntPtr.Zero)
                    Utils.DebugWriteLine($"  win32_gui_hwndactive: 0x{guithreadinfo.hwndActive:x}");
                if (guithreadinfo.hwndFocus != IntPtr.Zero)
                    Utils.DebugWriteLine($"  win32_gui_hwndfocus: 0x{guithreadinfo.hwndFocus:x}");
                if (guithreadinfo.hwndCapture != IntPtr.Zero)
                    Utils.DebugWriteLine($"  win32_gui_hwndcapture: 0x{guithreadinfo.hwndCapture:x}");
                if (guithreadinfo.hwndMenuOwner != IntPtr.Zero)
                    Utils.DebugWriteLine($"  win32_gui_hwndmenuowner: 0x{guithreadinfo.hwndMenuOwner:x}");
                if (guithreadinfo.hwndMoveSize != IntPtr.Zero)
                    Utils.DebugWriteLine($"  win32_gui_hwndmovesize: 0x{guithreadinfo.hwndMoveSize:x}");
            }
            var hwnd = element.ProviderByType<HwndProvider>();
            if (!(hwnd is null))
            {
                if (hwnd.Hwnd == guithreadinfo.hwndActive)
                    Utils.DebugWriteLine($"  win32_gui_active: true");
                if (hwnd.Hwnd == guithreadinfo.hwndFocus)
                    Utils.DebugWriteLine($"  win32_gui_focus: true");
                if (hwnd.Hwnd == guithreadinfo.hwndCapture)
                    Utils.DebugWriteLine($"  win32_gui_capture: true");
                if (hwnd.Hwnd == guithreadinfo.hwndMenuOwner)
                    Utils.DebugWriteLine($"  win32_gui_menuowner: true");
                if (hwnd.Hwnd == guithreadinfo.hwndMoveSize)
                    Utils.DebugWriteLine($"  win32_gui_movesize: true");
            }
        }

        private void UpdateGuiThreadInfo()
        {
            var new_threadinfo = new GUITHREADINFO();
            new_threadinfo.cbSize = Marshal.SizeOf<GUITHREADINFO>();
            GetGUIThreadInfo(0, ref new_threadinfo);

            var old_threadinfo = guithreadinfo;
            guithreadinfo = new_threadinfo;

            var changed_flags = new_threadinfo.flags ^ old_threadinfo.flags;

            if ((changed_flags & GUI_INMOVESIZE) != 0)
                Root.PropertyChanged("win32_gui_inmovesize", (new_threadinfo.flags & GUI_INMOVESIZE) != 0);
            if ((changed_flags & GUI_INMENUMODE) != 0)
                Root.PropertyChanged("win32_gui_inmenumode", (new_threadinfo.flags & GUI_INMENUMODE) != 0);
            if ((changed_flags & GUI_POPUPMENUMODE) != 0)
                Root.PropertyChanged("win32_gui_popupmenumode", (new_threadinfo.flags & GUI_POPUPMENUMODE) != 0);
            if ((changed_flags & GUI_SYSTEMMENUMODE) != 0)
                Root.PropertyChanged("win32_gui_systemmenumode", (new_threadinfo.flags & GUI_SYSTEMMENUMODE) != 0);

            if (new_threadinfo.hwndActive != old_threadinfo.hwndActive)
            {
                Root.PropertyChanged("win32_gui_hwndactive", $"0x{new_threadinfo.hwndActive:x}");
                LookupElement(old_threadinfo.hwndActive)?.PropertyChanged("win32_gui_active", "false");
                LookupElement(new_threadinfo.hwndActive)?.PropertyChanged("win32_gui_active", "true");
            }

            if (new_threadinfo.hwndFocus != old_threadinfo.hwndFocus)
            {
                Root.PropertyChanged("win32_gui_hwndfocus", $"0x{new_threadinfo.hwndFocus:x}");
                LookupElement(old_threadinfo.hwndFocus)?.PropertyChanged("win32_gui_focus", "false");
                LookupElement(new_threadinfo.hwndFocus)?.PropertyChanged("win32_gui_focus", "true");
            }

            if (new_threadinfo.hwndCapture != old_threadinfo.hwndCapture)
            {
                Root.PropertyChanged("win32_gui_hwndcapture", $"0x{new_threadinfo.hwndCapture:x}");
                LookupElement(old_threadinfo.hwndCapture)?.PropertyChanged("win32_gui_capture", "false");
                LookupElement(new_threadinfo.hwndCapture)?.PropertyChanged("win32_gui_capture", "true");
            }

            if (new_threadinfo.hwndMenuOwner != old_threadinfo.hwndMenuOwner)
            {
                Root.PropertyChanged("win32_gui_hwndmenuowner", $"0x{new_threadinfo.hwndMenuOwner:x}");
                LookupElement(old_threadinfo.hwndMenuOwner)?.PropertyChanged("win32_gui_menuowner", "false");
                LookupElement(new_threadinfo.hwndMenuOwner)?.PropertyChanged("win32_gui_menuowner", "true");
            }

            if (new_threadinfo.hwndMoveSize != old_threadinfo.hwndMoveSize)
            {
                Root.PropertyChanged("win32_gui_hwndmovesize", $"0x{new_threadinfo.hwndMoveSize:x}");
                LookupElement(old_threadinfo.hwndMoveSize)?.PropertyChanged("win32_gui_movesize", "false");
                LookupElement(new_threadinfo.hwndMoveSize)?.PropertyChanged("win32_gui_movesize", "true");
            }

            // We don't have a way to get caret change notifications so we'll ignore that for now
        }

        internal static void RecursiveLocationChange(UiDomElement element, bool include_element = true)
        {
            if (include_element)
            {
                element.ProviderByType<HwndProvider>()?.MsaaAncestorLocationChange();
                element.ProviderByType<AccessibleProvider>()?.MsaaAncestorLocationChange();
            }
            foreach (var child in element.Children)
            {
                RecursiveLocationChange(child);
            }
        }

        private void OnMsaaEvent(IntPtr hWinEventProc, uint eventId, IntPtr hwnd, int idObject, int idChild, int idEventThread, int dwmsEventTime)
        {
            switch (eventId)
            {
                case EVENT_SYSTEM_FOREGROUND:
                case EVENT_SYSTEM_MENUSTART:
                case EVENT_SYSTEM_MENUEND:
                case EVENT_SYSTEM_MENUPOPUPSTART:
                case EVENT_SYSTEM_MENUPOPUPEND:
                case EVENT_SYSTEM_CAPTURESTART:
                case EVENT_SYSTEM_CAPTUREEND:
                case EVENT_SYSTEM_MOVESIZESTART:
                case EVENT_SYSTEM_MOVESIZEEND:
                case EVENT_OBJECT_FOCUS:
                    UpdateGuiThreadInfo();
                    break;
                case EVENT_OBJECT_CREATE:
                    if (idObject == OBJID_WINDOW && idChild == CHILDID_SELF)
                    {
                        var parent_hwnd = GetAncestor(hwnd, GA_PARENT);
                        if (parent_hwnd == GetDesktopWindow())
                        {
                            UpdateToplevels();
                        }
                        else
                        {
                            var parent_element = LookupElement(parent_hwnd);
                            if (!(parent_element is null))
                            {
                                parent_element.ProviderByType<HwndProvider>()?.MsaaChildWindowAdded();
                            }
                        }
                    }
                    if (idObject == OBJID_CLIENT && idChild > 0)
                    {
                        var hwnd_element = LookupElement(hwnd);
                        if (!(hwnd_element is null))
                        {
                            hwnd_element.ProviderByType<IWin32Container>()?.MsaaChildCreated(idChild);
                        }
                    }
                    break;
                case EVENT_OBJECT_DESTROY:
                    {
                        var parent_element = LookupElement(hwnd, idObject, idChild)?.Parent;
                        if (!(parent_element is null))
                        {
                            if (parent_element is UiDomRoot)
                                UpdateToplevels();
                            else
                            {
                                if (idObject == OBJID_WINDOW && idChild == CHILDID_SELF)
                                    parent_element.ProviderByType<HwndProvider>()?.MsaaChildWindowRemoved();
                            }
                        }
                        if (idObject == OBJID_CLIENT && idChild > 0)
                        {
                            var hwnd_element = LookupElement(hwnd);
                            if (!(hwnd_element is null))
                            {
                                hwnd_element.ProviderByType<IWin32Container>()?.MsaaChildDestroyed(idChild);
                            }
                        }
                        break;
                    }
                case EVENT_OBJECT_SHOW:
                case EVENT_OBJECT_HIDE:
                case EVENT_OBJECT_STATECHANGE:
                case EVENT_OBJECT_CLOAKED:
                case EVENT_OBJECT_UNCLOAKED:
                    {
                        switch (idObject)
                        {
                            case OBJID_WINDOW:
                            case OBJID_CLIENT:
                                {
                                    var element = LookupElement(hwnd, idObject, idChild);
                                    if (!(element is null))
                                    {
                                        element.ProviderByType<AccessibleProvider>()?.MsaaStateChange();
                                        element.ProviderByType<HwndProvider>()?.MsaaStateChange();
                                    }
                                    break;
                                }
                            case OBJID_HSCROLL:
                            case OBJID_VSCROLL:
                                {
                                    var element = LookupElement(hwnd, idObject, idChild);
                                    if (!(element is null))
                                    {
                                        element.ProviderByType<NonclientScrollProvider>()?.MsaaStateChange();
                                    }
                                    break;
                                }
                        }
                        break;
                    }
                case EVENT_OBJECT_REORDER:
                    {
                        if (idObject == OBJID_CLIENT)
                        {
                            var element = LookupElement(hwnd, idObject, idChild);
                            if (!(element is null))
                            {
                                element.ProviderByType<AccessibleProvider>()?.MsaaChildrenReordered();
                                element.ProviderByType<IWin32Container>()?.MsaaChildrenReordered();
                            }
                        }
                        else if (idObject == OBJID_WINDOW)
                        {
                            var element = LookupElement(hwnd, idObject, idChild);
                            if (!(element is null))
                            {
                                element.ProviderByType<HwndProvider>()?.MsaaStateChange();
                            }
                        }
                        break;
                    }
                case EVENT_OBJECT_LOCATIONCHANGE:
                    {
                        var element = LookupElement(hwnd, idObject, idChild);
                        if (!(element is null))
                        {
                            element.ProviderByType<IWin32LocationChange>()?.MsaaLocationChange();
                            RecursiveLocationChange(element);
                            element.ProviderByType<HwndProvider>()?.MsaaStateChange();
                        }
                        break;
                    }
                case EVENT_SYSTEM_MINIMIZESTART:
                case EVENT_SYSTEM_MINIMIZEEND:
                    {
                        var element = LookupElement(hwnd, idObject, idChild);
                        if (!(element is null))
                            element.ProviderByType<HwndProvider>()?.MsaaStateChange();
                        break;
                    }
                case EVENT_OBJECT_SELECTION:
                    {
                        var hwnd_element = LookupElement(hwnd, idObject, CHILDID_SELF);
                        if (!(hwnd_element is null))
                        {
                            hwnd_element.ProviderByType<HwndTabProvider>()?.MsaaSelectionChange(idChild);
                        }
                        break;
                    }
                case EVENT_OBJECT_VALUECHANGE:
                    {
                        switch (idObject)
                        {
                            case OBJID_VSCROLL:
                            case OBJID_HSCROLL:
                                {
                                    var element = LookupElement(hwnd, idObject, idChild);
                                    element?.ProviderByType<NonclientScrollProvider>()?.MsaaValueChange();
                                    
                                    // The window WS_HSCROLL or WS_VSCROLL style may have changed.
                                    element = LookupElement(hwnd);
                                    element?.ProviderByType<HwndProvider>()?.MsaaStateChange();

                                    element?.ProviderByType<IWin32ScrollChange>()?.MsaaScrolled(idObject);

                                    // Scrolling a window may also move child elements
                                    var hwnd_element = LookupElement(hwnd);
                                    if (!(hwnd_element is null))
                                        RecursiveLocationChange(hwnd_element, include_element:false);
                                }
                                break;
                        }
                        break;
                    }
                case EVENT_OBJECT_DEFACTIONCHANGE:
                    {
                        var element = LookupElement(hwnd, idObject, idChild);
                        if (!(element is null))
                        {
                            element?.ProviderByType<AccessibleProvider>()?.MsaaDefaultActionChange();
                        }
                        break;
                    }
                case EVENT_OBJECT_NAMECHANGE:
                    {
                        var element = LookupElement(hwnd, idObject, idChild);
                        if (!(element is null))
                        {
                            element?.ProviderByType<AccessibleProvider>()?.MsaaNameChange();
                            element?.ProviderByType<HwndProvider>()?.MsaaNameChange();
                            element?.ProviderByType<IWin32NameChange>()?.MsaaNameChange();
                        }
                        break;
                    }
                case IA2_EVENT_TEXT_CHANGED:
                case IA2_EVENT_TEXT_INSERTED:
                case IA2_EVENT_TEXT_REMOVED:
                case IA2_EVENT_TEXT_UPDATED:
                    {
                        // This can affect name calculation
                        var element = LookupElement(hwnd, idObject, idChild);
                        if (!(element is null))
                        {
                            element?.ProviderByType<AccessibleProvider>()?.MsaaNameChange();
                            element?.ProviderByType<HwndProvider>()?.MsaaNameChange();
                        }
                        break;
                    }

            }
        }

        public CommandThread CreateBackgroundThread(int tid)
        {
            if (background_threads.TryGetValue(tid, out var pair))
            {
                background_threads[tid] = (pair.Item1 + 1, pair.Item2);
                return pair.Item2;
            }

            var result = new CommandThread();
            background_threads.Add(tid, (1, result));
            return result;
        }

        public void UnrefBackgroundThread(int tid)
        {
            var pair = background_threads[tid];

            if (pair.Item1 == 1)
            {
                background_threads.Remove(tid);
                pair.Item2.Dispose();
            }
            else
            {
                background_threads[tid] = (pair.Item1 - 1, pair.Item2);
            }
        }

        private void OnUiaEvent(IntPtr pArgs, object[,] pRequestedData, string pTreeStructure)
        {
            IntPtr node = (IntPtr)(long)pRequestedData[0, 0];
            int hr;

            try
            {
                hr = UiaGetRuntimeId(node, out var runtime_id);
                Marshal.ThrowExceptionForHR(hr);

                UiaEventArgs args = Marshal.PtrToStructure<UiaEventArgs>(pArgs);
                switch (args.Type)
                {
                    case EventArgsType.StructureChanged:
                        {
                            var sc = Marshal.PtrToStructure<UiaStructureChangedEventArgs>(pArgs);

                            if (sc.StructureChangeType == StructureChangeType.ChildAdded)
                            {
                                IntPtr condition;
                                UiaCacheRequest cache_request = new UiaCacheRequest()
                                {
                                    Scope = TreeScope.Element,
                                    automationElementMode = AutomationElementMode.Full
                                };

                                condition = Marshal.AllocCoTaskMem(Marshal.SizeOf<UiaCondition>());

                                try
                                {
                                    UiaCondition view_condition = new UiaCondition
                                    {
                                        ConditionType = ConditionType.True,
                                    };
                                    Marshal.StructureToPtr(view_condition, condition, false);
                                    cache_request.pViewCondition = condition;

                                    hr = UiaNavigate(node, NavigateDirection.Parent, condition,
                                        ref cache_request, out var ppRequestedData, out string ppTreeStructure);
                                    Marshal.ThrowExceptionForHR(hr);

                                    IntPtr node2 = (IntPtr)(long)ppRequestedData[0, 0];

                                    try
                                    {
                                        hr = UiaGetRuntimeId(node2, out runtime_id);
                                        Marshal.ThrowExceptionForHR(hr);
                                    }
                                    finally
                                    {
                                        UiaNodeRelease(node2);
                                    }
                                }
                                finally
                                {
                                    Marshal.FreeCoTaskMem(condition);
                                }
                            }

                            MainContext.Post(OnChildrenChanged, runtime_id);

                            break;
                        }
                    case EventArgsType.PropertyChanged:
                        {
                            var pc = Marshal.PtrToStructure<UiaPropertyChangedEventArgs>(pArgs);

                            MainContext.Post(OnPropertyChanged, (runtime_id, pc.PropertyId, pc.NewValue));

                            break;
                        }
                }
            }
            catch (Exception e)
            {
                if (AccessibleProvider.IsExpectedException(e))
                    return;
                throw;
            }
        }

        private void OnPropertyChanged(object state)
        {
            var st = ((int[], int, object))state;
            var runtime_id = st.Item1;
            var prop_id = st.Item2;
            var new_value = st.Item3;

            var element = LookupElement(runtime_id);
            if (element is null)
                return;

            element.ProviderByType<UiaProvider>()?.PropertyChanged(prop_id, new_value);
        }

        private void OnChildrenChanged(object state)
        {
            int[] runtime_id = (int[])state;

            var element = LookupElement(runtime_id);
            if (element is null)
                return;

            element.ProviderByType<UiaProvider>()?.ChildrenChanged();
        }

        public unsafe Task AddUiaEventWindow(UiaEvent ev, IntPtr hwnd)
        {
            return UiaEventThread.OnBackgroundThread(() => {
                ref UiaEventInfo info = ref uia_events[(int)ev];
                int hr;
                if (info.handle == IntPtr.Zero)
                {
                    UiaCacheRequest cache_request = new UiaCacheRequest()
                    {
                        Scope = TreeScope.Element,
                        automationElementMode = AutomationElementMode.Full
                    };

                    cache_request.pViewCondition = Marshal.AllocCoTaskMem(Marshal.SizeOf<UiaCondition>());
                    try
                    {
                        UiaCondition view_condition = new UiaCondition
                        {
                            ConditionType = ConditionType.True,
                        };
                        Marshal.StructureToPtr(view_condition, cache_request.pViewCondition, false);

                        hr = UiaGetRootNode(out var root_node);
                        Marshal.ThrowExceptionForHR(hr);
                        try
                        {
                            IntPtr handle;

                            if (ev == UiaEvent.PropertyChanged)
                            {
                                int[] properties = new int[]
                                {
                                    UIA_ControlTypePropertyId,
                                    UIA_IsEnabledPropertyId,
                                    UIA_IsOffscreenPropertyId,
                                    UIA_BoundingRectanglePropertyId,
                                };
                                fixed (int* pProperties = properties)
                                {
                                    hr = UiaAddEvent(root_node, info.eventid,
                                        EventCallback, TreeScope.Subtree, (IntPtr)pProperties, properties.Length,
                                        ref cache_request, out handle);
                                    Marshal.ThrowExceptionForHR(hr);
                                }
                            }
                            else
                            {
                                hr = UiaAddEvent(root_node, info.eventid,
                                    EventCallback, TreeScope.Subtree, IntPtr.Zero, 0, ref cache_request,
                                    out handle);
                                Marshal.ThrowExceptionForHR(hr);
                            }
                            info.handle = handle;
                        }
                        finally
                        {
                            UiaNodeRelease(root_node);
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(cache_request.pViewCondition);
                    }
                }

                hr = UiaEventAddWindow(info.handle, hwnd);
                Marshal.ThrowExceptionForHR(hr);
                info.refcount++;
            }, CommandThreadPriority.Query);
        }

        public async Task RemoveUiaEventWindow(UiaEvent ev, IntPtr hwnd)
        {
            await UiaEventThread.OnBackgroundThread(() =>
            {
                ref UiaEventInfo info = ref uia_events[(int)ev];
                int hr;
                if (info.refcount == 1)
                {
                    hr = UiaRemoveEvent(info.handle);
                    Marshal.ThrowExceptionForHR(hr);
                    info.handle = IntPtr.Zero;
                    info.refcount = 0;
                }
                else
                {
                    hr = UiaEventRemoveWindow(info.handle, hwnd);
                    Marshal.ThrowExceptionForHR(hr);
                    info.refcount--;
                }
            }, CommandThreadPriority.Query);
        }
    }
}
