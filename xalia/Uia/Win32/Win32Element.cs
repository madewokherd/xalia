using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Uia.Win32
{
    public class Win32Element : UiDomElement
    {
        static Win32Element()
        {
            string[] aliases = {
                "x", "win32_x",
                "y", "win32_y",
                "width", "win32_width",
                "height", "win32_height",
                "style", "win32_style",
                "enabled", "win32_enabled",
                "visible", "win32_visible",
                "set_foreground_window", "win32_set_foreground_window",
                "application_name", "win32_process_name",
                "process_name", "win32_process_name",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i=0; i<aliases.Length; i+=2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
        }

        public Win32Element(string typename, IntPtr hwnd, UiaConnection root) : base($"{typename}-{hwnd}", root)
        {
            Hwnd = hwnd;
            Root = root;
        }

        public new UiaConnection Root { get; }
        private static readonly Dictionary<string, string> property_aliases;
        private string ProcessName;

        private int _pid;
        public int Pid
        {
            get
            {
                if (_pid == 0)
                {
                    GetWindowThreadProcessId(Hwnd, out _pid);
                }
                return _pid;
            }
        }

        public IntPtr Hwnd { get; }

        internal RECT WindowRect { get; private set; }
        public bool WindowRectKnown { get; private set; }
        public int X => WindowRect.left;
        public int Y => WindowRect.top;
        public int Width => WindowRect.right - WindowRect.left;
        public int Height => WindowRect.bottom - WindowRect.top;

        public int WindowStyle { get; private set; }
        public bool WindowStyleKnown { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj is Win32Element win32)
            {
                return DebugId == win32.DebugId;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Hwnd.GetHashCode() ^ GetType().GetHashCode();
        }

        protected override void SetAlive(bool value)
        {
            if (!value)
            {
                use_virtual_scroll = false;
            }
            base.SetAlive(value);
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (property_aliases.TryGetValue(id, out string aliased))
            {
                var value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;
                id = aliased;
            }

            switch (id)
            {
                case "is_win32_element":
                    return UiDomBoolean.True;
                case "win32_set_foreground_window":
                    return new UiDomRoutineSync(this, "win32_set_foreground_window", Win32SetForegroundWindow);
                case "win32_process_name":
                    try
                    {
                        if (ProcessName is null)
                        {
                            GetWindowThreadProcessId(Hwnd, out var pid);
                            using (var process = Process.GetProcessById(pid))
                                ProcessName = process.ProcessName;
                        }
                    }
                    catch (ArgumentException)
                    {
                        return UiDomUndefined.Instance;
                    }
                    return new UiDomString(ProcessName);
                case "win32_x":
                    depends_on.Add((this, new IdentifierExpression("win32_rect")));
                    if (WindowRectKnown)
                        return new UiDomInt(X);
                    return UiDomUndefined.Instance;
                case "win32_y":
                    depends_on.Add((this, new IdentifierExpression("win32_rect")));
                    if (WindowRectKnown)
                        return new UiDomInt(Y);
                    return UiDomUndefined.Instance;
                case "win32_width":
                    depends_on.Add((this, new IdentifierExpression("win32_rect")));
                    if (WindowRectKnown)
                        return new UiDomInt(Width);
                    return UiDomUndefined.Instance;
                case "win32_height":
                    depends_on.Add((this, new IdentifierExpression("win32_rect")));
                    if (WindowRectKnown)
                        return new UiDomInt(Height);
                    return UiDomUndefined.Instance;
                case "win32_style":
                    depends_on.Add((this, new IdentifierExpression("win32_style")));
                    if (WindowStyleKnown)
                        return new UiDomInt(WindowStyle);
                    return UiDomUndefined.Instance;
                case "win32_visible":
                    depends_on.Add((this, new IdentifierExpression("win32_style")));
                    if (WindowStyleKnown)
                        return UiDomBoolean.FromBool((WindowStyle & WS_VISIBLE) == WS_VISIBLE);
                    return UiDomUndefined.Instance;
                case "win32_enabled":
                    depends_on.Add((this, new IdentifierExpression("win32_style")));
                    if (WindowStyleKnown)
                        return UiDomBoolean.FromBool((WindowStyle & WS_DISABLED) == 0);
                    return UiDomUndefined.Instance;
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override void DumpProperties()
        {
            if (!(ProcessName is null))
                Utils.DebugWriteLine($"  win32_process_name: {ProcessName}");
            if (WindowRectKnown)
            {
                Utils.DebugWriteLine($"  win32_x: {X}");
                Utils.DebugWriteLine($"  win32_y: {Y}");
                Utils.DebugWriteLine($"  win32_width: {Width}");
                Utils.DebugWriteLine($"  win32_height: {Height}");
            }
            if (WindowStyleKnown)
            {
                Utils.DebugWriteLine($"  win32_style: 0x{WindowStyle:X}");
                Utils.DebugWriteLine($"  win32_visible: {(WindowStyle & WS_VISIBLE) == WS_VISIBLE}");
                Utils.DebugWriteLine($"  win32_enabled: {(WindowStyle & WS_DISABLED) == 0}");
            }
            base.DumpProperties();
        }

        private void Win32SetForegroundWindow(UiDomRoutineSync obj)
        {
            SetForegroundWindow(Hwnd);
        }

        public Task RefreshWindowRect()
        {
            bool new_rect_known;
            RECT new_rect;
            new_rect_known = GetWindowRect(Hwnd, out new_rect);

            if (new_rect_known != WindowRectKnown ||
                (new_rect_known && !new_rect.Equals(WindowRect)))
            {
                WindowRectKnown = new_rect_known;
                WindowRect = new_rect;

                if (MatchesDebugCondition())
                {
                    if (WindowRectKnown)
                        Utils.DebugWriteLine($"{DebugId}.win32_rect: {X},{Y} {Width}x{Height}");
                    else
                        Utils.DebugWriteLine($"{DebugId}.win32_rect: undefined");
                }

                PropertyChanged("win32_rect");
            }
            return Task.CompletedTask;
        }

        private Task RefreshWindowStyle()
        {
            int new_style = GetWindowLong(Hwnd, GWL_STYLE).ToInt32();

            if (new_style != WindowStyle || !WindowStyleKnown)
            {
                int styles_changed = new_style ^ WindowStyle;
                WindowStyle = new_style;
                WindowStyleKnown = true;

                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{DebugId}.win32_style: {new_style:x}");

                PropertyChanged("win32_style");
            }
            return Task.CompletedTask;
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_rect":
                        PollProperty(expression, RefreshWindowRect, 200);
                        break;
                    case "win32_style":
                        PollProperty(expression, RefreshWindowStyle, 200);
                        break;
                }
            }
            base.WatchProperty(expression);
        }

        protected override void UnwatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_rect":
                        EndPollProperty(expression);
                        WindowRectKnown = false;
                        break;
                    case "win32_style":
                        EndPollProperty(expression);
                        WindowStyleKnown = false;
                        break;
                }
            }
            base.UnwatchProperty(expression);
        }

        protected override void PropertiesChanged(HashSet<GudlExpression> changed_properties)
        {
            if (use_virtual_scroll && changed_properties.Contains(new IdentifierExpression("win32_style")))
            {
                UpdateScrollbars();
            }
            base.PropertiesChanged(changed_properties);
        }

        private bool use_virtual_scroll;
        private bool has_hscroll;
        private bool has_vscroll;

        public bool UseVirtualScrollBars
        {
            get => use_virtual_scroll;
            protected set
            {
                if (value != use_virtual_scroll)
                {
                    use_virtual_scroll = value;
                    if (value)
                    {
                        UpdateScrollbars();
                    }
                    else
                    {
                        has_hscroll = false;
                        has_vscroll = false;

                        int idx = Children.FindIndex(e => e is Win32VirtualScrollbar);
                        if (idx != -1)
                        {
                            RemoveChild(idx);
                            idx = Children.FindIndex(idx, e => e is Win32VirtualScrollbar);
                            if (idx != -1)
                                RemoveChild(idx);
                        }
                    }
                }
            }
        }

        private void UpdateScrollbars()
        {
            bool hscroll = WindowStyleKnown && (WindowStyle & WS_HSCROLL) == WS_HSCROLL;
            bool vscroll = WindowStyleKnown && (WindowStyle & WS_VSCROLL) == WS_VSCROLL;

            if (hscroll != has_hscroll)
            {
                has_hscroll = hscroll;
                if (has_hscroll)
                    AddChild(Children.Count, new Win32VirtualScrollbar(this, false));
                else
                    RemoveChild(Children.FindIndex(e => e is Win32VirtualScrollbar scroll && !scroll.Vertical));
            }
            if (vscroll != has_vscroll)
            {
                has_vscroll = vscroll;
                if (has_vscroll)
                    AddChild(Children.Count, new Win32VirtualScrollbar(this, true));
                else
                    RemoveChild(Children.FindIndex(e => e is Win32VirtualScrollbar scroll && scroll.Vertical));
            }
        }

        public virtual Task<double> GetHScrollMinimumIncrement()
        {
            return Task.FromResult(25.0);
        }

        public virtual Task<double> GetVScrollMinimumIncrement()
        {
            return Task.FromResult(25.0);
        }

        public virtual Task OffsetHScroll(double ofs)
        {
            return Task.CompletedTask;
        }

        public virtual Task OffsetVScroll(double ofs)
        {
            return Task.CompletedTask;
        }
    }
}
