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
        }

        public UiDomViewer UiDomViewer { get; }
        public UiDomRoot Root { get; }
        public SynchronizationContext ViewerContext { get; }

        private struct ElementInfo
        {
            public string[] children;
            public IDisposable children_notifier;
        }

        private Dictionary<string, ElementInfo> elements = new Dictionary<string, ElementInfo>();
        private bool poke_queued;

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
            var depends_on = new HashSet<(UiDomElement, Gudl.GudlExpression)>();

            var id = element.EvaluateIdentifier("id", Root, depends_on);
            string id_str;
            if (id is UiDomUndefined)
                id_str = "";
            else
                id_str = " " + id.ToString();

            var role = element.EvaluateIdentifier("role", Root, depends_on);
            string role_str;
            if (role is UiDomEnum e)
                role_str = " " + e.Names[0];
            else
                role_str = "";

            var name = element.EvaluateIdentifier("name", Root, depends_on);
            string name_str;
            if (name is UiDomString str)
                name_str = " " + str.Value;
            else
                name_str = "";

            return $"{element.DebugId}{id_str}{role_str}{name_str}";
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
                elements.Add(element.DebugId, info);
            }
            return info;
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
            UiDomViewer.ChildrenUpdates.Enqueue((element?.DebugId, child_descs));
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
