using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndComboBoxProvider : UiDomProviderBase, IWin32Styles
    {
        public HwndComboBoxProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }

        static UiDomEnum role = new UiDomEnum(new string[] { "combo_box", "combobox" });

        static string[] style_names =
        {
            "ownerdrawfixed",
            "ownerdrawvariable",
            "autohscroll",
            "oemconvert",
            "sort",
            "hasstrings",
            "nointegralheight",
            "disablenoscroll",
            null,
            "uppercase",
            "lowercase"
        };

        static Dictionary<string,int> style_flags;

        static HwndComboBoxProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x10 << i;
            }
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_combo_box":
                case "is_hwnd_combobox":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
            }
            return UiDomUndefined.Instance;
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "combo_box":
                case "combobox":
                    return UiDomBoolean.True;
                case "simple":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & CBS_TYPEMASK) == CBS_SIMPLE);
                case "dropdown":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & CBS_TYPEMASK) == CBS_DROPDOWN);
                case "dropdownlist":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & CBS_TYPEMASK) == CBS_DROPDOWNLIST);
            }
            if (style_flags.TryGetValue(identifier, out var flag))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((HwndProvider.Style & flag) != 0);
            }
            return UiDomUndefined.Instance;
        }

        public void GetStyleNames(int style, List<string> names)
        {
            switch (HwndProvider.Style & CBS_TYPEMASK)
            {
                case CBS_SIMPLE:
                    names.Add("simple");
                    break;
                case CBS_DROPDOWN:
                    names.Add("dropdown");
                    break;
                case CBS_DROPDOWNLIST:
                    names.Add("dropdownlist");
                    break;
            }
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                if ((HwndProvider.Style & (0x10 << i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }
    }
}