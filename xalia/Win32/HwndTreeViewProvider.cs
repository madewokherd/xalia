using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    class HwndTreeViewProvider : UiDomProviderBase, IWin32Styles
    {
        public HwndTreeViewProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;
        public Win32Connection Connection => HwndProvider.Connection;
        public UiDomRoot Root => Element.Root;

        static UiDomEnum role = new UiDomEnum(new string[] { "tree", "tree_view", "treeview", "outline" });

        static string[] style_names =
        {
            "hasbuttons",
            "haslines",
            "linesatroot",
            "editlabels",
            "disabledragdrop",
            "showselalways",
            "rtlreading",
            "notooltips",
            "checkboxes",
            "trackselect",
            "singleexpand",
            "infotip",
            "fullrowselect",
            "noscroll",
            "nonevenheight",
            "nonhscroll"
        };

        static Dictionary<string,int> style_flags;

        static HwndTreeViewProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 1 << i;
            }
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_treeview":
                case "is_hwnd_tree_view":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "tree":
                case "tree_view":
                case "treeview":
                case "outline":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
            }
            if (style_flags.TryGetValue(identifier, out int flag))
            {
                depends_on.Add((Element, new IdentifierExpression("win32_style")));
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
                if ((HwndProvider.Style & (1 << i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }
    }
}