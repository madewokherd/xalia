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
    internal class HwndListBoxItemProvider : UiDomProviderBase, IUiDomScrollToProvider
    {
        public HwndListBoxItemProvider(HwndListBoxProvider parent, UiDomElement element)
        {
            Parent = parent;
            Element = element;
        }

        public HwndListBoxProvider Parent { get; }

        public UiDomElement Element { get; }

        public UiDomRoot Root => Parent.Root;
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public HwndProvider HwndProvider => Parent.HwndProvider;
        public CommandThread CommandThread => HwndProvider.CommandThread;

        public int ChildId
        {
            get
            {
                return Element.IndexInParent + Parent.FirstChildId;
            }
        }

        static readonly UiDomEnum role = new UiDomEnum(new string[] { "list_item", "listitem" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "x", "win32_x" },
            { "y", "win32_y" },
            { "width", "win32_width" },
            { "height", "win32_height" },
        };

        public override void DumpProperties(UiDomElement element)
        {
            Utils.DebugWriteLine($"  msaa_child_id: {ChildId}");
            Parent.HwndProvider.ChildDumpProperties();
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_list_box_item":
                case "is_hwnd_listbox_item":
                case "is_hwnd_subelement":
                    return UiDomBoolean.True;
                case "win32_x":
                    return Parent.Element.EvaluateIdentifier("win32_client_x", Root, depends_on);
                case "win32_y":
                case "win32_height":
                    {
                        depends_on.Add((Parent.Element, new IdentifierExpression("win32_top_index")));
                        depends_on.Add((Parent.Element, new IdentifierExpression("win32_item_height")));
                        depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));

                        if (Parent.TopIndexKnown && Parent.ItemHeightKnown)
                        {
                            var rc = new RECT();
                            rc.top = (ChildId - 1 - Parent.TopIndex) * Parent.ItemHeight;
                            rc.bottom = Parent.ItemHeight + rc.top;
                            var screen = Parent.HwndProvider.ClientRectToScreen(rc);

                            if (identifier == "win32_y")
                                return new UiDomInt(screen.top);
                            else
                                return new UiDomInt(screen.height);
                        }
                        break;
                    }
                case "win32_width":
                    return Parent.Element.EvaluateIdentifier("win32_client_width", Root, depends_on);
            }
            return Parent.HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "role":
                case "control_type":
                    return role;
                case "listitem":
                case "list_item":
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return Parent.HwndProvider.ChildEvaluateIdentifierLate(identifier, depends_on);
        }

        unsafe private RECT GetItemRectBackground()
        {
            RECT rect = new RECT();
            IntPtr result = SendMessageW(HwndProvider.Hwnd, LB_GETITEMRECT,
                (IntPtr)(ChildId - 1), (IntPtr)(&rect));
            if (unchecked((int)result) == LB_ERR)
            {
                return new RECT();
            }
            return rect;
        }

        public async Task<bool> ScrollToAsync()
        {
            var view_info = await Parent.GetViewInfoAsync();

            int index = ChildId - 1;
            int value;
            if (index < view_info.top_index)
                value = index;
            else if (index >= view_info.top_index + view_info.items_per_page)
                value = index - view_info.items_per_page + 1;
            else
                // Item already in view
                return true;

            await SendMessageAsync(Hwnd, LB_SETTOPINDEX, new IntPtr(value), IntPtr.Zero);

            // We may not get a notification of the change
            Utils.RunIdle(UpdatedScroll);

            return true;
        }

        private void UpdatedScroll()
        {
            Parent.MsaaScrolled(0);
        }
    }
}
