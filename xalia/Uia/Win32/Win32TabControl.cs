using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Uia.Win32
{
    internal class Win32TabControl : Win32Element
    {
        public Win32TabControl(IntPtr hwnd, UiaConnection root) : base("Win32TabControl", hwnd, root)
        {
        }

        static Win32TabControl()
        {
            string[] aliases = {
                "selection_index", "win32_selection_index",
                "item_count", "win32_item_count",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i = 0; i < aliases.Length; i += 2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
        }

        static Dictionary<string, string> property_aliases;
        private static readonly UiDomValue role = new UiDomEnum(new[] { "tab", "page_tab_list", "pagetablist" });

        private Win32RemoteProcessMemory remote_process_memory;
        public bool SelectionIndexKnown;
        public int SelectionIndex;
        private bool ItemCountKnown;
        private int ItemCount;
        private bool refreshing_children;
        private bool watching_children;
        private IDisposable ItemCountWatcher;
        private int num_child_items;

        protected override void SetAlive(bool value)
        {
            if (!value)
            {
                if (!(ItemCountWatcher is null))
                {
                    ItemCountWatcher.Dispose();
                    ItemCountWatcher = null;
                }
                if (!(remote_process_memory is null))
                {
                    remote_process_memory.Unref();
                    remote_process_memory = null;
                }
            }
            base.SetAlive(value);
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
                case "is_win32_tab_control":
                case "is_win32_tabcontrol":
                case "tab":
                case "page_tab_list":
                case "pagetablist":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "win32_selection_index":
                    depends_on.Add((this, new IdentifierExpression("win32_selection_index")));
                    if (SelectionIndexKnown)
                        return new UiDomInt(SelectionIndex);
                    return UiDomUndefined.Instance;
                case "win32_item_count":
                    depends_on.Add((this, new IdentifierExpression("win32_item_count")));
                    if (ItemCountKnown)
                        return new UiDomInt(ItemCount);
                    return UiDomUndefined.Instance;
                default:
                    break;
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override void DumpProperties()
        {
            if (SelectionIndexKnown)
                Utils.DebugWriteLine($"  win32_selection_index: {SelectionIndex}");
            if (ItemCountKnown)
                Utils.DebugWriteLine($"  win32_item_count: {ItemCount}");
            base.DumpProperties();
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_selection_index":
                        PollProperty(expression, RefreshSelectionIndex, 200);
                        break;
                    case "win32_item_count":
                        PollProperty(expression, RefreshItemCount, 200);
                        break;
                }
            }
            base.WatchProperty(expression);
        }

        protected override void UnwatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_selection_index":
                        EndPollProperty(expression);
                        SelectionIndexKnown = false;
                        break;
                    case "win32_item_count":
                        EndPollProperty(expression);
                        ItemCountKnown = false;
                        break;
                }
            }
            base.UnwatchProperty(expression);
        }

        private async Task RefreshSelectionIndex()
        {
            IntPtr index = await SendMessageAsync(Hwnd, TCM_GETCURSEL, IntPtr.Zero, IntPtr.Zero);
            int i = index.ToInt32();

            bool known = i >= 0;

            if (known != SelectionIndexKnown || i != SelectionIndex)
            {
                if (known)
                {
                    SelectionIndexKnown = true;
                    SelectionIndex = i;
                    PropertyChanged("win32_selection_index", i);
                }
                else
                {
                    SelectionIndexKnown = false;
                    PropertyChanged("win32_selection_index", "undefined");
                }
            }
        }

        private async Task RefreshItemCount()
        {
            IntPtr index = await SendMessageAsync(Hwnd, TCM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            int i = index.ToInt32();

            bool known = i >= 0;

            if (known != ItemCountKnown || i != ItemCount)
            {
                if (known)
                {
                    ItemCountKnown = true;
                    ItemCount = i;
                    PropertyChanged("win32_item_count", i);
                }
                else
                {
                    ItemCountKnown = false;
                    PropertyChanged("win32_item_count", "undefined");
                }
            }
        }

        protected override void PropertiesChanged(HashSet<GudlExpression> changed_properties)
        {
            if (changed_properties.Contains(new IdentifierExpression("recurse")) ||
                changed_properties.Contains(new IdentifierExpression("win32_item_count")))
            {
                QueueRefreshChildren(this, new IdentifierExpression("recurse"));
            }
            base.PropertiesChanged(changed_properties);
        }

        private void QueueRefreshChildren(UiDomElement element, GudlExpression identifierExpression)
        {
            if (!refreshing_children)
            {
                refreshing_children = true;
                Utils.RunIdle(RefreshChildren);
            }
        }

        private void RefreshChildren()
        {
            if (GetDeclaration("recurse").ToBool())
            {
                if (!watching_children)
                {
                    watching_children = true;
                    ItemCountWatcher = NotifyPropertyChanged(new IdentifierExpression("win32_item_count"), QueueRefreshChildren);
                }
                if (ItemCountKnown && num_child_items != ItemCount)
                {
                    if (ItemCount > num_child_items)
                        AddChildRange(num_child_items, ItemCount);
                    else
                        RemoveChildRange(ItemCount, num_child_items);
                    num_child_items = ItemCount;
                }
            }
            else
            {
                if (watching_children)
                {
                    watching_children = false;
                    if (!(ItemCountWatcher is null))
                    {
                        ItemCountWatcher.Dispose();
                        ItemCountWatcher = null;
                    }
                    RemoveChildRange(0, num_child_items);
                    num_child_items = 0;
                }
            }
            refreshing_children = false;
        }

        private void AddChildRange(int start_index, int end_index)
        {
            for (int i=start_index; i<end_index; i++)
            {
                AddChild(i, new Win32TabControlItem(this, i));
            }
        }

        private void RemoveChildRange(int start_index, int end_index)
        {
            for (int i=end_index-1; i>=start_index; i--)
            {
                RemoveChild(i);
            }
        }
    }
}
