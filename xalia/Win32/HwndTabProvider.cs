using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndTabProvider : UiDomProviderBase, IWin32Container, IWin32Styles, IWin32LocationChange
    {
        public HwndTabProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;
        public Win32Connection Connection => HwndProvider.Connection;
        public UiDomRoot Root => Element.Root;
        public int Pid => HwndProvider.Pid;

        static readonly UiDomEnum role = new UiDomEnum(new[] { "tab", "page_tab_list", "pagetablist" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "selection_index", "win32_selection_index" },
            { "item_count", "win32_item_count" },
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

        public bool ItemCountKnown;
        public int ItemCount;
        private bool fetching_item_count;
        private bool watching_item_count;

        private int uniqueid;

        bool watching_children;

        public RECT[] ItemRects { get; private set; }
        public bool ItemRectsKnown { get; private set; }
        private bool watching_item_rects;
        private int item_rects_change_count;

        public override void DumpProperties(UiDomElement element)
        {
            if (ItemCountKnown)
                Utils.DebugWriteLine($"  win32_item_count: {ItemCount}");
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
                case "win32_item_count":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (ItemCountKnown)
                        return new UiDomInt(ItemCount);
                    break;
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
            HwndProvider.HwndChildrenChanged += HwndProvider_HwndChildrenChanged;
            if (ItemCountKnown)
                RefreshChildren();
            else
                Utils.RunTask(FetchItemCount());
        }

        private string GetUniqueKey()
        {
            return $"tab-{Hwnd:x}-{++uniqueid}";
        }

        private string GetChildKey(int ChildId)
        {
            var child = GetMsaaChild(ChildId);
            if (child is null)
                return GetUniqueKey();
            return child.DebugId;
        }

        private UiDomElement CreateChildItem((string, IntPtr) key)
        {
            if (key.Item1 is null)
            {
                // hwnd
                return Connection.CreateElement(key.Item2);
            }
            else
            {
                var element = new UiDomElement(key.Item1, Root);
                element.AddProvider(new HwndTabItemProvider(this, element), 0);
                return element;
            }
        }

        private void RefreshChildren()
        {
            List<(string, IntPtr)> keys = new List<(string, IntPtr)>(ItemCount);
            for (int i = 1; i < ItemCount + 1; i++)
            {
                keys.Add((GetChildKey(i), IntPtr.Zero));
            }
            foreach (var hwnd in HwndProvider.GetChildHwnds())
                keys.Add((null, hwnd));
            Element.SyncRecurseMethodChildren(keys,
                ((string, IntPtr) key) => key.Item1 is null ? Connection.GetElementName(key.Item2) : key.Item1,
                CreateChildItem);
        }

        private void HwndProvider_HwndChildrenChanged(object sender, EventArgs e)
        {
            RefreshChildren();
        }

        private void UnwatchChildren()
        {
            Element.UnsetRecurseMethodProvider(this);
            HwndProvider.HwndChildrenChanged -= HwndProvider_HwndChildrenChanged;
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

        private async Task<int> FetchItemCount()
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

            ItemCount = Utils.TruncatePtr(result);
            ItemCountKnown = true;
            fetching_item_count = false;
            Element.PropertyChanged("win32_item_count", ItemCount);

            if (watching_children)
            {
                InvalidateItemRects();
                RefreshChildren();
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
                    case "win32_item_count":
                        watching_item_count = true;
                        if (!ItemCountKnown && !fetching_item_count)
                            Utils.RunTask(FetchItemCount());
                        return true;
                }
            }
            return base.WatchProperty(element, expression);
        }

        internal async Task FetchItemRects()
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
                    case "win32_item_count":
                        watching_item_count = false;
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

        public void MsaaLocationChange()
        {
            InvalidateItemRects();
        }

        public void MsaaChildCreated(int ChildId)
        {
            if (ItemCountKnown)
            {
                if (ChildId < 1 || ChildId > ItemCount + 1)
                {
                    Utils.DebugWriteLine($"got EVENT_OBJECT_CREATE for {Element} with child id {ChildId} with ItemCount={ItemCount}");
                    return;
                }

                ItemCount++;
                Element.PropertyChanged("win32_item_count", ItemCount);

                if (watching_children)
                {
                    var child = CreateChildItem((GetUniqueKey(), IntPtr.Zero));
                    Element.AddChild(ChildId - 1, child, true);

                    InvalidateItemRects();
                }
            }
        }

        public void MsaaChildDestroyed(int ChildId)
        {
            if (ItemCountKnown)
            {
                if (ChildId < 1 || ChildId >= ItemCount + 1)
                {
                    Utils.DebugWriteLine($"got EVENT_OBJECT_DESTROY for {Element} with child id {ChildId} with ItemCount={ItemCount}");
                    return;
                }

                ItemCount--;
                Element.PropertyChanged("win32_item_count", ItemCount);

                if (watching_children)
                {
                    Element.RemoveChild(ChildId - 1, true);
                    InvalidateItemRects();
                }
            }
        }

        private async Task DoChildrenReordered()
        {
            fetching_item_count = true;
            await FetchItemCount();

            // This leaves us in an odd state where we may have existing
            // children that are actually different items now, so we
            // need to invalidate everything. This is preferable to
            // disruption caused by recreating everything if the
            // "reorder" was a minor change.

            InvalidateItemRects();
        }

        public void MsaaChildrenReordered()
        {
            ItemCountKnown = false;
            if (watching_children || watching_item_count)
                Utils.RunTask(DoChildrenReordered());
        }

        public UiDomElement GetMsaaChild(int ChildId)
        {
            if (ChildId >= 1)
            {
                int index = ChildId - 1;
                if (index < ItemCount && index < Element.RecurseMethodChildCount &&
                    !(Element.Children[index].ProviderByType<HwndTabItemProvider>() is null))
                    return Element.Children[index];
            }
            return null;
        }
    }
}