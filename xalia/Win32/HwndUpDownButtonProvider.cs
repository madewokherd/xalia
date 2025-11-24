using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal class HwndUpDownButtonProvider : UiDomProviderBase
    {
        public HwndUpDownButtonProvider(HwndUpDownProvider parent, int child_id)
        {
            Parent = parent;
            ChildId = child_id;
        }

        public HwndUpDownProvider Parent { get; }
        public int ChildId { get; }
        public HwndProvider HwndProvider => Parent.HwndProvider;

        static readonly UiDomEnum role = new UiDomEnum(new string[] { "button", "push_button", "pushbutton" });

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_updown_button":
                case "is_hwnd_up_down_button":
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
                case "button":
                case "push_button":
                case "pushbutton":
                case "enabled":
                case "visible":
                    return UiDomBoolean.True;
                // FIXME: size/position calculation is approximate
                case "x":
                    depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
                    if (HwndProvider.WindowRectsKnown)
                    {
                        if (ChildId == 1 || !Parent.Horizontal)
                            return new UiDomInt(HwndProvider.ClientRect.left);
                        else
                            return new UiDomInt(HwndProvider.ClientRect.left + HwndProvider.ClientRect.width / 2);
                    }
                    break;
                case "y":
                    depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
                    if (HwndProvider.WindowRectsKnown)
                    {
                        if (ChildId == 1 || Parent.Horizontal)
                            return new UiDomInt(HwndProvider.ClientRect.top);
                        else
                            return new UiDomInt(HwndProvider.ClientRect.top + HwndProvider.ClientRect.height / 2);
                    }
                    break;
                case "width":
                    depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
                    if (HwndProvider.WindowRectsKnown)
                    {
                        if (Parent.Horizontal)
                            return new UiDomInt(HwndProvider.ClientRect.width / 2);
                        else
                            return new UiDomInt(HwndProvider.ClientRect.width);
                    }
                    break;
                case "height":
                    depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
                    if (HwndProvider.WindowRectsKnown)
                    {
                        if (Parent.Horizontal)
                            return new UiDomInt(HwndProvider.ClientRect.height);
                        else
                            return new UiDomInt(HwndProvider.ClientRect.height / 2);
                    }
                    break;
            }
            return Parent.HwndProvider.ChildEvaluateIdentifierLate(identifier, depends_on);
        }
    }
}