using System;
using System.Threading;
using System.Threading.Tasks;

using Tmds.DBus;

using Gazelle.UiDom;
using Gazelle.AtSpi.DBus;
using Gazelle.Gudl;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Gazelle.AtSpi
{
    internal class AtSpiObject : UiDomObject
    {
        internal readonly AtSpiConnection Connection;
        internal readonly string Service;
        internal readonly string Path;
        public override string DebugId => string.Format("{0}:{1}", Service, Path);

        private bool watching_children;
        private bool children_known;
        private IDisposable children_changed_event;

        internal IAccessible acc;

        internal IObject object_events;

        internal AtSpiObject(AtSpiConnection connection, string service, string path) : base(connection)
        {
            Path = path;
            Service = service;
            Connection = connection;
            acc = connection.connection.CreateProxy<IAccessible>(service, path);
            object_events = connection.connection.CreateProxy<IObject>(service, path);
        }

        internal AtSpiObject(AtSpiConnection connection, string service, ObjectPath path) :
            this(connection, service, path.ToString())
        { }

        private async Task WatchChildrenTask()
        {
            IDisposable children_changed_event = await object_events.WatchChildrenChangedAsync(OnChildrenChanged, Utils.OnError);

            if (this.children_changed_event != null)
                this.children_changed_event.Dispose();

            this.children_changed_event = children_changed_event;

            (string, ObjectPath)[] children = await acc.GetChildrenAsync();

            if (children_known)
                return;

            for (int i=0; i<children.Length; i++)
            {
                string service = children[i].Item1;
                ObjectPath path = children[i].Item2;
                AddChild(i, new AtSpiObject(Connection, service, path));
            }
            children_known = true;
        }

        internal void WatchChildren()
        {
            if (watching_children)
                return;
#if DEBUG
            Console.WriteLine("WatchChildren for {0}", DebugId);
#endif
            watching_children = true;
            children_known = false;
            Utils.RunTask(WatchChildrenTask());
        }

        internal void UnwatchChildren()
        {
            if (!watching_children)
                return;
#if DEBUG
            Console.WriteLine("UnwatchChildren for {0}", DebugId);
#endif
            watching_children = false;
            if (children_changed_event != null)
            {
                children_changed_event.Dispose();
                children_changed_event = null;
            }
            for (int i=Children.Count-1; i >= 0; i--)
            {
                RemoveChild(i);
            }
        }

        private void OnChildrenChanged((string, uint, uint, object) obj)
        {
            if (!watching_children || !children_known)
                return;
            var detail = obj.Item1;
            var index = obj.Item2;
            var id = ((string, ObjectPath))(obj.Item4);
            var service = id.Item1;
            var path = id.Item2.ToString();
            if (detail == "add")
            {
                AddChild((int)index, new AtSpiObject(Connection, id.Item1, id.Item2));
            }
            else if (detail == "remove")
            {
                // Don't assume the index matches our internal view, we don't always get "reorder" notificaions
#if DEBUG
                bool found = false;
#endif
                for (int i=0; i<Children.Count; i++)
                {
                    var child = (AtSpiObject)Children[i];
                    if (child.Service == service && child.Path == path)
                    {
#if DEBUG
                        if (index != i)
                        {
                            Console.WriteLine("Got remove notification for {0} with index {1}, but we have it at index {2}", child.DebugId, index, i);
                        }
                        found = true;
#endif
                        RemoveChild(i);
                        break;
                    }
                }
#if DEBUG
                if (!found)
                    Console.WriteLine("Got remove notification from {0} for {1}:{2}, but we don't have it as a child",
                        DebugId, service, path);
#endif
            }
        }

        protected override void DeclarationsChanged(Dictionary<string, UiDomValue> all_declarations, HashSet<(UiDomObject, GudlExpression)> dependencies)
        {
            if (all_declarations.TryGetValue("recurse", out var recurse) && recurse.ToBool())
                WatchChildren();
            else
                UnwatchChildren();

            base.DeclarationsChanged(all_declarations, dependencies);
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "spi_service":
                    // depends_on.Add((this, new IdentifierExpression(id))); // not needed because this property is known and can't change
                    return new UiDomString(Service);
                case "spi_path":
                    // depends_on.Add((this, new IdentifierExpression(id))); // not needed because this property is known and can't change
                    return new UiDomString(Path);
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
