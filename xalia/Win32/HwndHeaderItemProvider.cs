using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal class HwndHeaderItemProvider : UiDomProviderBase
    {
        public HwndHeaderItemProvider(HwndHeaderProvider parent, UiDomElement element)
        {
            Parent = parent;
            Element = element;
        }

        public HwndHeaderProvider Parent { get; }
        public UiDomElement Element { get; }

        public int ChildId
        {
            get
            {
                return Element.IndexInParent + 1;
            }
        }

        static readonly UiDomEnum role = new UiDomEnum(new string[] { "column_header", "columnheader" });

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
                case "is_hwnd_header_item":
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
                    return role;
                case "column_header":
                case "columnheader":
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
            }
            return Parent.HwndProvider.ChildEvaluateIdentifierLate(identifier, depends_on);
        }
    }
}
