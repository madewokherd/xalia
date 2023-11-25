using Accessibility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndProvider : UiDomProviderBase
    {
        public HwndProvider(IntPtr hwnd, UiDomElement element, Win32Connection connection)
        {
            Hwnd = hwnd;
            Element = element;
            Connection = connection;
            RealClassName = RealGetWindowClass(hwnd);
            ClassName = GetClassName(hwnd);
            Tid = GetWindowThreadProcessId(hwnd, out var pid);
            Pid = pid;

            Style = unchecked((int)(long)GetWindowLong(Hwnd, GWL_STYLE));

            Utils.RunTask(DiscoverProviders());
        }

        private static readonly UiDomEnum window_role = new UiDomEnum(new string[] { "window" });
        private static readonly UiDomEnum pane_role = new UiDomEnum(new string[] { "pane" });

        public IntPtr Hwnd { get; }
        public UiDomElement Element { get; }
        public Win32Connection Connection { get; }
        public string ClassName { get; }
        public string RealClassName { get; }
        public int Pid { get; }
        public int Tid { get; }

        private bool _watchingChildren;

        private bool _useNonclient;

        private static string[] tracked_properties = new string[] { "recurse_method", "win32_use_nonclient" };

        private static Dictionary<string, string> child_property_aliases = new Dictionary<string, string>()
        {
            { "hwnd", "win32_hwnd" },
            { "pid", "win32_pid" },
            { "tid", "win32_tid" },
            { "process_name", "win32_process_name" },
            { "application_name", "win32_process_name" },
        };

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "class_name", "win32_class_name" },
            { "real_class_name", "win32_real_class_name" },
            { "style", "win32_style" },
            { "x", "win32_x" },
            { "y", "win32_y" },
            { "width", "win32_width" },
            { "height", "win32_height" },
            { "client_x", "win32_client_x" },
            { "client_y", "win32_client_y" },
            { "client_width", "win32_client_width" },
            { "client_height", "win32_client_height" },
            { "control_id", "win32_control_id" },
            { "name", "win32_window_text" },
            { "window_text", "win32_window_text" },
        };

        private static string[] win32_stylenames =
        {
            "popup",
            "child",
            "minimize",
            "visible",
            "disabled",
            "clipsiblings",
            "clipchildren",
            "maximize",
            "border",
            "dlgframe",
            "vscroll",
            "hscroll",
            "sysmenu",
            "thickframe",
            // The other styles are contextual
        };

        private static Dictionary<string, int> win32_styles_by_name = new Dictionary<string, int>();

        public int Style { get; private set; }

        private bool _watchingWindowRect;
        internal RECT WindowRect { get; private set; }
        internal RECT ClientRect { get; private set; }
        public bool WindowRectsKnown { get; private set; }
        public int X => WindowRect.left;
        public int Y => WindowRect.top;
        public int Width => WindowRect.right - WindowRect.left;
        public int Height => WindowRect.bottom - WindowRect.top;

        private int _controlId;
        private bool _controlIdKnown;
        public int ControlId
        {
            get
            {
                if (!_controlIdKnown)
                {
                    _controlId = unchecked((int)(long)GetWindowLong(Hwnd, GWLP_ID));
                    _controlIdKnown = true;
                }
                return _controlId;
            }
        }

        private bool _fetchingWindowText;
        public string WindowText { get; private set; }
        public bool WindowTextKnown { get; private set; }

        public string ProcessName { get; private set; }

        static HwndProvider()
        {
            for (int i=0; i<win32_stylenames.Length; i++)
            {
                win32_styles_by_name[win32_stylenames[i]] = (int)(0x80000000 >> i);
            }
            win32_styles_by_name["group"] = WS_GROUP;
            win32_styles_by_name["minimizebox"] = WS_MINIMIZEBOX;
            win32_styles_by_name["tabstop"] = WS_TABSTOP;
            win32_styles_by_name["maximizebox"] = WS_MAXIMIZEBOX;
        }

        private void AddProvider(IUiDomProvider provider, int index)
        {
            Element.AddProvider(provider, index);
            if (provider is IWin32Scrollable scrollable)
            {
                var vs = Connection.LookupElement(Hwnd, OBJID_VSCROLL);
                if (!(vs is null))
                {
                    var custom = scrollable.GetScrollBarProvider(vs.ProviderByType<NonclientScrollProvider>());
                    if (!(custom is null))
                        vs.AddProvider(custom, 0);
                }

                var hs = Connection.LookupElement(Hwnd, OBJID_HSCROLL);
                if (!(hs is null))
                {
                    var custom = scrollable.GetScrollBarProvider(hs.ProviderByType<NonclientScrollProvider>());
                    if (!(custom is null))
                        hs.AddProvider(custom, 0);
                }
            }
        }

        private async Task DiscoverProviders()
        {
            // TODO: Check if there's a UIA provider

            IntPtr lr = default;
            try
            {
                lr = await SendMessageAsync(Hwnd, WM_GETOBJECT, IntPtr.Zero, (IntPtr)OBJID_CLIENT);
            }
            catch (Win32Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
            if ((long)lr > 0)
            {
                try
                {
                    IAccessible acc = await Connection.CommandThread.OnBackgroundThread(() =>
                    {
                        int hr = ObjectFromLresult(lr, IID_IAccessible, IntPtr.Zero, out var obj);
                        Marshal.ThrowExceptionForHR(hr);
                        return obj;
                    }, Tid + 1);
                    AddProvider(new AccessibleProvider(this, Element, acc, 0), 0);
                    return;
                }
                catch (Exception e)
                {
                    if (!AccessibleProvider.IsExpectedException(e))
                        throw;
                }
            }
            
            switch (RealClassName)
            {
                case "#32770":
                    AddProvider(new HwndDialogProvider(this), 0);
                    return;
                case "Button":
                    AddProvider(new HwndButtonProvider(this), 0);
                    return;
                case "ComboBox":
                    AddProvider(new HwndComboBoxProvider(this), 0);
                    return;
                case "msctls_trackbar32":
                    AddProvider(new HwndTrackBarProvider(this), 0);
                    return;
                case "SysListView32":
                    AddProvider(new HwndListViewProvider(this), 0);
                    return;
                case "SysTabControl32":
                    AddProvider(new HwndTabProvider(this), 0);
                    return;
            }

            try
            {
                lr = await SendMessageAsync(Hwnd, WM_GETOBJECT, IntPtr.Zero, (IntPtr)OBJID_QUERYCLASSNAMEIDX);
            }
            catch (Win32Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
            switch((long)lr)
            {
                case 65536 + 2:
                    AddProvider(new HwndButtonProvider(this), 0);
                    return;
                case 65536 + 5:
                    AddProvider(new HwndComboBoxProvider(this), 0);
                    return;
                case 65536 + 15:
                    AddProvider(new HwndTabProvider(this), 0);
                    return;
                case 65536 + 18:
                    AddProvider(new HwndTrackBarProvider(this), 0);
                    return;
                case 65536 + 19:
                    AddProvider(new HwndListViewProvider(this), 0);
                    return;
            }
        }

        public string FormatStyles()
        {
            List<string> styles = new List<string>();
            for (int i=0; i<win32_stylenames.Length; i++)
            {
                int flag = (int)(0x80000000 >> i);
                if ((Style & flag) != 0)
                {
                    styles.Add(win32_stylenames[i]);
                }
            }

            if ((Style & (WS_SYSMENU|WS_MINIMIZEBOX)) == (WS_SYSMENU|WS_MINIMIZEBOX))
            {
                styles.Add("minimizebox");
            }
            if ((Style & (WS_SYSMENU|WS_MINIMIZEBOX)) == WS_GROUP)
            {
                styles.Add("group");
            }
            if ((Style & (WS_SYSMENU|WS_MAXIMIZEBOX)) == (WS_SYSMENU|WS_MAXIMIZEBOX))
            {
                styles.Add("maximizebox");
            }
            if ((Style & (WS_SYSMENU|WS_MAXIMIZEBOX)) == WS_TABSTOP)
            {
                styles.Add("tabstop");
            }

            var style_provider = Element.ProviderByType<IWin32Styles>();
            if (!(style_provider is null))
            {
                style_provider.GetStyleNames(Style, styles);
            }

            return $"0x{unchecked((uint)Style):x} [{string.Join("|", styles)}]";
        }

        public override void DumpProperties(UiDomElement element)
        {
            ChildDumpProperties();
            Utils.DebugWriteLine($"  win32_class_name: \"{ClassName}\"");
            Utils.DebugWriteLine($"  win32_real_class_name: \"{RealClassName}\"");
            Utils.DebugWriteLine($"  win32_style: {FormatStyles()}");
            if (WindowRectsKnown)
            {
                Utils.DebugWriteLine($"  win32_x: {X}");
                Utils.DebugWriteLine($"  win32_y: {Y}");
                Utils.DebugWriteLine($"  win32_width: {Width}");
                Utils.DebugWriteLine($"  win32_height: {Height}");
                Utils.DebugWriteLine($"  win32_client_x: {ClientRect.left}");
                Utils.DebugWriteLine($"  win32_client_y: {ClientRect.top}");
                Utils.DebugWriteLine($"  win32_client_width: {ClientRect.width}");
                Utils.DebugWriteLine($"  win32_client_height: {ClientRect.height}");
            }
            if ((Style & WS_CHILD) != 0 && ControlId != 0)
            {
                Utils.DebugWriteLine($"  win32_control_id: {ControlId}");
            }
            if (WindowTextKnown && WindowText != "")
            {
                Utils.DebugWriteLine($"  win32_window_text: \"{WindowText}\"");
            }
        }

        internal void ChildDumpProperties()
        {
            Utils.DebugWriteLine($"  win32_hwnd: 0x{Hwnd.ToInt64():x}");
            Utils.DebugWriteLine($"  win32_pid: {Pid}");
            Utils.DebugWriteLine($"  win32_tid: {Tid}");
            if (!(ProcessName is null))
                Utils.DebugWriteLine($"  win32_process_name: {ProcessName}");
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_element":
                    return UiDomBoolean.True;
                case "win32_class_name":
                    return new UiDomString(ClassName);
                case "win32_real_class_name":
                    return new UiDomString(RealClassName);
                case "win32_style":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return new UiDomInt(Style);
                case "win32_x":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectsKnown)
                        return new UiDomInt(X);
                    break;
                case "win32_y":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectsKnown)
                        return new UiDomInt(Y);
                    break;
                case "win32_width":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectsKnown)
                        return new UiDomInt(Width);
                    break;
                case "win32_height":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectsKnown)
                        return new UiDomInt(Height);
                    break;
                case "win32_client_x":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectsKnown)
                        return new UiDomInt(ClientRect.left);
                    break;
                case "win32_client_y":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectsKnown)
                        return new UiDomInt(ClientRect.top);
                    break;
                case "win32_client_width":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectsKnown)
                        return new UiDomInt(ClientRect.width);
                    break;
                case "win32_client_height":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectsKnown)
                        return new UiDomInt(ClientRect.height);
                    break;
                case "win32_control_id":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    if ((Style & WS_CHILD) != 0)
                    {
                        return new UiDomInt(ControlId);
                    }
                    break;
                case "win32_window_text":
                    depends_on.Add((element, new IdentifierExpression("win32_window_text")));
                    if (WindowTextKnown)
                    {
                        return new UiDomString(WindowText);
                    }
                    break;
            }
            return ChildEvaluateIdentifier(identifier, depends_on);
        }

        internal UiDomValue ChildEvaluateIdentifier(string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "win32_hwnd":
                    return new UiDomInt((int)Hwnd);
                case "win32_pid":
                    return new UiDomInt(Pid);
                case "win32_tid":
                    return new UiDomInt(Tid);
                case "win32_process_name":
                    if (ProcessName is null)
                    {
                        try
                        {
                            using (var process = Process.GetProcessById(Pid))
                                ProcessName = process.ProcessName;
                        }
                        catch (ArgumentException)
                        {
                            // process no longer running
                            return UiDomUndefined.Instance;
                        }
                    }
                    return new UiDomString(ProcessName);
            }
            return UiDomUndefined.Instance;
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                        return new UiDomString("win32");
                    break;
                case "win32_use_nonclient":
                    return element.EvaluateIdentifier("recurse", element.Root, depends_on);
                case "enabled":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((Style & WS_DISABLED) == 0);
                case "focusable":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((Style & (WS_SYSMENU | WS_TABSTOP)) == WS_TABSTOP);
                case "control_type":
                case "role":
                    // FIXME: Account for window styles?
                    if (element.Parent is UiDomRoot)
                        return window_role;
                    else
                        return pane_role;
                case "window":
                case "pane":
                    return element.EvaluateIdentifier("role", element.Root, depends_on).
                        EvaluateIdentifier(identifier, element.Root, depends_on);
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            if (win32_styles_by_name.TryGetValue(identifier, out var style))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((Style & style) == style);
            }
            return ChildEvaluateIdentifierLate(identifier, depends_on);
        }

        internal UiDomValue ChildEvaluateIdentifierLate(string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (child_property_aliases.TryGetValue(identifier, out var aliased))
                return Element.EvaluateIdentifier(aliased, Element.Root, depends_on);
            return UiDomUndefined.Instance;
        }

        public override string[] GetTrackedProperties()
        {
            return tracked_properties;
        }

        private void WatchChildren()
        {
            if (_watchingChildren)
                return;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"WatchChildren for {Element} (win32)");
            Element.SetRecurseMethodProvider(this);
            _watchingChildren = true;
            Utils.RunIdle(PollChildren); // need to wait for any other providers to remove their children
        }

        private void PollChildren()
        {
            if (!_watchingChildren)
                return;

            var child_hwnds = EnumImmediateChildWindows(Hwnd);

            // Remove or ignore any existing children
            int i = 0;
            while (i < child_hwnds.Count)
            {
                var existing = Connection.LookupElement(child_hwnds[i]);
                if (!(existing is null) && existing.Parent != Element)
                {
                    // duplicate elsewhere in tree
                    var hwnd_existing_parent = existing.Parent.ProviderByType<HwndProvider>();
                    if (!(hwnd_existing_parent is null))
                    {
                        // try asking the other parent to remove it
                        hwnd_existing_parent.ReleaseChildren();
                        if (!existing.IsAlive)
                        {
                            i++;
                            continue;
                        }
                    }
                    child_hwnds.RemoveAt(i);
                    continue;
                }
                i++;
            }

            Element.SyncRecurseMethodChildren(child_hwnds, (IntPtr hwnd) => Connection.GetElementName(hwnd),
                (IntPtr hwnd) => Connection.CreateElement(hwnd));
        }

        private void ReleaseChildren()
        {
            // Remove any child HWNDs that no longer belong to this element
            if (!_watchingChildren)
                return;
            List<IntPtr> new_children = new List<IntPtr>(Element.RecurseMethodChildCount);
            bool changed = false;
            for (int i = 0; i < Element.RecurseMethodChildCount; i++)
            {
                var child_provider = Element.Children[i].ProviderByType<HwndProvider>();
                if (child_provider is null)
                    continue;
                var child_hwnd = child_provider.Hwnd;
                var child_parent_hwnd = GetAncestor(child_hwnd, GA_PARENT);
                if (child_parent_hwnd != Hwnd)
                {
                    changed = true;
                    continue;
                }
                new_children.Add(child_hwnd);
            }
            if (changed)
            {
                Element.SyncRecurseMethodChildren(new_children, (IntPtr hwnd) => Connection.GetElementName(hwnd),
                    (IntPtr hwnd) =>
                    {
                        throw new Exception("ReleaseChildren should not create new children");
                    });
            }
        }

        private void UnwatchChildren()
        {
            if (!_watchingChildren)
                return;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {Element} (win32)");
            _watchingChildren = false;
            Element.UnsetRecurseMethodProvider(this);
        }

        public override void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            switch (name)
            {
                case "recurse_method":
                    if (new_value is UiDomString st && st.Value == "win32")
                        WatchChildren();
                    else
                        UnwatchChildren();
                    break;
                case "win32_use_nonclient":
                    _useNonclient = new_value.ToBool();
                    RefreshNonclientChildren();
                    break;
            }
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_pos":
                        _watchingWindowRect = false;
                        return true;
                }
            }
            return false;
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_pos":
                        _watchingWindowRect = true;
                        if (!WindowRectsKnown)
                            RefreshWindowRects();
                        return true;
                    case "win32_window_text":
                        if (!_fetchingWindowText)
                        {
                            _fetchingWindowText = true;
                            Utils.RunTask(FetchWindowText());
                        }
                        break;
                }
            }
            return false;
        }

        private async Task FetchWindowText()
        {
            string result;
            try
            {
                result = await Connection.CommandThread.OnBackgroundThread(() =>
                {
                    int buffer_size = 256;
                    IntPtr buffer = Marshal.AllocCoTaskMem(buffer_size * 2);
                    try
                    {
                        // For some reason SendMessageCallback refuses to send this
                        int buffer_length = unchecked((int)(long)SendMessageW(Hwnd, WM_GETTEXT, (IntPtr)buffer_size, buffer));
                        if (buffer_length >= 0 && buffer_length <= buffer_size)
                            return Marshal.PtrToStringUni(buffer, buffer_length);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(buffer);
                    }
                    return null;
                }, Tid+1);
            }
            catch (Win32Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (!(result is null))
            {
                WindowText = result;
                WindowTextKnown = true;
                Element.PropertyChanged("win32_window_text", result);
            }
        }

        static bool DebugExceptions = Environment.GetEnvironmentVariable("XALIA_DEBUG_EXCEPTIONS") != "0";

        public static bool IsExpectedException(Win32Exception e)
        {
#if DEBUG
            if (DebugExceptions)
            {
                Utils.DebugWriteLine($"WARNING: Win32 exception:");
                Utils.DebugWriteLine(e);
            }
#endif
            switch (e.NativeErrorCode)
            {
                case 5: // Access denied
                case 1400: // Invalid window handle
                    return true;
                default:
#if DEBUG
                    return false;
#else
                    if (DebugExceptions)
                    {
                        Utils.DebugWriteLine("WARNING: Win32 exception ignored:");
                        Utils.DebugWriteLine(e);
                    }
                    return true;
#endif
            }
        }

        public void MsaaStateChange()
        {
            int new_style = unchecked((int)(long)GetWindowLong(Hwnd, GWL_STYLE));
            if (new_style != Style)
            {
                Style = new_style;
                if (Element.MatchesDebugCondition())
                {
                    Utils.DebugWriteLine($"{Element}.win32_style: {FormatStyles()}");
                }
                Element.PropertyChanged("win32_style");
                if (_useNonclient)
                    RefreshNonclientChildren();
            }
            Element.ProviderByType<HwndButtonProvider>()?.MsaaStateChange();
        }

        public void MsaaAncestorLocationChange()
        {
            if (_watchingWindowRect)
            {
                RefreshWindowRects();
            }
            else
            {
                WindowRectsKnown = false;
            }
            if (_useNonclient)
            {
                Connection.LookupElement(Hwnd, OBJID_VSCROLL)?.
                    ProviderByType<NonclientScrollProvider>()?.
                    ParentLocationChanged();
                Connection.LookupElement(Hwnd, OBJID_HSCROLL)?.
                    ProviderByType<NonclientScrollProvider>()?.
                    ParentLocationChanged();
            }
        }

        public void MsaaChildWindowAdded()
        {
            if (_watchingChildren)
            {
                PollChildren();
            }
        }

        public void MsaaChildWindowRemoved()
        {
            if (_watchingChildren)
            {
                PollChildren();
            }
        }

        private void RefreshWindowRects()
        {
            POINT client_pos = default;
            if (GetWindowRect(Hwnd, out var new_rect) &&
                ClientToScreen(Hwnd, ref client_pos) && GetClientRect(Hwnd, out var new_client_rect))
            {
                new_client_rect.left = client_pos.x;
                new_client_rect.top = client_pos.y;
                new_client_rect.right += client_pos.x;
                new_client_rect.bottom += client_pos.y;
                if (!WindowRectsKnown || !new_rect.Equals(WindowRect) ||
                    !new_client_rect.Equals(ClientRect))
                {
                    WindowRectsKnown = true;
                    WindowRect = new_rect;
                    ClientRect = new_client_rect;
                    if (Element.MatchesDebugCondition())
                    {
                        Utils.DebugWriteLine($"{Element}.win32_(x,y,width,height): {X},{Y},{Width},{Height}");
                        Utils.DebugWriteLine($"{Element}.win32_client_(x,y,width,height): {ClientRect.left},{ClientRect.top},{ClientRect.width},{ClientRect.height}");
                    }
                    Element.PropertyChanged("win32_pos");
                }
            }
        }

        private void RefreshNonclientChildren()
        {
            bool hasVScrollChild = _useNonclient && (Style & WS_VSCROLL) != 0;
            var vs = Connection.LookupElement(Hwnd, OBJID_VSCROLL);
            if (hasVScrollChild != !(vs is null))
            {
                if (hasVScrollChild)
                {
                    var child = Connection.CreateElement(Hwnd, OBJID_VSCROLL);
                    Element.AddChild(Element.Children.Count, child);
                }
                else if (!_useNonclient)
                {
                    Element.RemoveChild(Element.Children.IndexOf(vs));
                }
            }

            bool hasHScrollChild = _useNonclient && (Style & WS_HSCROLL) != 0;
            var hs = Connection.LookupElement(Hwnd, OBJID_HSCROLL);
            if (hasHScrollChild != !(hs is null))
            {
                if (hasHScrollChild)
                {
                    var child = Connection.CreateElement(Hwnd, OBJID_HSCROLL);
                    Element.AddChild(Element.Children.Count, child);
                }
                else if (!_useNonclient)
                {
                    Element.RemoveChild(Element.Children.IndexOf(hs));
                }
            }
        }

        internal RECT DpiAdjustScreenRect(RECT input)
        {
            // Adjust the given rectangle to account for DPI awareness

            var input_dpi = GetDpiForWindow(Hwnd);

            var output_dpi = GetWindowMonitorDpi(false);

            if (input_dpi == output_dpi)
                return input;

            var multiplier = (float)output_dpi / input_dpi;

            // FIXME: Account for multiple monitors with different DPI
            var result = new RECT();
            result.left = (int)Math.Round(input.left * multiplier);
            result.top = (int)Math.Round(input.top * multiplier);
            result.right = (int)Math.Round(input.right * multiplier);
            result.bottom = (int)Math.Round(input.bottom * multiplier);

            return result;
        }

        internal int GetWindowMonitorDpi(bool vertical)
        {
            IntPtr monitor = MonitorFromWindow(Hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor == IntPtr.Zero)
                throw new Win32Exception();

            try
            {
                int hr = GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out int dpix, out int dpiy);

                Marshal.ThrowExceptionForHR(hr);

                return vertical ? dpiy : dpix;
            }
            catch (Win32Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
                return 96;
            }
        }
    }
}
