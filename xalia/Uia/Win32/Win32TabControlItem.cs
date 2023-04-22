using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Uia.Win32
{
    internal class Win32TabControlItem : UiDomElement
    {
        public Win32TabControlItem(Win32TabControl parent, int index) : base($"Win32TabControlItem-{parent.Hwnd}-{index}", parent.Root)
        {
            Parent = parent;
            Hwnd = parent.Hwnd;
            Index = index;
        }

        protected override void SetAlive(bool value)
        {
            if (!value)
            {
                if (!(remote_process_memory is null))
                {
                    remote_process_memory.Unref();
                    remote_process_memory = null;
                }
            }
            base.SetAlive(value);
        }

        private static readonly UiDomValue role = new UiDomEnum(new[] { "tab_item", "tabitem", "page_tab", "pagetab" });

        private Win32RemoteProcessMemory remote_process_memory;
        private bool BoundsKnown;
        private int X;
        private int Y;
        private int Width;
        private int Height;

        public new Win32TabControl Parent { get; }
        public IntPtr Hwnd { get; }
        public int Index { get; }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "is_win32_subelement":
                case "is_win32_tabcontrol_item":
                case "tab_item":
                case "tabitem":
                case "page_tab":
                case "pagetab":
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "selected":
                    depends_on.Add((Parent, new IdentifierExpression("win32_selection_index")));
                    if (Parent.SelectionIndexKnown)
                        return UiDomBoolean.FromBool(Index == Parent.SelectionIndex);
                    return UiDomUndefined.Instance;
                case "win32_x":
                case "win32_y":
                case "win32_width":
                case "win32_height":
                    depends_on.Add((this, new IdentifierExpression("win32_bounds")));
                    if (BoundsKnown)
                    {
                        switch (id)
                        {
                            case "win32_x":
                                return new UiDomInt(X);
                            case "win32_y":
                                return new UiDomInt(Y);
                            case "win32_width":
                                return new UiDomInt(Width);
                            case "win32_height":
                                return new UiDomInt(Height);
                        }
                    }
                    return UiDomUndefined.Instance;
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override void DumpProperties()
        {
            if (Parent.SelectionIndexKnown)
                Utils.DebugWriteLine($"  selected: {Index == Parent.SelectionIndex}");
            if (BoundsKnown)
            {
                Utils.DebugWriteLine($"  win32_x: {X}");
                Utils.DebugWriteLine($"  win32_y: {Y}");
                Utils.DebugWriteLine($"  win32_width: {Width}");
                Utils.DebugWriteLine($"  win32_height: {Height}");
            }
            base.DumpProperties();
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_bounds":
                        PollProperty(expression, RefreshBounds, 2000);
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
                    case "win32_bounds":
                        EndPollProperty(expression);
                        BoundsKnown = false;
                        break;
                }
            }
            base.UnwatchProperty(expression);
        }

        private async Task RefreshBounds()
        {
            if (remote_process_memory is null)
            {
                GetWindowThreadProcessId(Hwnd, out var pid);
                remote_process_memory = Win32RemoteProcessMemory.FromPid(pid);
            }

            RECT rc;
            bool known;
            using (var memory = remote_process_memory.Alloc<RECT>())
            {
                IntPtr result = await SendMessageAsync(Hwnd, TCM_GETITEMRECT, new IntPtr(Index), new IntPtr((long)memory.Address));
                known = result != IntPtr.Zero;
                rc = memory.Read<RECT>();
            }

            if (known != BoundsKnown || X != rc.left || Y != rc.top ||
                Width != rc.right - rc.left || Height != rc.bottom - rc.top)
            {
                BoundsKnown = known;
                if (known)
                {
                    X = rc.left;
                    Y = rc.top;
                    Width = rc.right - rc.left;
                    Height = rc.bottom - rc.top;

                    if (MatchesDebugCondition())
                        Utils.DebugWriteLine($"{this}.win32_bounds: {X},{Y} {Width}x{Height}");
                    PropertyChanged("win32_bounds");
                }
                else
                {
                    PropertyChanged("win32_bounds", "undefined");
                }
            }
        }
    }
}
