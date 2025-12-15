using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    class HwndTreeViewProvider : HwndTreeViewItemProvider, IWin32Styles
    {
        public HwndTreeViewProvider(HwndProvider hwndProvider) : base(null, hwndProvider.Element, IntPtr.Zero)
        {
            HwndProvider = hwndProvider;
        }

        public new HwndProvider HwndProvider { get; }
        public new IntPtr Hwnd => HwndProvider.Hwnd;
        public new UiDomElement Element => HwndProvider.Element;
        public Win32Connection Connection => HwndProvider.Connection;
        public new UiDomRoot Root => Element.Root;

        static UiDomEnum role = new UiDomEnum(new string[] { "tree", "tree_view", "treeview", "outline" });

        public int ExtendedStyle;
        public bool ExtendedStyleKnown;

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "extended_treeview_style", "win32_extended_treeview_style" },
        };

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

        static string[] extended_style_names =
        {
            "nosinglecollapse",
            "multiselect",
            "doublebuffer",
            "noindentstyle",
            "richtooltip",
            "autohscroll",
            "fadeinoutexpandos",
            "partialcheckboxes",
            "exclusioncheckboxes",
            "dimmedcheckboxes",
            "drawimageasync"
        };

        static Dictionary<string,int> style_flags;
        static Dictionary<string,int> extended_style_flags;

        static HwndTreeViewProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 1 << i;
            }
            extended_style_flags = new Dictionary<string, int>();
            for (int i=0; i<extended_style_names.Length; i++)
            {
                if (extended_style_names[i] is null)
                    continue;
                extended_style_flags[extended_style_names[i]] = 1 << i;
            }
        }

        private static string ExtendedStyleToString(int style)
        {
            StringBuilder style_list = new StringBuilder();
            bool seen_any_styles = false;
            for (int i=0; i < extended_style_names.Length; i++)
            {
                if (((1 << i) & style) != 0)
                {
                    if (seen_any_styles)
                        style_list.Append('|');
                    style_list.Append(extended_style_names[i]);
                    seen_any_styles = true;
                }
            }
            return $"0x{style:x} ({style_list})";
        }

        private static UiDomValue ExtendedStyleToEnum(int style)
        {
            List<string> result = new List<string>();
            for (int i=0; i < extended_style_names.Length; i++)
            {
                if (((1 << i) & style) != 0)
                {
                    result.Add(extended_style_names[i]);
                }
            }
            if (result.Count == 0)
                return UiDomUndefined.Instance;
            return new UiDomEnum(result.ToArray());
        }

        public override void DumpProperties(UiDomElement element)
        {
            if (ExtendedStyleKnown)
            {
                Utils.DebugWriteLine($"  win32_extended_treeview_style: {ExtendedStyleToString(ExtendedStyle)}");
            }
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_treeview":
                case "is_hwnd_tree_view":
                    return UiDomBoolean.True;
                case "win32_extended_treeview_style":
                    depends_on.Add((Element, new IdentifierExpression(identifier)));
                    if (ExtendedStyleKnown)
                        return new UiDomInt(ExtendedStyle);
                    break;
                case "win32_extended_treeview_style_names":
                    depends_on.Add((Element, new IdentifierExpression("win32_extended_treeview_style")));
                    if (ExtendedStyleKnown)
                        return ExtendedStyleToEnum(ExtendedStyle);
                    break;
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
            if (property_aliases.TryGetValue(identifier, out var aliased)) {
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            }
            if (style_flags.TryGetValue(identifier, out int flag))
            {
                depends_on.Add((Element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((HwndProvider.Style & flag) != 0);
            }
            if (extended_style_flags.TryGetValue(identifier, out int ex_flag))
            {
                depends_on.Add((Element, new IdentifierExpression("win32_extended_treeview_style")));
                if (ExtendedStyleKnown)
                    return UiDomBoolean.FromBool((ExtendedStyle & ex_flag) != 0);
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

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_extended_treeview_style":
                        Element.PollProperty(expression, PollExtendedStyle, 500);
                        return true;
                }
            }
            return base.WatchProperty(element, expression);
        }

        private async Task PollExtendedStyle()
        {
            var style = (int)(long)await SendMessageAsync(Hwnd, TVM_GETEXTENDEDSTYLE, IntPtr.Zero, IntPtr.Zero);

            if (!ExtendedStyleKnown || ExtendedStyle != style)
            {
                ExtendedStyle = style;
                ExtendedStyleKnown = true;
                Element.PropertyChanged("win32_extended_treeview_style", ExtendedStyleToString(style));
            }
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_extended_treeview_style":
                        Element.EndPollProperty(expression);
                        return true;
                }
            }
            return base.UnwatchProperty(element, expression);
        }
    }
}