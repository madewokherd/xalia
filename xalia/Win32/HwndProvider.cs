using Accessibility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public IntPtr Hwnd { get; }
        public UiDomElement Element { get; }
        public Win32Connection Connection { get; }
        public string ClassName { get; }
        public string RealClassName { get; }
        public int Pid { get; }
        public int Tid { get; }

        private bool _watchingChildren;
        private int _childCount;

        private static string[] tracked_properties = new string[] { "recurse_method" };

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "hwnd", "win32_hwnd" },
            { "class_name", "win32_class_name" },
            { "real_class_name", "win32_real_class_name" },
            { "pid", "win32_pid" },
            { "tid", "win32_tid" },
            { "style", "win32_style" },
            { "x", "win32_x" },
            { "y", "win32_y" },
            { "width", "win32_width" },
            { "height", "win32_height" },
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
        public bool WindowRectKnown { get; private set; }
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
                    Element.AddProvider(new AccessibleProvider(this, Element, acc, 0), 0);
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
                    Element.AddProvider(new HwndDialogProvider(this), 0);
                    return;
                case "Button":
                    Element.AddProvider(new HwndButtonProvider(this), 0);
                    return;
                case "ComboBox":
                    Element.AddProvider(new HwndComboBoxProvider(this), 0);
                    return;
                case "msctls_trackbar32":
                    Element.AddProvider(new HwndTrackBarProvider(this), 0);
                    return;
                case "SysTabControl32":
                    Element.AddProvider(new HwndTabProvider(this), 0);
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
                    Element.AddProvider(new HwndButtonProvider(this), 0);
                    return;
                case 65536 + 5:
                    Element.AddProvider(new HwndComboBoxProvider(this), 0);
                    return;
                case 65536 + 15:
                    Element.AddProvider(new HwndTabProvider(this), 0);
                    return;
                case 65536 + 18:
                    Element.AddProvider(new HwndTrackBarProvider(this), 0);
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
            Utils.DebugWriteLine($"  win32_hwnd: {Hwnd}");
            Utils.DebugWriteLine($"  win32_class_name: \"{ClassName}\"");
            Utils.DebugWriteLine($"  win32_real_class_name: \"{RealClassName}\"");
            Utils.DebugWriteLine($"  win32_pid: {Pid}");
            Utils.DebugWriteLine($"  win32_tid: {Tid}");
            Utils.DebugWriteLine($"  win32_style: {FormatStyles()}");
            if (WindowRectKnown)
            {
                Utils.DebugWriteLine($"  win32_x: {X}");
                Utils.DebugWriteLine($"  win32_y: {Y}");
                Utils.DebugWriteLine($"  win32_width: {Width}");
                Utils.DebugWriteLine($"  win32_height: {Height}");
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

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_element":
                    return UiDomBoolean.True;
                case "win32_hwnd":
                    return new UiDomInt((int)Hwnd);
                case "win32_class_name":
                    return new UiDomString(ClassName);
                case "win32_real_class_name":
                    return new UiDomString(RealClassName);
                case "win32_pid":
                    return new UiDomInt(Pid);
                case "win32_tid":
                    return new UiDomInt(Tid);
                case "win32_style":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return new UiDomInt(Style);
                case "win32_x":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectKnown)
                        return new UiDomInt(X);
                    break;
                case "win32_y":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectKnown)
                        return new UiDomInt(Y);
                    break;
                case "win32_width":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectKnown)
                        return new UiDomInt(Width);
                    break;
                case "win32_height":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (WindowRectKnown)
                        return new UiDomInt(Height);
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
                case "enabled":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((Style & WS_DISABLED) == 0);
                case "focusable":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((Style & (WS_SYSMENU | WS_TABSTOP)) == WS_TABSTOP);
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            if (win32_styles_by_name.TryGetValue(identifier, out var style))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((Style & style) == style);
            }
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
            _watchingChildren = true;
            Utils.RunIdle(PollChildren); // need to wait for any other providers to remove their children
        }

        private void PollChildren()
        {
            if (!_watchingChildren)
                return;

            var child_hwnds = EnumImmediateChildWindows(Hwnd);

            // First remove any existing children that are missing or out of order
            int i = 0;
            foreach (var new_child in child_hwnds)
            {
                var child_name = $"hwnd-{new_child}";
                if (!Element.Children.Exists((UiDomElement element) => element.DebugId == child_name))
                    continue;
                while (Element.Children[i].DebugId != child_name)
                {
                    Element.RemoveChild(i);
                    _childCount--;
                }
                i++;
            }

            // Remove any remaining missing children
            while (i < _childCount)
            {
                Element.RemoveChild(i);
                _childCount--;
            }

            // Add any new children
            i = 0;
            foreach (var new_child in child_hwnds)
            {
                var child_name = $"hwnd-{new_child}";
                if (_childCount <= i || Element.Children[i].DebugId != child_name)
                {
                    if (!(Connection.LookupElement(child_name) is null))
                    {
                        // Child element is a duplicate of another element somewhere in the tree.
                        continue;
                    }
                    Element.AddChild(i, Connection.CreateElementFromHwnd(new_child));
                    _childCount++;
                }
                i += 1;
            }
        }

        private void UnwatchChildren()
        {
            if (!_watchingChildren)
                return;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {Element} (win32)");
            _watchingChildren = false;
            for (int i = _childCount - 1; i >= 0; i--)
                Element.RemoveChild(i);
            _childCount = 0;
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
                        if (!WindowRectKnown)
                            RefreshWindowRect();
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
            }
            Element.ProviderByType<HwndButtonProvider>()?.MsaaStateChange();
        }

        public void MsaaAncestorLocationChange()
        {
            if (_watchingWindowRect)
            {
                RefreshWindowRect();
            }
            else
            {
                WindowRectKnown = false;
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

        private void RefreshWindowRect()
        {
            if (GetWindowRect(Hwnd, out var new_rect))
            {
                if (!WindowRectKnown || !new_rect.Equals(WindowRect))
                {
                    WindowRectKnown = true;
                    WindowRect = new_rect;
                    if (Element.MatchesDebugCondition())
                        Utils.DebugWriteLine($"{Element}.win32_(x,y,width,height): {X},{Y},{Width},{Height}");
                    Element.PropertyChanged("win32_pos");
                }
            }
        }
    }
}
