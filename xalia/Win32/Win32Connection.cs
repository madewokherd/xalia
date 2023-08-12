using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

            var eventprocdelegate = new WINEVENTPROC(OnMsaaEvent);

            event_proc_delegates.Add(eventprocdelegate);

            SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_UNCLOAKED, IntPtr.Zero,
                eventprocdelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            Utils.RunIdle(UpdateToplevels);
            Utils.RunIdle(UpdateGuiThreadInfo);
        }

        private HashSet<IntPtr> toplevel_hwnds = new HashSet<IntPtr>();

        private Dictionary<string, UiDomElement> elements_by_id = new Dictionary<string, UiDomElement>();

        public UiDomRoot Root { get; }

        public CommandThread CommandThread { get; } = new CommandThread();

        private GUITHREADINFO guithreadinfo;

        static List<WINEVENTPROC> event_proc_delegates = new List<WINEVENTPROC>(); // to make sure delegates aren't GC'd while in use

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
                    return $"hwnd-{hwnd}";
                case OBJID_CLIENT:
                    if (idChild == CHILDID_SELF)
                        goto case OBJID_WINDOW;
                    return $"hwnd-{hwnd}-{idChild}";
                case OBJID_VSCROLL:
                    return $"NonclientVScroll-{hwnd}";
                case OBJID_HSCROLL:
                    return $"NonclientHScroll-{hwnd}";
            }
            return null;
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
            return LookupElement(element_name);
        }

        internal UiDomElement CreateElement(IntPtr hwnd, int idObject = OBJID_CLIENT, int idChild = CHILDID_SELF)
        {
            var element_name = GetElementName(hwnd, idObject, idChild);
            if (element_name is null)
                throw new InvalidOperationException($"cannot create element for {idObject}/{idChild}");

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

                        element.AddProvider(new NonclientScrollProvider(hwnd_ancestor, element, idObject == OBJID_VSCROLL));
                        break;
                    }
            }

            elements_by_id.Add(element_name, element);

            return element;
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
                    Utils.DebugWriteLine($"  win32_gui_hwndactive: {guithreadinfo.hwndActive}");
                if (guithreadinfo.hwndFocus != IntPtr.Zero)
                    Utils.DebugWriteLine($"  win32_gui_hwndfocus: {guithreadinfo.hwndFocus}");
                if (guithreadinfo.hwndCapture != IntPtr.Zero)
                    Utils.DebugWriteLine($"  win32_gui_hwndcapture: {guithreadinfo.hwndCapture}");
                if (guithreadinfo.hwndMenuOwner != IntPtr.Zero)
                    Utils.DebugWriteLine($"  win32_gui_hwndmenuowner: {guithreadinfo.hwndMenuOwner}");
                if (guithreadinfo.hwndMoveSize != IntPtr.Zero)
                    Utils.DebugWriteLine($"  win32_gui_hwndmovesize: {guithreadinfo.hwndMoveSize}");
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
                Root.PropertyChanged("win32_gui_hwndactive", new_threadinfo.hwndActive);
                LookupElement(old_threadinfo.hwndActive)?.PropertyChanged("win32_gui_active", "false");
                LookupElement(new_threadinfo.hwndActive)?.PropertyChanged("win32_gui_active", "true");
            }

            if (new_threadinfo.hwndFocus != old_threadinfo.hwndFocus)
            {
                Root.PropertyChanged("win32_gui_hwndfocus", new_threadinfo.hwndFocus);
                LookupElement(old_threadinfo.hwndFocus)?.PropertyChanged("win32_gui_focus", "false");
                LookupElement(new_threadinfo.hwndFocus)?.PropertyChanged("win32_gui_focus", "true");
            }

            if (new_threadinfo.hwndCapture != old_threadinfo.hwndCapture)
            {
                Root.PropertyChanged("win32_gui_hwndcapture", new_threadinfo.hwndCapture);
                LookupElement(old_threadinfo.hwndCapture)?.PropertyChanged("win32_gui_capture", "false");
                LookupElement(new_threadinfo.hwndCapture)?.PropertyChanged("win32_gui_capture", "true");
            }

            if (new_threadinfo.hwndMenuOwner != old_threadinfo.hwndMenuOwner)
            {
                Root.PropertyChanged("win32_gui_hwndmenuowner", new_threadinfo.hwndMenuOwner);
                LookupElement(old_threadinfo.hwndMenuOwner)?.PropertyChanged("win32_gui_menuowner", "false");
                LookupElement(new_threadinfo.hwndMenuOwner)?.PropertyChanged("win32_gui_menuowner", "true");
            }

            if (new_threadinfo.hwndMoveSize != old_threadinfo.hwndMoveSize)
            {
                Root.PropertyChanged("win32_gui_hwndmovesize", new_threadinfo.hwndMoveSize);
                LookupElement(old_threadinfo.hwndMoveSize)?.PropertyChanged("win32_gui_movesize", "false");
                LookupElement(new_threadinfo.hwndMoveSize)?.PropertyChanged("win32_gui_movesize", "true");
            }

            // We don't have a way to get caret change notifications so we'll ignore that for now
        }

        static void RecursiveLocationChange(UiDomElement element)
        {
            element.ProviderByType<HwndProvider>()?.MsaaAncestorLocationChange();
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
                            hwnd_element.ProviderByType<HwndItemListProvider>()?.MsaaChildAdded(idChild);
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
                                hwnd_element.ProviderByType<HwndItemListProvider>()?.MsaaChildDestroyed(idChild);
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
                case EVENT_OBJECT_LOCATIONCHANGE:
                    {
                        var element = LookupElement(hwnd, idObject, idChild);
                        if (!(element is null))
                        {
                            element.ProviderByType<HwndTabProvider>()?.MsaaLocationChange();
                            RecursiveLocationChange(element);
                        }
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
                                }
                                break;
                        }
                        break;
                    }
            }
        }
    }
}
