using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndScrollBar : UiDomProviderBase, IWin32Styles
    {
        public HwndScrollBar(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;

        static UiDomEnum role = new UiDomEnum(new string[] { "scroll_bar", "scrollbar" });

        public void GetStyleNames(int style, List<string> names)
        {
            if ((style & SBS_SIZEBOX) != 0)
                names.Add("sizebox");
            if ((style & SBS_SIZEGRIP) != 0)
                names.Add("sizegrip");
            if ((style & SBS_VERT) != 0)
                names.Add("vert");
            if ((style & (SBS_SIZEBOX|SBS_SIZEGRIP)) != 0)
            {
                if ((style & SBS_TOPALIGN) != 0)
                    names.Add("sizeboxtopleftalign");
                if ((style & SBS_BOTTOMALIGN) != 0)
                    names.Add("sizeboxbottomrightalign");
            }
            else if ((style & SBS_VERT) != 0)
            {
                if ((style & SBS_TOPALIGN) != 0)
                    names.Add("leftalign");
                if ((style & SBS_BOTTOMALIGN) != 0)
                    names.Add("rightalign");
            }
            else
            {
                names.Add("horz");
                if ((style & SBS_TOPALIGN) != 0)
                    names.Add("topalign");
                if ((style & SBS_BOTTOMALIGN) != 0)
                    names.Add("bottomalign");
            }
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_scroll_bar":
                case "is_hwnd_scrollbar":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "scrollbar":
                case "scroll_bar":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "horz":
                case "horizontal":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP)) == 0);
                case "vert":
                case "vertical":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & SBS_VERT) != 0);
                case "topalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP | SBS_TOPALIGN)) == SBS_TOPALIGN);
                case "bottomalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP | SBS_BOTTOMALIGN)) == SBS_BOTTOMALIGN);
                case "leftalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP | SBS_TOPALIGN)) == (SBS_VERT | SBS_TOPALIGN));
                case "rightalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP | SBS_BOTTOMALIGN)) == (SBS_VERT | SBS_BOTTOMALIGN));
                case "sizeboxtopleftalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_SIZEBOX | SBS_SIZEGRIP)) != 0 &&
                        (HwndProvider.Style & (SBS_VERT | SBS_TOPALIGN)) == SBS_TOPALIGN);
                case "sizeboxbottomrightalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_SIZEBOX | SBS_SIZEGRIP)) != 0 &&
                        (HwndProvider.Style & (SBS_VERT | SBS_BOTTOMALIGN)) == SBS_BOTTOMALIGN);
                case "sizebox":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & SBS_SIZEBOX) != 0);
                case "sizegrip":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & SBS_SIZEGRIP) != 0);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }
    }
}
