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
            base.DumpProperties(element);
        }

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
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }
    }
}