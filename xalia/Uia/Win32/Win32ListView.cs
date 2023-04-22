using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Uia.Win32
{
    internal class Win32ListView : Win32Element
    {
        public Win32ListView(IntPtr hwnd, UiaConnection root) : base("Win32ListView", hwnd, root)
        {
        }

        static Win32ListView()
        {
            string[] aliases = {
                "view", "win32_view",
                "top_index", "win32_top_index",
                "item_count", "win32_item_count",
                "count_per_page", "win32_count_per_page",
                "header", "win32_header",
                "header_hwnd", "win32_header_hwnd",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i = 0; i < aliases.Length; i += 2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
        }

        static Dictionary<string, string> property_aliases;

        private Win32RemoteProcessMemory remote_process_memory;

        bool CheckingComCtl6;
        bool IsComCtl6;
        bool IsComCtl6Known;

        int ViewInt;
        bool ViewKnown;

        bool watching_children;
        private IDisposable HeaderHwndWatcher;
        private IDisposable ChildItemStartWatcher;
        private IDisposable ChildItemCountWatcher;
        bool HeaderHwndKnown;
        bool CheckingHeaderHwnd;
        IntPtr HeaderHwnd;
        Win32Element Header;
        private bool refreshing_children;
        private bool TopIndexKnown;
        private int TopIndex;
        private bool ItemCountKnown;
        private int ItemCount;
        private bool CountPerPageKnown;
        private int CountPerPage;
        private int child_item_start;
        private int child_item_count;

        protected override void SetAlive(bool value)
        {
            if (!value)
            {
                if (!(HeaderHwndWatcher is null))
                {
                    HeaderHwndWatcher.Dispose();
                    HeaderHwndWatcher = null;
                }
                if (!(ChildItemStartWatcher is null))
                {
                    ChildItemStartWatcher.Dispose();
                    ChildItemStartWatcher = null;
                }
                if (!(ChildItemCountWatcher is null))
                {
                    ChildItemCountWatcher.Dispose();
                    ChildItemCountWatcher = null;
                }
                if (!(remote_process_memory is null))
                {
                    remote_process_memory.Unref();
                    remote_process_memory = null;
                }
            }
            base.SetAlive(value);
        }

        private static UiDomValue ViewFromInt(int view)
        {
            switch (view)
            {
                case LV_VIEW_ICON:
                    return new UiDomEnum(new string[] { "icon" });
                case LV_VIEW_DETAILS:
                    return new UiDomEnum(new string[] { "report", "details", "table" });
                case LV_VIEW_SMALLICON:
                    return new UiDomEnum(new string[] { "small_icon", "smallicon" });
                case LV_VIEW_LIST:
                    return new UiDomEnum(new string[] { "list" });
                case LV_VIEW_TILE:
                    return new UiDomEnum(new string[] { "tile" });
            }
            return UiDomUndefined.Instance;
        }

        private static UiDomValue ViewFromStyle(int style)
        {
            return ViewFromInt(style & LVS_TYPEMASK);
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (property_aliases.TryGetValue(id, out string aliased))
            {
                var value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;
                id = aliased;
            }

            switch (id)
            {
                case "is_win32_list_view":
                case "is_win32_listview":
                    return UiDomBoolean.True;
                case "icon":
                case "report":
                case "details":
                case "table":
                case "smallicon":
                case "small_icon":
                case "list":
                case "tile":
                    return EvaluateIdentifierCore("win32_view", Root, depends_on).EvaluateIdentifier(id, Root, depends_on);
                case "win32_is_comctl6":
                    if (IsComCtl6Known)
                        return UiDomBoolean.FromBool(IsComCtl6);
                    return UiDomUndefined.Instance;
                case "role":
                case "control_type":
                case "win32_view":
                    if (!IsComCtl6Known)
                    {
                        depends_on.Add((this, new IdentifierExpression("win32_is_comctl6")));
                        return UiDomUndefined.Instance;
                    }
                    if (!IsComCtl6)
                    {
                        depends_on.Add((this, new IdentifierExpression("win32_style")));
                        if (WindowStyleKnown)
                        {
                            return ViewFromStyle(WindowStyle);
                        }
                        return UiDomUndefined.Instance;
                    }
                    depends_on.Add((this, new IdentifierExpression("win32_view")));
                    if (ViewKnown)
                    {
                        return ViewFromInt(ViewInt);
                    }
                    return UiDomUndefined.Instance;
                case "win32_header_hwnd":
                    EvaluateIdentifierCore("win32_view", root, depends_on);
                    if (HasHeader())
                    {
                        if (HeaderHwndKnown)
                            return new UiDomInt(HeaderHwnd.ToInt32());
                        depends_on.Add((this, new IdentifierExpression("win32_header_hwnd")));
                    }
                    if (ViewAsInt == -1)
                    {
                        return UiDomUndefined.Instance;
                    }
                    return UiDomBoolean.False;
                case "win32_header":
                    depends_on.Add((this, new IdentifierExpression("win32_header")));
                    if (!(Header is null))
                        return Header;
                    return UiDomUndefined.Instance;
                case "win32_top_index":
                    depends_on.Add((this, new IdentifierExpression("win32_view")));
                    if (!HasTopIndex())
                        return UiDomUndefined.Instance;
                    depends_on.Add((this, new IdentifierExpression("win32_top_index")));
                    if (TopIndexKnown)
                        return new UiDomInt(TopIndex);
                    return UiDomUndefined.Instance;
                case "win32_item_count":
                    depends_on.Add((this, new IdentifierExpression("win32_item_count")));
                    if (ItemCountKnown)
                        return new UiDomInt(ItemCount);
                    return UiDomUndefined.Instance;
                case "win32_count_per_page":
                    depends_on.Add((this, new IdentifierExpression("win32_count_per_page")));
                    if (CountPerPageKnown)
                        return new UiDomInt(CountPerPage);
                    return UiDomUndefined.Instance;
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        private bool HasHeader()
        {
            return ViewAsInt == LV_VIEW_DETAILS;
        }

        private bool HasTopIndex()
        {
            switch (ViewAsInt)
            {
                case LV_VIEW_ICON:
                case LV_VIEW_SMALLICON:
                case -1:
                    return false;
                default:
                    return true;
            }
        }

        protected override void DumpProperties()
        {
            if (IsComCtl6Known)
            {
                Utils.DebugWriteLine($"  win32_is_comctl6: {IsComCtl6}");
                if (IsComCtl6)
                {
                    if (ViewKnown)
                        Utils.DebugWriteLine($"  win32_view: {ViewFromInt(ViewInt)}");
                }
                else
                {
                    if (WindowStyleKnown)
                        Utils.DebugWriteLine($"  win32_view: {ViewFromStyle(WindowStyle)}");
                }
            }
            if (HasHeader())
            {
                if (HeaderHwndKnown)
                    Utils.DebugWriteLine($"  win32_header_hwnd: {HeaderHwnd}");
                if (!(Header is null))
                    Utils.DebugWriteLine($"  win32_header: {Header}");
            }
            if (HasTopIndex() && TopIndexKnown)
                Utils.DebugWriteLine($"  win32_top_index: {TopIndex}");
            if (ItemCountKnown)
                Utils.DebugWriteLine($"  win32_item_count: {ItemCount}");
            if (CountPerPageKnown)
                Utils.DebugWriteLine($"  win32_count_per_page: {CountPerPage}");
            base.DumpProperties();
        }

        protected override void WatchProperty(GudlExpression expression)
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
                        break;
                    case "win32_header_hwnd":
                        if (!CheckingHeaderHwnd)
                        {
                            Utils.RunTask(CheckHeaderHwnd());
                            CheckingHeaderHwnd = true;
                        }
                        break;
                    case "win32_view":
                        PollProperty(expression, RefreshView, 200);
                        break;
                    case "win32_top_index":
                        PollProperty(expression, RefreshTopIndex, 200);
                        break;
                    case "win32_item_count":
                        PollProperty(expression, RefreshItemCount, 200);
                        break;
                    case "win32_count_per_page":
                        PollProperty(expression, RefreshCountPerPage, 200);
                        break;
                }
            }
            base.WatchProperty(expression);
        }

        private async Task CheckHeaderHwnd()
        {
            var result = await SendMessageAsync(Hwnd, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);

            if (GetAncestor(result, GA_PARENT) != Hwnd)
                result = IntPtr.Zero;

            CheckingHeaderHwnd = false;
            HeaderHwndKnown = true;
            HeaderHwnd = result;

            PropertyChanged("win32_header_hwnd");
        }

        protected override void UnwatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_view":
                        EndPollProperty(expression);
                        ViewKnown = false;
                        break;
                    case "win32_top_index":
                        EndPollProperty(expression);
                        TopIndexKnown = false;
                        break;
                    case "win32_item_count":
                        EndPollProperty(expression);
                        ItemCountKnown = false;
                        break;
                    case "win32_count_per_page":
                        EndPollProperty(expression);
                        CountPerPageKnown = false;
                        break;
                }
            }
            base.UnwatchProperty(expression);
        }

        private async Task CheckComCtl6()
        {
            // comctl6 will return -1 to indicate error, earlier versions should not recognize the message and return 0
            var result = await SendMessageAsync(Hwnd, LVM_SETVIEW, new IntPtr(-1), IntPtr.Zero);

            if (result == IntPtr.Zero)
            {
                IsComCtl6 = false;
                IsComCtl6Known = true;
                PropertyChanged("win32_is_comctl6", "false");
            }
            else if (result == new IntPtr(-1))
            {
                IsComCtl6 = true;
                IsComCtl6Known = true;
                PropertyChanged("win32_is_comctl6", "true");
            }
        }

        private async Task RefreshView()
        {
            var result = await SendMessageAsync(Hwnd, LVM_GETVIEW, IntPtr.Zero, IntPtr.Zero);

            if (!ViewKnown || ViewInt != result.ToInt32())
            {
                ViewKnown = true;
                ViewInt = result.ToInt32();
                PropertyChanged("win32_view", ViewFromInt(ViewInt));
            }
        }

        private async Task RefreshTopIndex()
        {
            var result = await SendMessageAsync(Hwnd, LVM_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero);

            if (!TopIndexKnown || TopIndex != result.ToInt32())
            {
                TopIndexKnown = true;
                TopIndex = result.ToInt32();
                PropertyChanged("win32_top_index", TopIndex);
            }
        }

        private async Task RefreshItemCount()
        {
            var result = await SendMessageAsync(Hwnd, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);

            if (!ItemCountKnown || ItemCount != result.ToInt32())
            {
                ItemCountKnown = true;
                ItemCount = result.ToInt32();
                PropertyChanged("win32_item_count", ItemCount);
            }
        }

        private async Task RefreshCountPerPage()
        {
            var result = await SendMessageAsync(Hwnd, LVM_GETCOUNTPERPAGE, IntPtr.Zero, IntPtr.Zero);

            if (!CountPerPageKnown || CountPerPage != result.ToInt32())
            {
                CountPerPageKnown = true;
                CountPerPage = result.ToInt32();
                PropertyChanged("win32_count_per_page", CountPerPage);
            }
        }

        protected override void PropertiesChanged(HashSet<GudlExpression> changed_properties)
        {
            if (changed_properties.Contains(new IdentifierExpression("recurse")) ||
                changed_properties.Contains(new IdentifierExpression("win32_header_hwnd")) ||
                changed_properties.Contains(new IdentifierExpression("win32_top_index")))
            {
                QueueRefreshChildren(this, new IdentifierExpression("recurse"));
            }
            base.PropertiesChanged(changed_properties);
        }

        public int ViewAsInt
        {
            get
            {
                if (IsComCtl6Known)
                {
                    if (IsComCtl6)
                    {
                        if (ViewKnown)
                            return ViewInt;
                    }
                    else
                    {
                        if (WindowStyleKnown)
                            return WindowStyle & LVS_TYPEMASK;
                    }
                }
                return -1;
            }
        }

        private void RefreshChildren()
        {
            if (GetDeclaration("recurse").ToBool())
            {
                if (!watching_children)
                {
                    watching_children = true;
                    HeaderHwndWatcher = NotifyPropertyChanged(new IdentifierExpression("win32_header_hwnd"), QueueRefreshChildren);
                    ChildItemStartWatcher = NotifyPropertyChanged(new IdentifierExpression("win32_child_item_start"), QueueRefreshChildren);
                    ChildItemCountWatcher = NotifyPropertyChanged(new IdentifierExpression("win32_child_item_count"), QueueRefreshChildren);
                    UseVirtualScrollBars = true;
                }
                if (!HasHeader() || HeaderHwnd == IntPtr.Zero)
                {
                    if (!(Header is null))
                    {
                        RemoveChild(Children.IndexOf(Header));
                        Header = null;
                        PropertyChanged("win32_header", "undefined");
                    }
                }
                else if (Header is null || HeaderHwnd != Header.Hwnd)
                {
                    if (!(Header is null))
                        RemoveChild(Children.IndexOf(Header));
                    Header = new Win32Element("Win32Element", HeaderHwnd, Root);
                    AddChild(Children.Count, Header);
                    PropertyChanged("win32_header", Header);
                }
                if (GetDeclaration("win32_child_item_start") is UiDomInt start &&
                    GetDeclaration("win32_child_item_count") is UiDomInt count &&
                    (start.Value != child_item_start || count.Value != child_item_count))
                {
                    int old_start = child_item_start;
                    int old_count = child_item_count;
                    int old_end = child_item_start + child_item_count;
                    child_item_start = start.Value;
                    child_item_count = count.Value;
                    int child_item_end = child_item_start + child_item_count;

                    if (child_item_start >= old_end || old_start >= child_item_end)
                    {
                        // Disjoint sets
                        RemoveChildRange(0, old_count);
                        AddChildItemRange(0, child_item_count, child_item_start);
                    }
                    else
                    {
                        if (child_item_end > old_end)
                        {
                            AddChildItemRange(old_count, child_item_end - old_end, old_end);
                        }
                        else if (child_item_end < old_end)
                        {
                            RemoveChildRange(child_item_end - old_start, old_end - child_item_end);
                        }
                        if (child_item_start > old_start)
                        {
                            RemoveChildRange(0, child_item_start - old_start);
                        }
                        else if (child_item_start < old_start)
                        {
                            AddChildItemRange(0, old_start - child_item_start, child_item_start);
                        }
                    }
                }
            }
            else
            {
                if (watching_children)
                {
                    watching_children = false;
                    UseVirtualScrollBars = false;
                    if (!(Header is null))
                    {
                        RemoveChild(Children.IndexOf(Header));
                        Header = null;
                        PropertyChanged("win32_header", "undefined");
                    }
                    if (!(HeaderHwndWatcher is null))
                    {
                        HeaderHwndWatcher.Dispose();
                        HeaderHwndWatcher = null;
                    }
                    if (!(ChildItemStartWatcher is null))
                    {
                        ChildItemStartWatcher.Dispose();
                        ChildItemStartWatcher = null;
                    }
                    if (!(ChildItemCountWatcher is null))
                    {
                        ChildItemCountWatcher.Dispose();
                        ChildItemCountWatcher = null;
                    }
                    while (Children.Count != 0)
                    {
                        RemoveChild(Children.Count - 1);
                    }
                    child_item_start = 0;
                    child_item_count = 0;
                }
            }
            refreshing_children = false;
        }

        private void RemoveChildRange(int child_start, int child_count)
        {
            for (int i = child_count - 1; i >= 0; i--)
            {
                RemoveChild(child_start + i);
            }
        }

        private void AddChildItemRange(int child_start, int child_count, int item_start)
        {
            for (int i = 0; i < child_count; i++)
            {
                AddChild(child_start + i, new Win32ListViewItem(this, item_start + i));
            }
        }

        private void QueueRefreshChildren(UiDomElement element, GudlExpression property)
        {
            if (!refreshing_children)
            {
                refreshing_children = true;
                Utils.RunIdle(RefreshChildren);
            }
        }

        double yremainder;

        public override Task<double> GetVScrollMinimumIncrement()
        {
            return Task.FromResult(1.0);
        }

        public override async Task OffsetVScroll(double ofs)
        {
            switch (ViewAsInt)
            {
                case LV_VIEW_DETAILS:
                    {
                        int pos = (int)await SendMessageAsync(Hwnd, LVM_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero);

                        int total_items = (int)await SendMessageAsync(Hwnd, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);

                        int count_per_page = (int)await SendMessageAsync(Hwnd, LVM_GETCOUNTPERPAGE, IntPtr.Zero, IntPtr.Zero);

                        int max_pos = total_items - count_per_page;

                        double new_pos = pos + yremainder + ofs;

                        int pos_ofs = (int)Math.Truncate(new_pos - pos);

                        int new_pos_int = pos + pos_ofs;

                        if (new_pos_int != pos)
                        {
                            if (new_pos_int < 0)
                                new_pos = new_pos_int = 0;
                            else if (new_pos_int > max_pos)
                                new_pos = new_pos_int = max_pos;
                        }

                        if (new_pos_int != pos)
                        {
                            if (remote_process_memory is null)
                                remote_process_memory = Win32RemoteProcessMemory.FromPid(Pid);
                            RECT rc = new RECT();
                            rc.left = LVIR_SELECTBOUNDS;
                            IntPtr result;
                            using (var memory = remote_process_memory.WriteAlloc(rc))
                            {
                                result = await SendMessageAsync(Hwnd, LVM_GETITEMRECT, (IntPtr)pos, new IntPtr((long)memory.Address));
                                rc = memory.Read<RECT>();
                            }

                            await SendMessageAsync(Hwnd, LVM_SCROLL, IntPtr.Zero, new IntPtr((rc.bottom - rc.top) * pos_ofs));
                        }

                        yremainder = new_pos - new_pos_int;
                    }
                    break;
                default:
                    // FIXME
                    break;
            }
        }
    }
}
