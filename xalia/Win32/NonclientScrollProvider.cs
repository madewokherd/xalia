using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal class NonclientScrollProvider : NonclientProvider
    {
        public NonclientScrollProvider(HwndProvider hwndProvider, UiDomElement element, bool vertical) : base(hwndProvider, element)
        {
            Vertical = vertical;
        }

        static UiDomEnum role = new UiDomEnum(new string[] { "scroll_bar", "scrollbar" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "vertical", "win32_vertical" },
            { "horizontal", "win32_horizontal" },
        };

        public bool Vertical { get; }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_nonclient_scrollbar":
                case "is_nonclient_scroll_bar":
                    return UiDomBoolean.True;
                case "win32_vertical":
                    return UiDomBoolean.FromBool(Vertical);
                case "win32_horizontal":
                    return UiDomBoolean.FromBool(!Vertical);
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "scrollbar":
                case "scroll_bar":
                case "enabled":
                case "visible":
                    return UiDomBoolean.True;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }
    }
}