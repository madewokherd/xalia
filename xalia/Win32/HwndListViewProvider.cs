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
    internal class HwndListViewProvider : UiDomProviderBase, IWin32Container, IWin32Styles, IWin32Scrollable
    {
        internal HwndListViewProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;
        public Win32Connection Connection => HwndProvider.Connection;
        public UiDomRoot Root => Element.Root;
        public int Pid => HwndProvider.Pid;

        public bool ItemCountKnown;
        public int ItemCount;
        private bool fetching_item_count;

        private int uniqueid;
        private int first_child_id;

        public int FirstChildId => first_child_id;

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "view", "win32_view" },
            { "control_type", "win32_view" },
            { "role", "win32_view" },
            { "item_count", "win32_item_count" },
        };

        static string[] tracked_properties = { "recurse_method" };

        static string[] style_names =
        {
            "nosortheader",
            "nocolumnheader",
            "noscroll",
            "ownerdata",
            "alignleft",
            "ownerdrawfixed",
            "editlabels",
            "autoarrange",
            "nolabelwrap",
            "shareimagelists",
            "sortdescending",
            "sortascending",
            "showselalways",
            "singlesel"
        };

        static Dictionary<string,int> style_flags;

        static HwndListViewProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x8000 >> i;
            }
        }

        bool CheckingComCtl6;
        TaskCompletionSource<bool> IsComCtl6Completion;
        bool IsComCtl6;
        bool IsComCtl6Known;

        bool watching_view;
        int view_change_count;
        int ViewInt;
        bool ViewKnown;

        bool watching_children;
        bool watching_children_visible;

        Win32RemoteProcessMemory remote_process_memory;

        private static UiDomValue ViewFromInt(int view)
        {
            switch (view)
            {
                // layered_pane is used by GTK icon views in AT-SPI2
                case LV_VIEW_ICON:
                    return new UiDomEnum(new string[] { "icon_view", "iconview", "layered_pane", "layeredpane" });
                case LV_VIEW_DETAILS:
                    return new UiDomEnum(new string[] { "report", "details", "table" });
                case LV_VIEW_SMALLICON:
                    return new UiDomEnum(new string[] { "small_icon", "smallicon", "layered_pane", "layeredpane"  });
                case LV_VIEW_LIST:
                    return new UiDomEnum(new string[] { "list" });
                case LV_VIEW_TILE:
                    return new UiDomEnum(new string[] { "tile", "layered_pane", "layeredpane"  });
            }
            return UiDomUndefined.Instance;
        }

        public int EvaluateView(HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (!IsComCtl6Known)
            {
                depends_on.Add((Element, new IdentifierExpression("win32_is_comctl6")));
                return -1;
            }
            if (!IsComCtl6)
            {
                depends_on.Add((Element, new IdentifierExpression("win32_style")));
                return HwndProvider.Style & LVS_TYPEMASK;
            }
            depends_on.Add((Element, new IdentifierExpression("win32_view")));
            if (ViewKnown)
            {
                return ViewInt;
            }
            return -1;
        }

        public void GetStyleNames(int style, List<string> names)
        {
            switch (style & LVS_TYPEMASK)
            {
                case LVS_ICON:
                    names.Add("icon_view");
                    break;
                case LVS_REPORT:
                    names.Add("report");
                    break;
                case LVS_SMALLICON:
                    names.Add("smallicon");
                    break;
                case LVS_LIST:
                    names.Add("list");
                    break;
            }
            if ((style & LVS_ALIGNMASK) == LVS_ALIGNTOP)
                names.Add("aligntop");
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                if ((HwndProvider.Style & (0x8000 >> i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }

        public override void DumpProperties(UiDomElement element)
        {
            if (IsComCtl6Known)
            {
                Utils.DebugWriteLine($"  win32_is_comctl6: {IsComCtl6}");
                var view = EvaluateView(new HashSet<(UiDomElement, GudlExpression)>());
                if (view != -1)
                    Utils.DebugWriteLine($"  win32_view: {ViewFromInt(view)}");
            }
            if (ItemCountKnown)
                Utils.DebugWriteLine($"  win32_item_count: {ItemCount}");
            base.DumpProperties(element);
        }

        public async Task<bool> IsComCtl6Async()
        {
            if (IsComCtl6Known)
                return IsComCtl6;
            if (IsComCtl6Completion is null)
                IsComCtl6Completion = new TaskCompletionSource<bool>();
            if (!CheckingComCtl6)
            {
                Utils.RunTask(CheckComCtl6());
                CheckingComCtl6 = true;
            }
            return await IsComCtl6Completion.Task;
        }

        public async Task<int> GetViewAsync()
        {
            if (await IsComCtl6Async())
            {
                IntPtr ret = await SendMessageAsync(Hwnd, LVM_GETVIEW, IntPtr.Zero, IntPtr.Zero);
                return Utils.TruncatePtr(ret);
            }
            return HwndProvider.Style & LVS_TYPEMASK;
        }

        public async Task<RECT> GetItemRectAsync(int index, int lvir)
        {
            if (remote_process_memory is null)
                remote_process_memory = Win32RemoteProcessMemory.FromPid(Pid);
            RECT rc = new RECT();
            rc.left = lvir;
            using (var memory = remote_process_memory.WriteAlloc(rc))
            {
                await SendMessageAsync(Hwnd, LVM_GETITEMRECT, (IntPtr)index, new IntPtr((long)memory.Address));
                return memory.Read<RECT>();
            }
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_listview":
                case "is_hwnd_list_view":
                    return UiDomBoolean.True;
                case "win32_is_comctl6":
                    if (IsComCtl6Known)
                        return UiDomBoolean.FromBool(IsComCtl6);
                    return UiDomUndefined.Instance;
                case "win32_view":
                    return ViewFromInt(EvaluateView(depends_on));
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
                case "aligntop":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & LVS_ALIGNMASK) == LVS_ALIGNTOP);
                case "icon_view":
                case "iconview":
                case "report":
                case "details":
                case "table":
                case "smallicon":
                case "small_icon":
                case "list":
                case "tile":
                case "layered_pane":
                case "layeredpane":
                    return element.EvaluateIdentifier("win32_view", element.Root, depends_on).
                        EvaluateIdentifier(identifier, element.Root, depends_on);
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                    {
                        if (element.EvaluateIdentifier("recurse_full", element.Root, depends_on).ToBool())
                            return new UiDomString("win32_list");
                        else
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

        private async Task FetchItemCount()
        {
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
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
            if (ItemCountKnown)
                RefreshChildren();
            else if (!fetching_item_count)
            {
                fetching_item_count = true;
                Utils.RunTask(FetchItemCount());
            }
        }

        private void UnwatchChildren()
        {
            Element.UnsetRecurseMethodProvider(this);
        }

        private void RefreshChildren()
        {
            // TODO: account for watching_children_visible
            SetRecurseMethodRange(1, ItemCount + 1);
        }

        public UiDomElement GetMsaaChild(int ChildId)
        {
            if (ChildId >= first_child_id)
            {
                int index = ChildId - first_child_id;
                if (index < Element.RecurseMethodChildCount)
                    return Element.Children[index];
            }
            return null;
        }

        private string GetUniqueKey()
        {
            return $"listview-{Hwnd:x}-{++uniqueid}";
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
            List<string> keys = new List<string>(end - start);
            for (int i = start; i < end; i++)
            {
                keys.Add(GetChildKey(i));
            }
            first_child_id = start;
            Element.SyncRecurseMethodChildren(keys, (string key) => key,
                CreateChildItem);
        }

        private UiDomElement CreateChildItem(string key)
        {
            var element = new UiDomElement(key, Root);
            element.AddProvider(new HwndListViewItemProvider(this, element), 0);
            return element;
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_is_comctl6":
                        if (!CheckingComCtl6)
                        {
                            Utils.RunTask(CheckComCtl6());
                            CheckingComCtl6 = true;
                        }
                        return true;
                    case "win32_item_count":
                        if (!ItemCountKnown && !fetching_item_count)
                        {
                            fetching_item_count = true;
                            Utils.RunTask(FetchItemCount());
                        }
                        return true;
                    case "win32_view":
                        watching_view = true;
                        Utils.RunTask(WatchView());
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
                    case "win32_view":
                        watching_view = false;
                        return true;
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        private async Task WatchView()
        {
            if (!ViewKnown && await IsComCtl6Async())
            {
                await FetchView();
            }
        }

        private async Task CheckComCtl6()
        {
            // comctl6 will return -1 to indicate error, earlier versions should not recognize the message and return 0
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, LVM_SETVIEW, new IntPtr(-1), IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }

            if (result == IntPtr.Zero)
            {
                IsComCtl6 = false;
                IsComCtl6Known = true;
                Element.PropertyChanged("win32_is_comctl6", "false");
            }
            else
            {
                IsComCtl6 = true;
                IsComCtl6Known = true;
                Element.PropertyChanged("win32_is_comctl6", "true");
            }

            IsComCtl6Completion?.SetResult(IsComCtl6);
            IsComCtl6Completion = null;
        }

        private async Task FetchView()
        {
            int old_change_count = view_change_count;
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, LVM_GETVIEW, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }

            if (old_change_count != view_change_count)
                return;

            int new_view = Utils.TruncatePtr(result);

            if (!ViewKnown || new_view != ViewInt)
            {
                ViewKnown = true;
                ViewInt = new_view;
                Element.PropertyChanged("win32_view", ViewFromInt(ViewInt));
            }
        }

        public IUiDomProvider GetScrollBarProvider(NonclientScrollProvider nonclient)
        {
            return new HwndListViewScrollProvider(this, nonclient);
        }

        public override void NotifyElementRemoved(UiDomElement element)
        {
            if (!(remote_process_memory is null))
            {
                remote_process_memory.Unref();
                remote_process_memory = null;
            }
            base.NotifyElementRemoved(element);
        }

        public void MsaaChildrenReordered()
        {
            view_change_count++;
            if (watching_view)
                Utils.RunTask(FetchView());
            else
                ViewKnown = false;
        }

        public void MsaaChildCreated(int ChildId)
        {
            // TODO: account for watching_children_visible
            if (ItemCountKnown)
            {
                if (ChildId < 1 || ChildId > ItemCount + 1)
                {
                    Utils.DebugWriteLine($"got EVENT_OBJECT_CREATE for {Element} with child id {ChildId} with ItemCount={ItemCount}");
                    return;
                }

                ItemCount++;
                Element.PropertyChanged("win32_item_count", ItemCount);

                if (!watching_children ||
                    ChildId > Element.RecurseMethodChildCount + first_child_id ||
                    ChildId < first_child_id)
                {
                    // Not in the range of children we're watching
                    return;
                }

                if (watching_children)
                {
                    var child = CreateChildItem(GetUniqueKey());
                    Element.AddChild(ChildId - first_child_id, child, true);
                }
            }
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

                if (!watching_children ||
                    ChildId >= Element.RecurseMethodChildCount + first_child_id ||
                    ChildId < first_child_id)
                {
                    // Not in the range of children we're watching
                    return;
                }

                Element.RemoveChild(ChildId - first_child_id, true);
            }
        }
    }
}
