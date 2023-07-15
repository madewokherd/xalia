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

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_tab_item":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }
    }
}