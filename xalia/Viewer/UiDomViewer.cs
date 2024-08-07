using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
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


        public ConcurrentQueue<(string, (string, string)[])> ChildrenUpdates = new ConcurrentQueue<(string, (string, string)[])>();

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
            if (ChildrenUpdates.Count != 0)
            {
                element_tree.BeginUpdate();
                while (ChildrenUpdates.TryDequeue(out var update))
                {
                    TreeNodeCollection nodes;
                    if (update.Item1 is null)
                        nodes = element_tree.Nodes;
                    else if (element_nodes.TryGetValue(update.Item1, out var parent_node))
                        nodes = parent_node.Nodes;
                    else
                        continue;
                    var children = update.Item2;
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
                element_tree.EndUpdate();
            }
        }
    }
}
