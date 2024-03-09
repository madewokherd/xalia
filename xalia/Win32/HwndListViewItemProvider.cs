using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListViewItemProvider : UiDomProviderBase
    {
        public HwndListViewItemProvider(HwndListViewProvider parent, UiDomElement element)
        {
            Parent = parent;
            Element = element;
        }

        public HwndListViewProvider Parent { get; }

        public UiDomElement Element { get; }

        public int ChildId
        {
            get
            {
                return Element.IndexInParent + Parent.FirstChildId;
            }
        }

        static readonly UiDomEnum item_role = new UiDomEnum(new string[] { "list_item", "listitem" });
        static readonly UiDomEnum icon_role = new UiDomEnum(new string[] { "icon" });
        static readonly UiDomEnum row_role = new UiDomEnum(new string[] { "row" });

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
                case "is_hwnd_list_view_item":
                case "is_hwnd_listview_item":
                case "is_hwnd_subelement":
                    return UiDomBoolean.True;
            }
            return Parent.HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "role":
                case "control_type":
                    switch (Parent.EvaluateView(depends_on))
                    {
                        case LV_VIEW_DETAILS:
                            return row_role;
                        case LV_VIEW_ICON:
                        case LV_VIEW_SMALLICON:
                            return icon_role;
                        case -1: // view unknown
                            break;
                        default:
                            return item_role;
                    }
                    break;
                case "row":
                    switch (Parent.EvaluateView(depends_on))
                    {
                        case LV_VIEW_DETAILS:
                            return UiDomBoolean.True;
                        case -1: // view unknown
                            break;
                        default:
                            return UiDomBoolean.False;
                    }
                    break;
                case "icon":
                    switch (Parent.EvaluateView(depends_on))
                    {
                        case LV_VIEW_ICON:
                        case LV_VIEW_SMALLICON:
                            return UiDomBoolean.True;
                        case -1: // view unknown
                            break;
                        default:
                            return UiDomBoolean.False;
                    }
                    break;
                case "index":
                    return new UiDomInt(ChildId - 1);
                case "listitem":
                case "list_item":
                    switch (Parent.EvaluateView(depends_on))
                    {
                        case LV_VIEW_ICON:
                        case LV_VIEW_SMALLICON:
                        case LV_VIEW_DETAILS:
                            return UiDomBoolean.False;
                        case -1: // view unknown
                            break;
                        default:
                            return UiDomBoolean.True;
                    }
                    break;
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
            }
            return Parent.HwndProvider.ChildEvaluateIdentifierLate(identifier, depends_on);
        }
    }
}