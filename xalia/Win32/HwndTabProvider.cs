using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;
using Xalia.Util;
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

        bool watching_children;

        public RECT[] ItemRects { get; private set; }
        public bool ItemRectsKnown { get; private set; }
        private bool watching_item_rects;
        private int item_rects_change_count;

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
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                        return new UiDomString("win32_tab");
                    break;
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

        public RECT[] GetItemRects(HashSet<(UiDomElement,GudlExpression)> depends_on)
        {
            if (!ItemCountKnown)
            {
                depends_on.Add((Element, new IdentifierExpression("win32_item_count")));
                return null;
            }

            depends_on.Add((Element, new IdentifierExpression("win32_item_rects")));
            if (ItemRectsKnown)
                return ItemRects;

            return null;
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
                case "win32_item_rects":
                    {
                        var rects = GetItemRects(depends_on);
                        if (rects != null)
                            return new Win32ItemRects(rects);
                        break;
                    }
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        static string[] tracked_properties = { "recurse_method" };

        public override string[] GetTrackedProperties()
        {
            return tracked_properties;
        }

        public override void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            if (name == "recurse_method")
            {
                bool new_watching_children = new_value is UiDomString id && id.Value == "win32_tab";
                if (new_watching_children != watching_children)
                {
                    watching_children = new_watching_children;
                    if (new_watching_children)
                        WatchChildren();
                    else
                        UnwatchChildren();
                }
            }
            base.TrackedPropertyChanged(element, name, new_value);
        }

        private void WatchChildren()
        {
            Element.SetRecurseMethodProvider(this);
            if (ItemCountKnown)
                SetUiDomChildCount(ItemCount);
            else
                Utils.RunTask(DoFetchItemCount());
        }

        private void UnwatchChildren()
        {
            Element.UnsetRecurseMethodProvider(this);
        }

        protected override void ItemCountChanged(int newCount)
        {
            InvalidateItemRects();
            if (watching_children)
                SetUiDomChildCount(newCount);
            base.ItemCountChanged(newCount);
        }

        private void SetUiDomChildCount(int newCount)
        {
            if (Element.RecurseMethodChildCount == newCount)
                return;

            Element.SyncRecurseMethodChildren(new Range(1, newCount + 1),
                (int key) => Connection.GetElementName(Hwnd, OBJID_CLIENT, key), CreateChildElement);
        }

        private UiDomElement CreateChildElement(int childId)
        {
            var element = Connection.CreateElement(Hwnd, OBJID_CLIENT, childId);
            element.AddProvider(new HwndTabItemProvider(this, childId), 0);
            return element;
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
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, TCM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return 0;
            }
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
                    case "win32_item_rects":
                        watching_item_rects = true;
                        if (!ItemRectsKnown)
                        {
                            Utils.RunTask(FetchItemRects());
                        }
                        return true;
                }
            }
            return base.WatchProperty(element, expression);
        }

        private async Task FetchItemRects()
        {
            int prev_change_count = item_rects_change_count;
            RECT[] result = new RECT[ItemCount];
            var mem = Win32RemoteProcessMemory.FromPid(Pid);
            try {
                using (var mem_result = mem.Alloc<RECT>())
                {
                    for (int i = 0; i < ItemCount; i++)
                    {
                        var msg_result = await SendMessageAsync(Hwnd, TCM_GETITEMRECT,
                            new IntPtr(i), new IntPtr(unchecked((long)mem_result.Address)));
                        if (item_rects_change_count != prev_change_count)
                            return;
                        if (msg_result == IntPtr.Zero)
                            continue;
                        result[i] = mem_result.Read<RECT>();
                    }
                }
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }
            finally
            {
                mem.Unref();
            }
            ItemRectsKnown = true;
            ItemRects = result;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"{Element}.win32_item_rects: {new Win32ItemRects(result)}");
            Element.PropertyChanged("win32_item_rects");
        }

        public void InvalidateItemRects()
        {
            item_rects_change_count++;
            if (watching_item_rects)
                Utils.RunTask(FetchItemRects());
            else
                ItemRectsKnown = false;
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_item_rects":
                        watching_item_rects = false;
                        return true;
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        private async Task FetchSelectionIndex()
        {
            IntPtr res;
            try
            {
                res = await SendMessageAsync(Hwnd, TCM_GETCURSEL, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }
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
            // If the selection changed to a different row, item rects may have changed.
            int new_selection_index = idChild - 1;
            if (ItemRectsKnown &&
                0 < SelectionIndex && SelectionIndex < ItemRects.Length &&
                0 < new_selection_index && new_selection_index < ItemRects.Length)
            {
                var old_r = ItemRects[SelectionIndex];
                var new_r = ItemRects[new_selection_index];
                if ((HwndProvider.Style & TCS_VERTICAL) != 0 ?
                    (old_r.left != new_r.left) :
                    (old_r.top != new_r.top))
                    InvalidateItemRects();
            }
            else
                InvalidateItemRects();

            SelectionIndex = new_selection_index;
            SelectionIndexKnown = true;
            Element.PropertyChanged("win32_selection_index", SelectionIndex);
        }

        internal void MsaaLocationChange()
        {
            InvalidateItemRects();
        }
    }
}