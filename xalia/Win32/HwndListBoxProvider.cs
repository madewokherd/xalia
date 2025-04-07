using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListBoxProvider : UiDomProviderBase, IWin32Styles, IWin32Container
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

        private int uniqueid;
        private int first_child_id;

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
                case "role":
                case "control_type":
                    return role;
                case "win32_item_count":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (ItemCountKnown)
                        return new UiDomInt(ItemCount);
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

            RefreshChildren();
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
                switch (string_value)
                {
                    case "win32_list":
                        if (watching_children && !watching_children_visible)
                            break;
                        watching_children = true;
                        watching_children_visible = false;
                        WatchChildren();
                        break;
                    case "win32_list_visible":
                        if (watching_children && watching_children_visible)
                            break;
                        watching_children = true;
                        watching_children_visible = true;
                        WatchChildren();
                        break;
                    default:
                        watching_children = false;
                        UnwatchChildren();
                        break;
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
            else if (!fetching_item_count)
            {
                fetching_item_count = true;
                Utils.RunTask(FetchItemCount());
            }
        }

        private void RefreshChildren()
        {
            if (watching_children_visible)
            {
                Utils.RunTask(RefreshRange(true));
            }
            else
            {
                SetRecurseMethodRange(1, ItemCount + 1);
            }
        }

        private async Task RefreshRange(bool full)
        {
            // TODO: Use scrollbar info to figure out range
        }

        public UiDomElement GetMsaaChild(int ChildId)
        {
            if (ChildId >= first_child_id)
            {
                int index = ChildId - first_child_id;
                if (index < Element.RecurseMethodChildCount &&
                    !(Element.Children[index].ProviderByType<HwndListViewItemProvider>() is null))
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
                }
            }
            return base.UnwatchProperty(element, expression);
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

            InvalidateChildBounds();
        }

        public void MsaaChildrenReordered()
        {
            view_change_count++;
            if (watching_view)
                Utils.RunTask(FetchView());
            else
                ViewKnown = false;

            ItemCountKnown = false;
            if (watching_children || watching_item_count)
                Utils.RunTask(DoChildrenReordered());
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

                if (watching_children && ChildId < first_child_id)
                    first_child_id++;

                if (!watching_children ||
                    ChildId > Element.RecurseMethodChildCount + first_child_id ||
                    ChildId < first_child_id)
                {
                    // Not in the range of children we're watching
                    return;
                }

                var child = CreateChildItem((GetUniqueKey(), IntPtr.Zero));
                Element.AddChild(ChildId - first_child_id, child, true);
                if (watching_children_visible)
                    Utils.RunTask(RefreshRange(false));
            }

            // FIXME: Should only be necessary for items after the one added, and only in LV_VIEW_LIST
            InvalidateChildBounds();
        }

        public void MsaaChildDestroyed(int ChildId)
        {
            // TODO: account for watching_children_visible
            if (ItemCountKnown)
            {
                if (ChildId < 1 || ChildId >= ItemCount + 1)
                {
                    Utils.DebugWriteLine($"got EVENT_OBJECT_DESTROY for {Element} with child id {ChildId} with ItemCount={ItemCount}");
                    return;
                }

                ItemCount--;
                Element.PropertyChanged("win32_item_count", ItemCount);

                if (watching_children && ChildId < first_child_id)
                    first_child_id--;

                if (!watching_children ||
                    ChildId >= Element.RecurseMethodChildCount + first_child_id ||
                    ChildId < first_child_id)
                {
                    // Not in the range of children we're watching
                    return;
                }

                Element.RemoveChild(ChildId - first_child_id, true);
                if (watching_children_visible)
                    Utils.RunTask(RefreshRange(false));
            }

            // FIXME: Should only be necessary for items after the one removed, and only in LV_VIEW_LIST
            InvalidateChildBounds();
        }

        public void MsaaScrolled(int which)
        {
            if (watching_children_visible)
            {
                Utils.RunTask(RefreshRange(false));
            }
            InvalidateChildBounds();
        }
    }
}
