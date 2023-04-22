using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class AccessibleProvider : IUiDomProvider
    {
        public AccessibleProvider(UiDomElement element, AtSpiConnection connection, string peer, string path)
        {
            Element = element;
            Connection = connection;
            Peer = peer;
            Path = path;
        }

        public UiDomElement Element { get; private set; }
        public AtSpiConnection Connection { get; }
        public string Peer { get; }
        public string Path { get; }

        private static string[] _trackedProperties = new string[] { "recurse_method" };

        private bool watching_children;
        private bool children_known;

        public void DumpProperties(UiDomElement element)
        {
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_uia_element":
                    return UiDomBoolean.False;
                case "is_spi_element":
                case "is_atspi_element":
                case "is_at_spi_element":
                    return UiDomBoolean.True;
                case "spi_peer":
                    return new UiDomString(Peer);
                case "spi_path":
                    return new UiDomString(Path);
            }
            return UiDomUndefined.Instance;
        }

        public UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                        return new UiDomString("spi_auto");
                    break;
            }
            return UiDomUndefined.Instance;
        }

        public Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public string[] GetTrackedProperties()
        {
            return _trackedProperties;
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
            watching_children = false;
            children_known = false;
            Element = null;
        }

        private async Task<List<(string, string)>> GetChildList()
        {
            try
            {
                var children = await CallMethod(Connection.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "GetChildren", ReadMessageElementList);

                if (children.Count == 0)
                {
                    var child_count = (int)await GetProperty(Connection.Connection, Peer, Path,
                        IFACE_ACCESSIBLE, "ChildCount");
                    if (child_count != 0)
                    {
                        // This happens for AtkSocket/AtkPlug
                        // https://gitlab.gnome.org/GNOME/at-spi2-core/-/issues/98

                        children = new List<(string, string)>(child_count);

                        for (int i = 0; i < child_count; i++)
                        {
                            children.Add(await CallMethod(Connection.Connection, Peer, Path,
                                IFACE_ACCESSIBLE, "GetChildAtIndex", i, ReadMessageElement));
                        }
                    }
                }

                return children;
            }
            catch (DBusException e)
            {
                if (!AtSpiElement.IsExpectedException(e))
                    throw;
                return new List<(string, string)>();
            }
            catch (InvalidCastException)
            {
                return new List<(string, string)>();
            }
        }

        private async Task PollChildrenTask()
        {
            if (!watching_children)
                return;

            await Connection.RegisterEvent("object:children-changed");

            List<(string, string)> children = await GetChildList();

            // Ignore any duplicate children
            HashSet<(string, string)> seen_children = new HashSet<(string, string)>();
            int i = 0;
            while (i < children.Count)
            {
                if (!seen_children.Add(children[i]))
                {
                    children.RemoveAt(i);
                    continue;
                }
                i++;
            }

            // First remove any existing children that are missing or out of order
            i = 0;
            foreach (var new_child in children)
            {
                if (!Element.Children.Exists((UiDomElement element) => ElementMatches(element, new_child)))
                    continue;
                while (!ElementMatches(Element.Children[i], new_child))
                {
                    Element.RemoveChild(i);
                }
                i++;
            }

            // Remove any remaining missing children
            while (i < Element.Children.Count && Element.Children[i] is AtSpiElement)
                Element.RemoveChild(i);

            // Add any new children
            i = 0;
            foreach (var new_child in children)
            {
                if (Element.Children.Count <= i || !ElementMatches(Element.Children[i], new_child))
                {
                    if (!(Connection.LookupElement(new_child) is null))
                    {
                        // Child element is a duplicate of another element somewhere in the tree.
                        continue;
                    }
                    Element.AddChild(i, new AtSpiElement(Connection, new_child.Item1, new_child.Item2));
                }
                i += 1;
            }

            children_known = true;
        }

        private bool ElementMatches(UiDomElement element, (string, string) new_child)
        {
            return element is AtSpiElement e && e.Peer == new_child.Item1 && e.Path == new_child.Item2;
        }

        internal void WatchChildren()
        {
            if (watching_children)
                return;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"WatchChildren for {Element}");
            watching_children = true;
            children_known = false;
            Utils.RunTask(PollChildrenTask());
        }

        internal void UnwatchChildren()
        {
            if (!watching_children)
                return;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {Element}");
            watching_children = false;
            for (int i=Element.Children.Count-1; i >= 0; i--)
            {
                if (Element.Children[i] is AtSpiElement)
                    Element.RemoveChild(i);
            }
        }

        internal void AtSpiChildrenChanged(AtSpiSignal signal)
        {
            if (!children_known)
                return;
            var index = signal.detail1;
            var child = ((string, ObjectPath))signal.value;
            var child_element = Connection.LookupElement(child);
            switch (signal.detail)
            {
                case "add":
                    {
                        if (!(child_element is null))
                        {
                            Utils.DebugWriteLine($"WARNING: {child_element} added to {Element} but is already a child of {child_element.Parent}, ignoring.");
                            return;
                        }
                        if (index > Element.Children.Count || index < 0)
                        {
                            Utils.DebugWriteLine($"WARNING: {child.Item1}:{child.Item2} added to {Element} at index {index}, but there are only {Element.Children.Count} known children");
                            index = Element.Children.Count;
                        }
                        Element.AddChild(index, new AtSpiElement(Connection, child.Item1, child.Item2));
                        break;
                    }
                case "remove":
                    {
                        if (child_element is null)
                        {
                            Utils.DebugWriteLine($"WARNING: {child.Item1}:{child.Item2} removed from {Element}, but the element is unknown");
                            return;
                        }
                        if (child_element.Parent != Element)
                        {
                            Utils.DebugWriteLine($"WARNING: {child.Item1}:{child.Item2} removed from {Element}, but is a child of {child_element.Parent}");
                            return;
                        }
                        if (index >= Element.Children.Count || index < 0 || Element.Children[index] != child_element)
                        {
                            var real_index = Element.Children.IndexOf(child_element);
                            Utils.DebugWriteLine($"WARNING: {child.Item1}:{child.Item2} remove event has wrong index - got {index}, should be {real_index}");
                            index = real_index;
                        }
                        Element.RemoveChild(index);
                        break;
                    }
                default:
                    Utils.DebugWriteLine($"WARNING: unknown detail on ChildrenChanged event: {signal.detail}");
                    break;
            }
        }

        public void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            switch (name)
            {
                case "recurse_method":
                    {
                        if (new_value is UiDomString st && st.Value == "spi_auto")
                            WatchChildren();
                        else
                            UnwatchChildren();
                        break;
                    }
            }
        }

        public bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }
    }
}
