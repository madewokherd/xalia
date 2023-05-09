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
    internal class HwndProvider : IUiDomProvider
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
        public UiDomElement Element { get; private set; }
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

        public void DumpProperties(UiDomElement element)
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
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
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
            }
            return UiDomUndefined.Instance;
        }

        public UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
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

        public Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public string[] GetTrackedProperties()
        {
            return tracked_properties;
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
            Element = null;
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

        public void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
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

        public bool UnwatchProperty(UiDomElement element, GudlExpression expression)
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

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
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
                }
            }
            return false;
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
