using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListBoxProvider : UiDomProviderBase, IWin32Styles, IWin32Container, IWin32ScrollChange, IWin32LocationChange, IWin32Scrollable
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

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "item_count", "win32_item_count" },
            { "item_height", "win32_item_height" },
            { "top_index", "win32_top_index" },
        };

        static string[] tracked_properties = { "recurse_method" };
        
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

        public bool ItemCountKnown;
        public int ItemCount;
        private bool fetching_item_count;
        private bool watching_item_count;

        public bool ItemHeightKnown;
        public int ItemHeight;
        private bool fetching_item_height;
        private bool watching_item_height;

        public bool TopIndexKnown;
        public int TopIndex;
        private bool fetching_top_index;
        private bool watching_top_index;

        private int uniqueid;
        private int first_child_id;
        private int end_child_id;

        public int FirstChildId => first_child_id;

        bool watching_children;
        bool watching_children_visible;

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_list_box":
                case "is_hwnd_listbox":
                    return UiDomBoolean.True;
                case "win32_item_count":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (ItemCountKnown)
                        return new UiDomInt(ItemCount);
                    break;
                case "win32_item_height":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (ItemHeightKnown)
                        return new UiDomInt(ItemHeight);
                    break;
                case "win32_top_index":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (TopIndexKnown)
                        return new UiDomInt(TopIndex);
                    break;
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
                case "role":
                case "control_type":
                    return role;
                case "recurse_method":
                    if (Element.EvaluateIdentifier("recurse", Root, depends_on).ToBool())
                    {
                        return new UiDomString("win32_list_visible");
                    }
                    break;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
            {
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
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

        private async Task FetchItemCount()
        {
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }

            ItemCount = Utils.TruncatePtr(result);
            ItemCountKnown = true;
            fetching_item_count = false;
            Element.PropertyChanged("win32_item_count", ItemCount);
        }

        private async Task FetchItemHeight()
        {
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, LB_GETITEMHEIGHT, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }

            ItemHeight = Utils.TruncatePtr(result);
            ItemHeightKnown = true;
            fetching_item_height = false;
            Element.PropertyChanged("win32_item_height", ItemHeight);
        }

        private async Task FetchTopIndex()
        {
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, LB_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }

            TopIndex = Utils.TruncatePtr(result);
            TopIndexKnown = true;
            fetching_top_index = false;
            Element.PropertyChanged("win32_top_index", TopIndex);
        }

        public override string[] GetTrackedProperties()
        {
            return tracked_properties;
        }

        private void UnwatchChildren()
        {
            HwndProvider.HwndChildrenChanged -= HwndProvider_HwndChildrenChanged;
            Element.UnsetRecurseMethodProvider(this);
        }

        public override void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            if (name == "recurse_method")
            {
                string string_value = new_value is UiDomString id ? id.Value : string.Empty;
                bool was_watching = watching_children;
                switch (string_value)
                {
                    case "win32_list":
                        if (watching_children && !watching_children_visible)
                            break;
                        watching_children = true;
                        watching_children_visible = false;
                        WatchChildren(was_watching);
                        break;
                    case "win32_list_visible":
                        if (watching_children && watching_children_visible)
                            break;
                        watching_children = true;
                        watching_children_visible = true;
                        WatchChildren(was_watching);
                        break;
                    default:
                        if (was_watching)
                        {
                            watching_children = false;
                            UnwatchChildren();
                        }
                        break;
                }
            }
            base.TrackedPropertyChanged(element, name, new_value);
        }

        private void WatchChildren(bool was_watching)
        {
            if (!was_watching)
            {
                Element.SetRecurseMethodProvider(this);
                HwndProvider.HwndChildrenChanged += HwndProvider_HwndChildrenChanged;
            }
            RefreshChildren();
        }

        private void RefreshChildren()
        {
            Utils.RunTask(RefreshRange());
        }

        internal struct ViewInfo
        {
            public int item_count;
            public int top_index;
            public int item_height;
            public int items_per_page;
        }

        internal async Task<ViewInfo> GetViewInfoAsync()
        {
            var result = new ViewInfo();
            result.item_count = Utils.TruncatePtr(await SendMessageAsync(Hwnd, LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero));
            result.top_index = Utils.TruncatePtr(await SendMessageAsync(Hwnd, LB_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero));
            result.item_height = Utils.TruncatePtr(await SendMessageAsync(Hwnd, LB_GETITEMHEIGHT, IntPtr.Zero, IntPtr.Zero));

            GetClientRect(Hwnd, out var client_rect);

            result.items_per_page = client_rect.height / result.item_height + (client_rect.height % result.item_height != 0 ? 1 : 0);

            return result;
        }

        private async Task RefreshRange()
        {
            var view_info = await GetViewInfoAsync();

            if (watching_children_visible)
                SetRecurseMethodRange(
                    view_info.top_index + 1,
                    Math.Min(view_info.top_index + view_info.items_per_page + 1, view_info.item_count + 1));
            else
                SetRecurseMethodRange(1, view_info.item_count + 1);
        }

        public UiDomElement GetMsaaChild(int ChildId)
        {
            if (ChildId >= first_child_id)
            {
                int index = ChildId - first_child_id;
                if (index < Element.RecurseMethodChildCount &&
                    !(Element.Children[index].ProviderByType<HwndListBoxItemProvider>() is null))
                    return Element.Children[index];
            }
            return null;
        }

        private string GetUniqueKey()
        {
            return $"listbox-{Hwnd.ToInt64().ToString("x", CultureInfo.InvariantCulture)}-{(++uniqueid).ToString(CultureInfo.InvariantCulture)}";
        }

        private string GetChildKey(int ChildId)
        {
            var child = GetMsaaChild(ChildId);
            if (child is null)
                return GetUniqueKey();
            return child.DebugId;
        }

        private void SetRecurseMethodRange(int start, int end)
        {
            List<(string, IntPtr)> keys = new List<(string, IntPtr)>(end - start);
            for (int i = start; i < end; i++)
            {
                keys.Add((GetChildKey(i), IntPtr.Zero));
            }
            foreach (var hwnd in HwndProvider.GetChildHwnds())
                keys.Add((null, hwnd));
            first_child_id = start;
            end_child_id = end;
            Element.SyncRecurseMethodChildren(keys, ((string, IntPtr) key) => key.Item1 is null ? Connection.GetElementName(key.Item2) : key.Item1,
                CreateChildItem);
        }

        private UiDomElement CreateChildItem((string, IntPtr) key)
        {
            if (key.Item1 is null)
            {
                return Connection.CreateElement(key.Item2);
            }
            else
            {
                var element = new UiDomElement(key.Item1, Root);
                element.AddProvider(new HwndListBoxItemProvider(this, element), 0);
                return element;
            }
        }

        private void HwndProvider_HwndChildrenChanged(object sender, EventArgs e)
        {
            RefreshChildren();
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_item_count":
                        watching_item_count = true;
                        if (!ItemCountKnown && !fetching_item_count)
                        {
                            fetching_item_count = true;
                            Utils.RunTask(FetchItemCount());
                        }
                        return true;
                    case "win32_item_height":
                        watching_item_height = true;
                        if (!ItemHeightKnown && !fetching_item_height)
                        {
                            fetching_item_height = true;
                            Utils.RunTask(FetchItemHeight());
                        }
                        return true;
                    case "win32_top_index":
                        watching_top_index = true;
                        if (!TopIndexKnown && !fetching_top_index)
                        {
                            fetching_top_index = true;
                            Utils.RunTask(FetchTopIndex());
                        }
                        return true;
                }
            }
            return false;
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_item_count":
                        watching_item_count = false;
                        return true;
                    case "win32_item_height":
                        watching_item_height = false;
                        return true;
                    case "win32_top_index":
                        watching_top_index = false;
                        return true;
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        public void MsaaLocationChange()
        {
            if (watching_children_visible)
            {
                Utils.RunTask(RefreshRange());
            }
            if (watching_item_height)
                Utils.RunTask(FetchItemHeight());
            else
                ItemHeightKnown = false;
        }

        public void MsaaChildrenReordered()
        {
            if (watching_children)
                Utils.RunTask(RefreshRange());
            if (watching_item_count)
                Utils.RunTask(FetchItemCount());
            else
                ItemCountKnown = false;
            if (watching_item_height)
                Utils.RunTask(FetchItemHeight());
            else
                ItemHeightKnown = false;
            if (watching_top_index)
                Utils.RunTask(FetchTopIndex());
            else
                TopIndexKnown = false;
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
            }

            if (watching_children && ChildId < first_child_id)
                first_child_id++;

            if (!watching_children ||
                ChildId >= end_child_id ||
                ChildId < first_child_id)
            {
                // Not in the range of children we're watching
                return;
            }

            var child = CreateChildItem((GetUniqueKey(), IntPtr.Zero));
            Element.AddChild(ChildId - first_child_id, child, true);
            if (watching_children_visible)
                Utils.RunTask(RefreshRange());
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
            }

            if (watching_children && ChildId < first_child_id)
                first_child_id--;

            if (!watching_children ||
                ChildId >= end_child_id ||
                ChildId < first_child_id)
            {
                // Not in the range of children we're watching
                return;
            }

            Element.RemoveChild(ChildId - first_child_id, true);
            if (watching_children_visible)
                Utils.RunTask(RefreshRange());
        }

        public void MsaaScrolled(int which)
        {
            if (watching_children_visible)
            {
                Utils.RunTask(RefreshRange());
            }
            if (watching_top_index)
                Utils.RunTask(FetchTopIndex());
            else
                TopIndexKnown = false;
        }

        public IUiDomProvider GetScrollBarProvider(NonclientScrollProvider nonclient)
        {
            return new HwndListBoxScrollProvider(nonclient, this);
        }
    }
}
