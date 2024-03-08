using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndStaticProvider : UiDomProviderBase, IWin32Styles
    {
        public HwndStaticProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;
        public UiDomRoot Root => Element.Root;

        static UiDomEnum label_role = new UiDomEnum(new string[] { "label", "static" });
        static UiDomEnum icon_role = new UiDomEnum(new string[] { "icon", "static" });
        static UiDomEnum image_role = new UiDomEnum(new string[] { "image", "static" });
        static UiDomEnum filler_role = new UiDomEnum(new string[] { "filler", "static" });
        static UiDomEnum drawing_area_role = new UiDomEnum(new string[] { "drawing_area", "drawingarea", "static" });
        static UiDomEnum static_role = new UiDomEnum(new string[] { "static" });

        static string[] style_names =
        {
            "realsizecontrol", // 0x0040
            "noprefix", // 0x0080
            "notify", // 0x0100
            "centerimage", // 0x0200
            "rightjust", // 0x0400
            "realsizeimage", // 0x0800
            "sunken", // 0x1000
            "editcontrol", // 0x2000
        };

        static string[] type_names =
        {
            "left",
            "center",
            "right",
            "icon",
            "blackrect",
            "grayrect",
            "whiterect",
            "blackframe",
            "grayframe",
            "whiteframe",
            "useritem",
            "simple",
            "leftnowordwrap",
            "ownerdraw",
            "bitmap",
            "enhmetafile",
            "etchedhorz",
            "etchedvert",
            "etchedframe"
        };

        static Dictionary<string,int> style_flags;
        static Dictionary<string,int> type_constants;

        static HwndStaticProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x40 << i;
            }
            type_constants = new Dictionary<string, int>();
            for (int i=0; i<type_names.Length; i++)
            {
                if (type_names[i] is null)
                    continue;
                type_constants[type_names[i]] = i;
            }
        }

        public void GetStyleNames(int style, List<string> names)
        {
            int type = style & SS_TYPEMASK;

            if (type < type_names.Length)
            {
                names.Add(type_names[type]);
            }

            for (int i=0; i < style_names.Length; i++)
            {
                if ((style & (0x40 << i)) != 0)
                    names.Add(style_names[i]);
            }

            switch (style & SS_ELLIPSISMASK)
            {
                case SS_ENDELLIPSIS:
                    names.Add("endellipsis");
                    break;
                case SS_PATHELLIPSIS:
                    names.Add("pathellipsis");
                    break;
                case SS_WORDELLIPSIS:
                    names.Add("wordellipsis");
                    break;
            }
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_static":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        private UiDomEnum GetControlType(int style)
        {
            switch (style & SS_TYPEMASK)
            {
                case SS_LEFT:
                case SS_CENTER:
                case SS_RIGHT:
                case SS_SIMPLE:
                case SS_LEFTNOWORDWRAP:
                    return label_role;
                case SS_ICON:
                    return icon_role;
                case SS_BLACKRECT:
                case SS_GRAYRECT:
                case SS_WHITERECT:
                case SS_BLACKFRAME:
                case SS_GRAYFRAME:
                case SS_WHITEFRAME:
                case SS_ETCHEDHORZ:
                case SS_ETCHEDVERT:
                case SS_ETCHEDFRAME:
                    return filler_role;
                case SS_OWNERDRAW:
                    return drawing_area_role;
                case SS_BITMAP:
                case SS_ENHMETAFILE:
                    return image_role;
                default:
                    return static_role;
            }
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "static":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return GetControlType(HwndProvider.Style);
                case "label":
                case "icon":
                case "image":
                case "filler":
                case "drawing_area":
                case "drawingarea":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return GetControlType(HwndProvider.Style).EvaluateIdentifier(identifier, Root, depends_on);
                case "endellipsis":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & SS_ELLIPSISMASK) == SS_ENDELLIPSIS);
                case "wordellipsis":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & SS_ELLIPSISMASK) == SS_WORDELLIPSIS);
                case "pathellipsis":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & SS_ELLIPSISMASK) == SS_PATHELLIPSIS);
            }
            if (style_flags.TryGetValue(identifier, out var flag))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((HwndProvider.Style & flag) != 0);
            }
            if (type_constants.TryGetValue(identifier, out var type))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((HwndProvider.Style & SS_TYPEMASK) == type);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }
    }
}
