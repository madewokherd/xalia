using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndTabProvider : UiDomProviderBase, IWin32Styles
    {
        public HwndTabProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;

        static readonly UiDomEnum role = new UiDomEnum(new[] { "tab", "page_tab_list", "pagetablist" });

        static string[] style_names =
        {
            "scrollopposite",
            null, // bottom or right
            "multiselect",
            "flatbuttons",
            "forceiconleft",
            "forcelabelleft",
            "hottrack",
            "vertical",
            "buttons", // "tabs" if unset
            "multiline", // "singleline" if unset
            "fixedwidth",
            "raggedright", // "rightjustify" if multiline is set and this is unset
            "focusonbuttondown",
            "ownerdrawfixed",
            "tooltips",
            "focusnever",
        };

        static Dictionary<string,int> style_flags;

        static HwndTabProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x1 << i;
            }
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "tab":
                case "page_tab_list":
                case "pagetablist":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "top":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (TCS_BOTTOM | TCS_VERTICAL)) == 0);
                case "bottom":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (TCS_BOTTOM | TCS_VERTICAL)) == TCS_BOTTOM);
                case "left":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (TCS_RIGHT | TCS_VERTICAL)) == TCS_VERTICAL);
                case "right":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (TCS_RIGHT | TCS_VERTICAL)) == (TCS_RIGHT | TCS_VERTICAL));
                case "tabs":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & TCS_BUTTONS) == 0);
                case "singleline":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & TCS_MULTILINE) == 0);
                case "rightjustify":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (TCS_MULTILINE|TCS_RAGGEDRIGHT)) == TCS_MULTILINE);
            }
            if (style_flags.TryGetValue(identifier, out var flag))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((HwndProvider.Style & flag) != 0);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_tab_control":
                case "is_hwnd_tabcontrol":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public void GetStyleNames(int style, List<string> names)
        {
            switch (style & (TCS_RIGHT|TCS_VERTICAL))
            {
                case 0:
                    names.Add("top");
                    break;
                case TCS_BOTTOM:
                    names.Add("bottom");
                    break;
                case TCS_VERTICAL:
                    names.Add("left");
                    break;
                case TCS_VERTICAL | TCS_RIGHT:
                    names.Add("right");
                    break;
            }
            if ((style & TCS_BUTTONS) == 0)
                names.Add("tabs");
            if ((style & TCS_MULTILINE) == 0)
                names.Add("singleline");
            if ((style & (TCS_MULTILINE|TCS_RAGGEDRIGHT)) == TCS_MULTILINE)
                names.Add("rightjustify");
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                if ((HwndProvider.Style & (0x1 << i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }
    }
}