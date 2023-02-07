using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

        internal Win32Element(IntPtr hwnd, UiDomRoot root) : base(root)
        {
            Hwnd = hwnd;
            _debugid = $"{GetType().Name}-{hwnd}";
        }

        private readonly string _debugid;
        private static readonly Dictionary<string, string> property_aliases;
        private string ProcessName;

        public IntPtr Hwnd { get; }

        internal RECT WindowRect { get; private set; }
        public bool WindowRectKnown { get; private set; }
        public int X => WindowRect.left;
        public int Y => WindowRect.top;
        public int Width => WindowRect.right - WindowRect.left;
        public int Height => WindowRect.bottom - WindowRect.top;

        public int WindowStyle { get; private set; }
        public bool WindowStyleKnown { get; private set; }

        public override string DebugId => _debugid;

        public override bool Equals(object obj)
        {
            if (obj is Win32Element win32)
            {
                return _debugid == win32._debugid;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Hwnd.GetHashCode() ^ GetType().GetHashCode();
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
                Console.WriteLine($"  win32_process_name: {ProcessName}");
            if (WindowRectKnown)
            {
                Console.WriteLine($"  win32_x: {X}");
                Console.WriteLine($"  win32_y: {Y}");
                Console.WriteLine($"  win32_width: {Width}");
                Console.WriteLine($"  win32_height: {Height}");
            }
            if (WindowStyleKnown)
            {
                Console.WriteLine($"  win32_style: 0x{WindowStyle:X}");
                Console.WriteLine($"  win32_visible: {(WindowStyle & WS_VISIBLE) == WS_VISIBLE}");
                Console.WriteLine($"  win32_enabled: {(WindowStyle & WS_DISABLED) == 0}");
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
                        Console.WriteLine($"{DebugId}.win32_rect: {X},{Y} {Width}x{Height}");
                    else
                        Console.WriteLine($"{DebugId}.win32_rect: undefined");
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
                    Console.WriteLine($"{DebugId}.win32_style: {new_style:x}");

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
    }
}
