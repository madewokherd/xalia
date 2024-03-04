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
    internal class HwndListViewProvider : HwndItemListProvider, IWin32Styles, IWin32Scrollable
    {
        internal HwndListViewProvider(HwndProvider hwndProvider) : base(hwndProvider) { }

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "view", "win32_view" },
            { "control_type", "win32_view" },
            { "role", "win32_view" },
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

        protected override async Task<int> FetchItemCount()
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
                return 0;
            }
            return Utils.TruncatePtr(result);
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
            else
                Utils.RunTask(DoFetchItemCount());
        }

        private void UnwatchChildren()
        {
            Element.UnsetRecurseMethodProvider(this);
        }

        protected override void ItemCountChanged(int newCount)
        {
            RefreshChildren();
            base.ItemCountChanged(newCount);
        }

        private void RefreshChildren()
        {
            // TODO: account for watching_children_visible
            SetRecurseMethodRange(0, ItemCount);
        }

        private void SetRecurseMethodRange(int start, int end)
        {
            Element.SyncRecurseMethodChildren(new RangeList(start, end), (int key) => Connection.GetElementName(Hwnd, OBJID_CLIENT, key+1),
                CreateChildItem);
        }

        private UiDomElement CreateChildItem(int key)
        {
            int childId = key + 1;
            var element = Connection.CreateElement(Hwnd, OBJID_CLIENT, childId);
            element.AddProvider(new HwndListViewItemProvider(this, childId), 0);
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

        internal void MsaaReorder()
        {
            view_change_count++;
            if (watching_view)
                Utils.RunTask(FetchView());
            else
                ViewKnown = false;
        }
    }
}
