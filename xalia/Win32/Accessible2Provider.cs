using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    class Accessible2Provider : UiDomProviderBase
    {
        public Accessible2Provider(HwndProvider hwndProvider, UiDomElement element, IAccessible2 accessible2)
        {
            HwndProvider = hwndProvider;
            Element = element;
            Accessible2 = accessible2;
        }

        public HwndProvider HwndProvider { get; }
        public UiDomElement Element { get; }
        public IAccessible2 Accessible2 { get; }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_accessible2_element":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }
    }
}