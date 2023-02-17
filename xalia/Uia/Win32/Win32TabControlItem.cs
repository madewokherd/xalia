using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;

namespace Xalia.Uia.Win32
{
    internal class Win32TabControlItem : UiDomElement
    {
        public Win32TabControlItem(Win32TabControl parent, int index) : base(parent.Root)
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

        public override string DebugId => $"Win32TabControlItem-{Hwnd}-{Index}";

        private static readonly UiDomValue role = new UiDomEnum(new[] { "tab_item", "tabitem", "page_tab", "pagetab" });

        private Win32RemoteProcessMemory remote_process_memory;

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
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override void DumpProperties()
        {
            if (Parent.SelectionIndexKnown)
                Console.WriteLine($"  selected: {Index == Parent.SelectionIndex}");
            base.DumpProperties();
        }
    }
}
