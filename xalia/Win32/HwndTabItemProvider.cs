using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal class HwndTabItemProvider : UiDomProviderBase
    {
        public HwndTabItemProvider(HwndTabProvider parent, int childId)
        {
            Parent = parent;
            ChildId = childId;
        }

        public HwndTabProvider Parent { get; }
        public int ChildId { get; }

        public override void DumpProperties(UiDomElement element)
        {
            if (Parent.SelectionIndexKnown)
                Utils.DebugWriteLine($"  selected: {Parent.SelectionIndex == ChildId - 1}");
            if (Parent.ItemRectsKnown && ChildId <= Parent.ItemRects.Length)
            {
                Utils.DebugWriteLine($"  rect: {new Win32Rect(Parent.ItemRects[ChildId - 1])}");
            }
            base.DumpProperties(element);
        }

        static readonly UiDomEnum role = new UiDomEnum(new[] { "tab_item", "tabitem", "page_tab", "pagetab" });

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_tab_item":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "selected":
                    depends_on.Add((Parent.Element, new IdentifierExpression("win32_selection_index")));
                    if (Parent.SelectionIndexKnown)
                        return UiDomBoolean.FromBool(Parent.SelectionIndex == ChildId - 1);
                    break;
                case "x":
                    {
                        depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
                        var rects = Parent.GetItemRects(depends_on);
                        if (ChildId <= rects.Length && Parent.HwndProvider.WindowRectKnown)
                            return new UiDomInt(Parent.HwndProvider.X + rects[ChildId - 1].left);
                        break;
                    }
                case "y":
                    {
                        depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
                        var rects = Parent.GetItemRects(depends_on);
                        if (ChildId <= rects.Length && Parent.HwndProvider.WindowRectKnown)
                            return new UiDomInt(Parent.HwndProvider.Y + rects[ChildId - 1].top);
                        break;
                    }
                case "width":
                    {
                        var rects = Parent.GetItemRects(depends_on);
                        if (ChildId <= rects.Length)
                            return new UiDomInt(rects[ChildId - 1].right - rects[ChildId - 1].left);
                        break;
                    }
                case "height":
                    {
                        var rects = Parent.GetItemRects(depends_on);
                        if (ChildId <= rects.Length)
                            return new UiDomInt(rects[ChildId - 1].bottom - rects[ChildId - 1].top);
                        break;
                    }
                case "rect":
                    {
                        var rects = Parent.GetItemRects(depends_on);
                        if (ChildId <= rects.Length)
                            return new Win32Rect(rects[ChildId - 1]);
                        break;
                    }
                case "tab_item":
                case "tabitem":
                case "page_tab":
                case "pagetab":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }
    }
}