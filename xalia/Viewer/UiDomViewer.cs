using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Viewer
{
    public partial class UiDomViewer : Form
    {
        public UiDomViewer(SynchronizationContext main_context)
        {
            InitializeComponent();
            MainContext = main_context;
        }

        UiDomViewerProvider Provider { get; set; }
        public SynchronizationContext MainContext { get; }

        public class TreeUpdate { }

        public class ChildrenUpdate : TreeUpdate
        {
            public string parent;
            public (string, string)[] children;
        }

        public class DescUpdate : TreeUpdate
        {
            public string element;
            public string desc;
        }

        public ConcurrentQueue<TreeUpdate> TreeUpdates = new ConcurrentQueue<TreeUpdate>();

        public class DataUpdate
        {
            public string element;
            public GudlExpression expression;
            public UiDomValue value;
        }

        public ConcurrentQueue<DataUpdate> PropertyUpdates = new ConcurrentQueue<DataUpdate>();

        private Dictionary<string, TreeNode> element_nodes = new Dictionary<string, TreeNode>();

        internal static void ThreadProc(SynchronizationContext main_context, UiDomRoot root)
        {
            var form = new UiDomViewer(main_context);
            form.Provider = new UiDomViewerProvider(form, root, SynchronizationContext.Current);
            main_context.Send((object state) =>
            {
                form.Provider.DoMainThreadSetup();
            }, null);
            form.Show();
            Application.Run();
        }

        internal void QueuesUpdated(object state)
        {
            if (PropertyUpdates.Count != 0)
            {
                var element = element_tree.SelectedNode?.Name;
                while (PropertyUpdates.TryDequeue(out var update)) {
                    if (element != update.element)
                        continue;
                    var expr = update.expression;
                    DataGridViewRow row = null;
                    foreach (DataGridViewRow r in properties_view.Rows)
                    {
                        if (r.Cells[0].Value == expr)
                        {
                            row = r;
                            break;
                        }
                    }
                    if (row is null)
                    {
                        row = properties_view.Rows[properties_view.Rows.Add()];
                        row.Cells[0].Value = expr;
                    }
                    row.Cells[1].Value = update.value;
                    if (update.value is UiDomElement)
                        row.Cells[2].Value = "Inspect";
                    else if (update.value is UiDomRoutine)
                        row.Cells[2].Value = "Execute";
                    else
                        row.Cells[2].Value = "Copy";
                    row.Visible = !(update.value is UiDomUndefined);
                }
            }
            if (TreeUpdates.Count != 0)
            {
                element_tree.BeginUpdate();
                while (TreeUpdates.TryDequeue(out var update))
                {
                    if (update is ChildrenUpdate ch)
                    {
                        TreeNodeCollection nodes;
                        if (ch.parent is null)
                            nodes = element_tree.Nodes;
                        else if (element_nodes.TryGetValue(ch.parent, out var parent_node))
                            nodes = parent_node.Nodes;
                        else
                            continue;
                        var children = ch.children;
                        int i;
                        for (i = 0; i < children.Length; i++)
                        {
                            var child_id = children[i].Item1;
                            var child_desc = children[i].Item2;
                            if (element_nodes.TryGetValue(child_id, out var child_node))
                            {
                                if (i >= nodes.Count || nodes[i] != child_node)
                                {
                                    child_node.Remove();
                                    nodes.Insert(i, child_node);
                                }
                            }
                            else
                            {
                                element_nodes[child_id] = nodes.Insert(i, child_id, child_desc);
                            }
                        }
                        while (i < nodes.Count)
                        {
                            var node = nodes[i];
                            element_nodes.Remove(node.Name);
                            node.Remove();
                        }
                    }
                    else if (update is DescUpdate desc)
                    {
                        if (element_nodes.TryGetValue(desc.element, out var node))
                        {
                            node.Text = desc.desc;
                        }
                    }
                }
                element_tree.EndUpdate();
            }
        }

        private void UiDomViewer_FormClosed(object sender, FormClosedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void element_tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            properties_view.Rows.Clear();
            MainContext.Post((object st) =>
            {
                Provider.SetCurrentElement(element_tree.SelectedNode?.Name);
            }, null);
        }

        private void properties_view_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 2)
                return;
            var value = properties_view.Rows[e.RowIndex].Cells[1].Value;
            if (value is UiDomElement element)
            {
                if (element_nodes.TryGetValue(element.DebugId, out var node))
                {
                    element_tree.SelectedNode = node;
                }
            }
            else if (value is UiDomRoutine routine)
            {
                MainContext.Post((object st) =>
                {
                    routine.Pulse();
                }, null);
            }
            else
            {
                Clipboard.SetText(value.ToString());
            }
        }
    }
}
