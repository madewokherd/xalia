using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    class HwndTreeViewItemProvider : UiDomProviderBase
    {
        public HwndTreeViewItemProvider(HwndTreeViewProvider view, UiDomElement element, IntPtr hItem)
        {
            if (view == null)
                view = this as HwndTreeViewProvider;
            View = view;
            Element = element;
            HItem = hItem;

            view.tree_items.Add(hItem, this);
        }

        public HwndTreeViewProvider View { get; }
        public IntPtr HItem { get; }
        public int ChildId { get; }
        public UiDomElement Element { get; }
        public HwndProvider HwndProvider => View.HwndProvider;
        public IntPtr Hwnd => View.Hwnd;
        public UiDomRoot Root => View.Root;

        static UiDomEnum role = new UiDomEnum(new string[] { "tree_item", "treeitem", "outlineitem" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "state", "win32_tree_item_state" },
        };

        static string[] state_names =
        {
            "focused",
            "selected",
            "cut",
            "drophilited",
            "bold",
            "expanded",
            "expandedonce",
            "expandpartial",
        };

        static Dictionary<string,int> state_flags;

        static HwndTreeViewItemProvider()
        {
            state_flags = new Dictionary<string, int>();
            for (int i=0; i<state_names.Length; i++)
            {
                if (state_names[i] is null)
                    continue;
                state_flags[state_names[i]] = 1 << i;
            }
        }

        bool watching_children;
        bool watching_children_visible;

        int item_state;
        int item_children;
        bool item_known;
        bool watching_item;

        public override void DumpProperties(UiDomElement element)
        {
            if (!(this is HwndTreeViewProvider))
            {
                if (item_known)
                {
                    Utils.DebugWriteLine($"  win32_tree_item_state: {item_state.ToString("x", CultureInfo.InvariantCulture)}");
                    Utils.DebugWriteLine($"  win32_tree_item_state_names: {NamesFromState(item_state)}");
                    Utils.DebugWriteLine($"  win32_tree_item_children: {item_children}");
                }
                HwndProvider.ChildDumpProperties();
            }
            base.DumpProperties(element);
        }

        private UiDomValue NamesFromState(int item_state)
        {
            List<string> result = new List<string>();
            for (int i=0; i < state_names.Length; i++)
            {
                if ((item_state & (1 << i)) != 0)
                    result.Add(state_names[i]);
            }
            if ((View.HwndProvider.Style & TVS_CHECKBOXES) != 0 ||
                (View.ExtendedStyleKnown && (View.ExtendedStyle & (TVS_EX_PARTIALCHECKBOXES|TVS_EX_EXCLUSIONCHECKBOXES|TVS_EX_DIMMEDCHECKBOXES)) != 0))
            {
                int state_image = (item_state >> 12) & 0xf;
                if (state_image == 1)
                {
                    result.Add("unchecked");
                }
                else if (state_image == 2)
                {
                    result.Add("checked");
                }
                else if (state_image > 2)
                {
                    state_image -= 2;
                    // FIXME: MSDN doesn't make it clear what is the correct order for these
                    if ((View.ExtendedStyle & TVS_EX_PARTIALCHECKBOXES) != 0)
                    {
                        if (state_image == 0)
                            result.Add("partial");
                        state_image--;
                    }
                    if ((View.ExtendedStyle & TVS_EX_EXCLUSIONCHECKBOXES) != 0)
                    {
                        if (state_image == 0)
                            result.Add("exclusion");
                        state_image--;
                    }
                    if ((View.ExtendedStyle & TVS_EX_DIMMEDCHECKBOXES) != 0)
                    {
                        if (state_image == 0)
                            result.Add("dimmed");
                        state_image--;
                    }
                }
            }
            if (result.Count != 0)
                return new UiDomEnum(result.ToArray());
            return UiDomUndefined.Instance;
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (!(this is HwndTreeViewProvider))
            {
                switch (identifier)
                {
                    case "is_hwnd_treeview_item":
                    case "is_hwnd_tree_view_item":
                    case "is_hwnd_subelement":
                        return UiDomBoolean.True;
                    case "win32_tree_item_state":
                        depends_on.Add((Element, new IdentifierExpression("win32_tree_item")));
                        if (item_known)
                            return new UiDomInt(item_state);
                        break;
                    case "win32_tree_item_state_names":
                        depends_on.Add((Element, new IdentifierExpression("win32_tree_item")));
                        depends_on.Add((View.Element, new IdentifierExpression("win32_extended_treeview_style")));
                        if (item_known)
                            return NamesFromState(item_state);
                        break;
                    case "win32_tree_item_children":
                        depends_on.Add((Element, new IdentifierExpression("win32_tree_item")));
                        if (item_known)
                            return new UiDomInt(item_children);
                        break;
                }
                return HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (identifier == "recurse_method")
            {
                if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                {
                    if (element.EvaluateIdentifier("recurse_full", element.Root, depends_on).ToBool())
                        return new UiDomString("win32_tree");
                    else
                        return new UiDomString("win32_tree_visible");
                }
            }
            if (!(this is HwndTreeViewProvider))
            {
                switch (identifier)
                {
                    case "tree_item":
                    case "treeitem":
                    case "outlineitem":
                    case "enabled":
                    case "visible":
                        if (!(this is HwndTreeViewProvider))
                            return UiDomBoolean.True;
                        break;
                    case "role":
                    case "control_type":
                        return role;
                    case "unchecked":
                    case "checked":
                    case "dimmed":
                    case "partial":
                    case "exclusion":
                        depends_on.Add((Element, new IdentifierExpression("win32_tree_item")));
                        depends_on.Add((View.Element, new IdentifierExpression("win32_extended_treeview_style")));
                        if (item_known)
                            return NamesFromState(item_state).EvaluateIdentifier(identifier, Root, depends_on);
                        break;
                    case "collapsed":
                        depends_on.Add((Element, new IdentifierExpression("win32_tree_item")));
                        if (item_known)
                            return UiDomBoolean.FromBool((item_state & TVIS_EXPANDED) == 0 && item_children != 0);
                        break;
                    case "expandable":
                        depends_on.Add((Element, new IdentifierExpression("win32_tree_item")));
                        if (item_known)
                            return UiDomBoolean.FromBool(item_children != 0);
                        break;
                }
                if (state_flags.TryGetValue(identifier, out var flag))
                {
                    depends_on.Add((Element, new IdentifierExpression("win32_tree_item")));
                    if (item_known)
                        return UiDomBoolean.FromBool((item_state & flag) != 0);
                }
                if (property_aliases.TryGetValue(identifier, out var alias))
                {
                    return Element.EvaluateIdentifier(alias, Root, depends_on);
                }
                return HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_tree_item":
                        watching_item = true;
                        if (!item_known)
                            Utils.RunTask(FetchItem());
                        return true;
                }
            }
            return base.WatchProperty(element, expression);
        }

        private async Task FetchItem()
        {
            var process_memory = View.RemoteProcessMemory;
            if (process_memory == null)
                return;

            int new_state;
            int new_children;

            if (process_memory.Is64Bit())
            {
                TVITEMEXW64 tvitem = default;

                tvitem.mask = TVIF_STATE | TVIF_CHILDREN;
                tvitem.hItem = unchecked((ulong)(long)HItem);
                tvitem.stateMask = 0xffff;

                using (var item_memory = process_memory.WriteAlloc(tvitem))
                {
                    await SendMessageAsync(Hwnd, TVM_GETITEMW, IntPtr.Zero, new IntPtr(unchecked((long)item_memory.Address)));

                    tvitem = item_memory.Read<TVITEMEXW64>();
                    new_state = tvitem.state;
                    new_children = tvitem.cChildren;
                }
            }
            else
            {
                TVITEMEXW32 tvitem = default;

                tvitem.mask = TVIF_STATE | TVIF_CHILDREN;
                tvitem.hItem = unchecked((uint)(long)HItem);
                tvitem.stateMask = 0xffff;

                using (var item_memory = process_memory.WriteAlloc(tvitem))
                {
                    await SendMessageAsync(Hwnd, TVM_GETITEMW, IntPtr.Zero, new IntPtr(unchecked((long)item_memory.Address)));

                    tvitem = item_memory.Read<TVITEMEXW32>();
                    new_state = tvitem.state;
                    new_children = tvitem.cChildren;
                }
            }

            bool item_was_known = item_known;
            bool any_changed = !item_known || (new_state != item_state) || (new_children != item_children);
            if (any_changed)
            {
                item_known = true;
                if (Element.MatchesDebugCondition())
                {
                    if (!item_was_known || new_state != item_state)
                    {
                        Utils.DebugWriteLine($"{Element}.win32_tree_item_state: {item_state.ToString("x", CultureInfo.InvariantCulture)}");
                        Utils.DebugWriteLine($"{Element}.win32_tree_item_state_names: {NamesFromState(item_state)}");
                    }
                    if (!item_was_known || new_children != item_children)
                    {
                        Utils.DebugWriteLine($"{Element}.win32_tree_item_children: {item_children}");
                    }
                }
                item_state = new_state;
                item_children = new_children;
                Element.PropertyChanged("win32_tree_item");
            }
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_tree_item":
                        watching_item = false;
                        return true;
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        static string[] tracked_properties =
        {
            "recurse_method"
        };

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
                    case "win32_tree":
                        if (watching_children && !watching_children_visible)
                            break;
                        watching_children = true;
                        watching_children_visible = false;
                        WatchChildren();
                        break;
                    case "win32_tree_visible":
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
            RefreshChildren();
        }

        private void UnwatchChildren()
        {
            Element.UnsetRecurseMethodProvider(this);
        }

        private void RefreshChildren()
        {
            Utils.RunTask(RefreshChildrenAsync());
        }

        private async Task RefreshChildrenAsync()
        {
            if (!watching_children)
                return;

            var child = await SendMessageAsync(Hwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_CHILD), HItem);
            var children = new List<IntPtr>();

            if (!watching_children)
                return;

            while (child != IntPtr.Zero)
            {
                children.Add(child);
                child = await SendMessageAsync(Hwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_NEXT), child);

                if (!watching_children)
                    return;
            }

            Element.SyncRecurseMethodChildren(children, GetChildId, CreateChild);
        }

        private UiDomElement CreateChild(IntPtr item)
        {
            var result = new UiDomElement(GetChildId(item), Root);

            result.AddProvider(new HwndTreeViewItemProvider(View, result, item));

            return result;
        }

        protected string GetChildId(IntPtr hitem)
        {
            return $"treeitem-{Hwnd.ToInt64().ToString("x", CultureInfo.InvariantCulture)}-{hitem.ToInt64().ToString("x", CultureInfo.InvariantCulture)}";
        }

        public async Task MsaaStateChange()
        {
            if (watching_item)
            {
                await FetchItem();
            }
            else
                item_known = false;
        }

        public override void NotifyElementRemoved(UiDomElement element)
        {
            View.tree_items.Remove(HItem);
            base.NotifyElementRemoved(element);
        }
    }
}