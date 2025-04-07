using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListBoxProvider : UiDomProviderBase, IWin32Styles
    {
        public HwndListBoxProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }

        public IntPtr Hwnd => HwndProvider.Hwnd;
        public Win32Connection Connection => HwndProvider.Connection;
        public UiDomElement Element => HwndProvider.Element;
        public int Tid => HwndProvider.Tid;
        public int Pid => HwndProvider.Pid;
        public UiDomRoot Root => Element.Root;

        static UiDomEnum role = new UiDomEnum(new string[] { "list", "list_box", "listbox" });

        static string[] style_names =
        {
            "notify",
            "sort",
            "noredraw",
            "multiplesel",
            "ownerdrawfixed",
            "ownerdrawvariable",
            "hasstrings",
            "usetabstops",
            "nointegralheight",
            "multicolumn",
            "wantkeyboardinput",
            "extendedsel",
            "disablenoscroll",
            "nodata",
            "nosel",
            "combobox"
        };

        static Dictionary<string,int> style_flags;

        static HwndListBoxProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x1 << i;
            }
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_list_box":
                case "is_hwnd_listbox":
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
                case "list":
                case "list_box":
                case "listbox":
                    return UiDomBoolean.True;
            }
            if (style_flags.TryGetValue(identifier, out var flag))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((HwndProvider.Style & flag) != 0);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public void GetStyleNames(int style, List<string> names)
        {
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
