using System;
using System.Collections.Generic;
using System.Threading;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Viewer
{
    internal class UiDomViewerProvider : UiDomProviderBase
    {
        public UiDomViewerProvider(UiDomViewer uiDomViewer, UiDomRoot root, SynchronizationContext viewer_context)
        {
            UiDomViewer = uiDomViewer;
            Root = root;
            ViewerContext = viewer_context;
            ElementDescExpression = "element_identifier + (\" \" + role.name or \"\") + (\" \" + id or \"\") + (\" \" + name or \"\")";
        }

        public UiDomViewer UiDomViewer { get; }
        public UiDomRoot Root { get; }
        public SynchronizationContext ViewerContext { get; }

        private struct ElementInfo
        {
            public string[] children;
            public IDisposable children_notifier;
            public ExpressionWatcher desc_notifier;
        }

        private Dictionary<string, ElementInfo> elements = new Dictionary<string, ElementInfo>();
        private bool poke_queued;

        private string element_desc_expression;
        private string ElementDescExpression
        {
            get => element_desc_expression;
            set
            {
                element_desc_expression = value;
                element_desc_compiled = GudlParser.ParseExpression(value);
            }
        }

        private GudlExpression element_desc_compiled;


        internal void DoMainThreadSetup()
        {
            Root.AddGlobalProvider(this);
            Utils.RunIdle(SendFullTree);
        }

        private void SendFullTree()
        {
            SendElementChildren(null, new UiDomElement[] { Root });
        }

        private void QueuePoke()
        {
            if (!poke_queued)
            {
                Utils.RunIdle(PokeViewerThread);
                poke_queued = true;
            }
        }

        private void PokeViewerThread()
        {
            ViewerContext.Post(UiDomViewer.QueuesUpdated, null);
            poke_queued = false;
        }

        private string GetElementDesc(UiDomElement element)
        {
            var depends_on = new HashSet<(UiDomElement, GudlExpression)>();

            var result = element.Evaluate(element_desc_compiled, depends_on);

            if (result is UiDomString s)
                return s.Value;

            return $"{element.DebugId} <ERROR>";
        }

        private ElementInfo CreateElementInfo(UiDomElement element)
        {
            if (element is null)
            {
                // parent of root element, should only be used once, from SendFullTree
                var result = new ElementInfo();
                result.children = new string[0];
                return result;
            }
            if (!elements.TryGetValue(element.DebugId, out var info))
            {
                info = new ElementInfo();
                info.children = new string[0];
                info.children_notifier = element.NotifyPropertyChanged(
                    new IdentifierExpression("children"), OnElementChildrenChanged);
                info.desc_notifier = new ExpressionWatcher(element, Root, element_desc_compiled);
                info.desc_notifier.ValueChanged += OnElementDescChanged;
                elements.Add(element.DebugId, info);
            }
            return info;
        }

        private void OnElementDescChanged(object sender, EventArgs e)
        {
            var watcher = (ExpressionWatcher)sender;
            var element = (UiDomElement)watcher.Context;

            if (!elements.ContainsKey(element.DebugId))
                return;

            if (watcher.CurrentValue is UiDomString s)
                SendElementDesc(element.DebugId, s.Value);
            else
                SendElementDesc(element.DebugId, $"{element.DebugId} <ERROR>");
        }

        private void SendElementDesc(string debug_id, string desc)
        {
            UiDomViewer.TreeUpdates.Enqueue(
                new UiDomViewer.DescUpdate { element = debug_id, desc = desc }
                );
            QueuePoke();
        }

        private void OnElementChildrenChanged(UiDomElement element, GudlExpression property)
        {
            SendElementChildren(element);
        }

        private bool DeleteElementInfo(string element)
        {
            if (elements.TryGetValue(element, out var info))
            {
                info.children_notifier.Dispose();
                info.desc_notifier.Dispose();
                foreach (var child in info.children)
                {
                    DeleteElementInfo(child);
                }
                elements.Remove(element);
                return true;
            }
            return false;
        }

        private void SendElementChildren(UiDomElement element)
        {
            SendElementChildren(element, element.Children);
        }

        private void SendElementChildren(UiDomElement element, IList<UiDomElement> children)
        {
            if (!(element is null) && !elements.ContainsKey(element.DebugId))
                return;
            var info = CreateElementInfo(element);
            var prev_children = new HashSet<string>(info.children.Length);
            List<UiDomElement> new_children = new List<UiDomElement>(children.Count);
            var child_descs = new (string, string)[children.Count];
            for (int i = 0; i < child_descs.Length; i++)
            {
                var child = children[i];
                if (!prev_children.Remove(child.DebugId))
                {
                    new_children.Add(child);
                }
                child_descs[i] = (child.DebugId, GetElementDesc(child));
            }
            UiDomViewer.TreeUpdates.Enqueue(
                new UiDomViewer.ChildrenUpdate { parent = element?.DebugId, children = child_descs });
            foreach (var child in prev_children)
            {
                DeleteElementInfo(child);
            }
            foreach (var child in new_children)
            {
                CreateElementInfo(child);
                SendElementChildren(child);
            }
            QueuePoke();
        }
    }
}
