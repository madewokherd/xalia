using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndTabProvider : HwndItemListProvider, IWin32Styles
    {
        public HwndTabProvider(HwndProvider hwndProvider) : base(hwndProvider)
        {
        }

        static readonly UiDomEnum role = new UiDomEnum(new[] { "tab", "page_tab_list", "pagetablist" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "selection_index", "win32_selection_index" },
        };

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

        public int SelectionIndex { get; private set; }
        public bool SelectionIndexKnown { get; private set; }
        private bool fetching_selection_index;

        public override void DumpProperties(UiDomElement element)
        {
            if (SelectionIndexKnown)
                Utils.DebugWriteLine($"  win32_selection_index: {SelectionIndex}");
            base.DumpProperties(element);
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
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_tab_control":
                case "is_hwnd_tabcontrol":
                    return UiDomBoolean.True;
                case "win32_selection_index":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (SelectionIndexKnown)
                        return new UiDomInt(SelectionIndex);
                    break;
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

        protected async override Task<int> FetchItemCount()
        {
            var result = await SendMessageAsync(Hwnd, TCM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            return Utils.TruncatePtr(result);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_selection_index":
                        if (!SelectionIndexKnown && !fetching_selection_index)
                        {
                            fetching_selection_index = true;
                            Utils.RunTask(FetchSelectionIndex());
                        }
                        return true;
                }
            }
            return base.WatchProperty(element, expression);
        }

        private async Task FetchSelectionIndex()
        {
            var res = await SendMessageAsync(Hwnd, TCM_GETCURSEL, IntPtr.Zero, IntPtr.Zero);
            if (SelectionIndexKnown)
            {
                // If we already know this, it must have come from an MSAA event and can be
                // assumed to be up to date
                return;
            }

            SelectionIndexKnown = true;
            SelectionIndex = Utils.TruncatePtr(res);
            Element.PropertyChanged("win32_selection_index", SelectionIndex);
        }

        internal void MsaaSelectionChange(int idChild)
        {
            SelectionIndex = idChild - 1;
            SelectionIndexKnown = true;
            Element.PropertyChanged("win32_selection_index", SelectionIndex);
        }
    }
}