using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal class HwndRichEditProvider : HwndEditProvider
    {
        public HwndRichEditProvider(HwndProvider hwndProvider) : base(hwndProvider)
        {
        }

        static UiDomEnum role = new UiDomEnum(new string[] { "document" });

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_richedit":
                case "is_hwnd_rich_edit":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "document":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }
    }
}
