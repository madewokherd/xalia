using System;
using System.Collections;
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

        public string[] SupportedInterfaces { get; private set; }
        private bool fetching_supported;

        private static string[] _trackedProperties = new string[] { "recurse_method" };

        private static readonly Dictionary<string, int> name_to_role;
        private static readonly UiDomEnum[] role_to_enum;

        internal static Dictionary<string, int> name_to_state;

        internal static readonly string[] role_names =
        {
            "invalid",
            "accelerator_label",
            "alert",
            "animation",
            "arrow",
            "calendar",
            "canvas",
            "check_box",
            "check_menu_item",
            "color_chooser",
            "column_header",
            "combo_box",
            "date_editor",
            "desktop_icon",
            "desktop_frame",
            "dial",
            "dialog",
            "directory_pane",
            "drawing_area",
            "file_chooser",
            "filler",
            "focus_traversable",
            "font_chooser",
            "frame",
            "glass_pane",
            "html_container",
            "icon",
            "image",
            "internal_frame",
            "label",
            "layered_pane",
            "list",
            "list_item",
            "menu",
            "menu_bar",
            "menu_item",
            "option_pane",
            "page_tab",
            "page_tab_list",
            "panel",
            "password_text",
            "popup_menu",
            "progress_bar",
            "push_button",
            "radio_button",
            "radio_menu_item",
            "root_pane",
            "row_header",
            "scroll_bar",
            "scroll_pane",
            "separator",
            "slider",
            "spin_button",
            "split_pane",
            "status_bar",
            "table",
            "table_cell",
            "table_column_header",
            "table_row_header",
            "tearoff_menu_item",
            "terminal",
            "text",
            "toggle_button",
            "tool_bar",
            "tool_tip",
            "tree",
            "tree_table",
            "unknown",
            "viewport",
            "window",
            "extended",
            "header",
            "footer",
            "paragraph",
            "ruler",
            "application",
            "autocomplete",
            "editbar",
            "embedded",
            "entry",
            "chart",
            "caption",
            "document_frame",
            "heading",
            "page",
            "section",
            "redundant_object",
            "form",
            "link",
            "input_method_window",
            "table_row",
            "tree_item",
            "document_spreadsheet",
            "document_presentation",
            "document_text",
            "document_web",
            "document_email",
            "comment",
            "list_box",
            "grouping",
            "image_map",
            "notification",
            "info_bar",
            "level_bar",
            "title_bar",
            "block_quote",
            "audio",
            "video",
            "definition",
            "article",
            "landmark",
            "log",
            "marquee",
            "math",
            "rating",
            "timer",
            "static",
            "math_fraction",
            "math_root",
            "subscript",
            "superscript",
            "description_list",
            "description_term",
            "description_value",
            "footnote",
            "content_deletion",
            "content_insertion",
            "mark",
            "suggestion",
        };

        internal static readonly string[] state_names =
        {
            "invalid",
            "active",
            "armed",
            "busy",
            "checked",
            "collapsed",
            "defunct",
            "editable",
            "enabled",
            "expandable",
            "expanded",
            "focusable",
            "focused",
            "has_tooltip",
            "horizontal",
            "iconified",
            "modal",
            "multi_line",
            "multiselectable",
            "opaque",
            "pressed",
            "resizable",
            "selectable",
            "selected",
            "sensitive",
            "showing",
            "single_line",
            "stale",
            "transient",
            "vertical",
            "visible",
            "manages_descendants",
            "indeterminate",
            "required",
            "truncated",
            "animated",
            "invalid_entry",
            "supports_autocompletion",
            "selectable_text",
            "is_default",
            "visited",
            "checkable",
            "has_popup",
            "read_only",
        };

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "role", "spi_role" },
            { "control_type", "spi_role" },
            { "state", "spi_state" },
        };

        private static readonly HashSet<string> other_interface_properties = new HashSet<string>()
        {
            // ComponentProvider
            "x", "y", "width", "height",
            "abs_x", "abs_y", "abs_width", "abs_height",
            "spi_abs_x", "spi_abs_y", "spi_abs_width", "spi_abs_height",
        };

        static AccessibleProvider()
        {
            name_to_role = new Dictionary<string, int>();
            role_to_enum = new UiDomEnum[role_names.Length];
            for (int i=0; i<role_names.Length; i++)
            {
                string name = role_names[i];
                string[] names;
                if (name == "push_button")
                    names = new[] { "push_button", "pushbutton", "button" };
                else if (name == "page_tab")
                    names = new[] { "page_tab", "pagetab", "tab" };
                else if (name == "page_tab_list")
                    names = new[] { "page_tab_list", "pagetablist", "tab_item", "tabitem" };
                else if (name == "text")
                    names = new[] { "text", "text_box", "textbox", "edit" };
                else if (name.Contains("_"))
                    names = new[] { name, name.Replace("_", "") };
                else
                    names = new[] { name };
                role_to_enum[i] = new UiDomEnum(names);
                foreach (string rolename in names)
                    name_to_role[rolename] = i;
            }
            name_to_state = new Dictionary<string, int>();
            for (int i=0; i<state_names.Length; i++)
            {
                name_to_state[state_names[i]] = i;
            }
        }

        private bool watching_children;
        private bool children_known;

        public bool RoleKnown { get; private set; }
        public int Role { get; private set; }
        private bool fetching_role;

        public bool StateKnown { get; private set; }
        public uint[] State { get; private set; }
        private bool fetching_state;

        public static UiDomValue ValueFromRole(int role)
        {
            if (role > 0 && role < role_to_enum.Length)
                return role_to_enum[role];
            else
                return new UiDomInt(role);
        }

        public UiDomValue RoleAsValue => RoleKnown ? ValueFromRole(Role) : UiDomUndefined.Instance;

        public void DumpProperties(UiDomElement element)
        {
            if (RoleKnown)
                Utils.DebugWriteLine($"  spi_role: {RoleAsValue}");
            if (StateKnown)
                Utils.DebugWriteLine($"  spi_state: {new AtSpiState(State)}");
            if (!(SupportedInterfaces is null))
                Utils.DebugWriteLine($"  spi_supported: [{String.Join(",", SupportedInterfaces)}]");
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
                case "spi_role":
                    depends_on.Add((element, new IdentifierExpression("spi_role")));
                    return RoleAsValue;
                case "spi_state":
                    depends_on.Add((element, new IdentifierExpression("spi_state")));
                    if (StateKnown)
                        return new AtSpiState(State);
                    return UiDomUndefined.Instance;
                case "spi_supported":
                    depends_on.Add((element, new IdentifierExpression("spi_supported")));
                    if (!(SupportedInterfaces is null))
                    {
                        return new AtSpiSupported(SupportedInterfaces);
                    }
                    return UiDomUndefined.Instance;
            }
            if (other_interface_properties.Contains(identifier))
                depends_on.Add((element, new IdentifierExpression("spi_supported")));
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
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, Element.Root, depends_on);
            if (name_to_role.TryGetValue(identifier, out var expected_role))
            {
                depends_on.Add((element, new IdentifierExpression("spi_role")));
                if (RoleKnown)
                    return UiDomBoolean.FromBool(Role == expected_role);
            }
            if (name_to_state.TryGetValue(identifier, out var expected_state))
            {
                depends_on.Add((element, new IdentifierExpression("spi_state")));
                if (StateKnown)
                    return UiDomBoolean.FromBool(AtSpiState.IsStateSet(State, expected_state));
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
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_role":
                        if (!fetching_role)
                        {
                            fetching_role = true;
                            Utils.RunTask(FetchRole());
                        }
                        return true;
                    case "spi_state":
                        if (!fetching_state)
                        {
                            fetching_state = true;
                            Utils.RunTask(FetchState());
                        }
                        return true;
                    case "spi_supported":
                        if (!fetching_supported)
                        {
                            fetching_supported = true;
                            Utils.RunTask(FetchSupported());
                        }
                        break;
                }
            }
            return false;
        }

        private async Task FetchSupported()
        {
            try
            {
                SupportedInterfaces = await CallMethod(Connection.Connection, Peer, Path, IFACE_ACCESSIBLE,
                    "GetInterfaces", ReadMessageStringArray);
            }
            catch (DBusException e)
            {
                if (!AtSpiElement.IsExpectedException(e))
                    throw;
                return;
            }
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"{Element}.spi_supported: ({string.Join(",", SupportedInterfaces)})");
            Element.PropertyChanged("spi_supported");
            foreach (var iface in SupportedInterfaces)
            {
                bool seen_component = false;
                switch (iface)
                {
                    case IFACE_COMPONENT:
                        if (!seen_component)
                            Element.AddProvider(new ComponentProvider(this), 0);
                        seen_component = true;
                        break;
                }
            }
        }

        private async Task FetchState()
        {
            uint[] result;
            try
            {
                await Connection.RegisterEvent("object:state-changed");

                result = await CallMethod(Connection.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "GetState", ReadMessageUint32Array);
            }
            catch (DBusException e)
            {
                if (!AtSpiElement.IsExpectedException(e))
                    throw;
                return;
            }
            if (StateKnown)
            {
                if (StructuralComparisons.StructuralEqualityComparer.Equals(State, result))
                    return;
            }
            StateKnown = true;
            State = result;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"{Element}.spi_state: {new AtSpiState(State)}");
            Element.PropertyChanged("spi_state");
        }

        internal void AtSpiPropertyChange(string detail, object value)
        {
            switch (detail)
            {
                case "accessible-role":
                    {
                        if (value is uint uval)
                            value = (int)uval;
                        if (value is int ival && (!RoleKnown || ival != Role))
                        {
                            RoleKnown = true;
                            Role = ival;
                            if (Element.MatchesDebugCondition())
                                Utils.DebugWriteLine($"{Element}.spi_role: {RoleAsValue}");
                            Element.PropertyChanged("spi_role");
                        }
                        else if (value is null)
                        {
                            if (fetching_role || RoleKnown)
                                Utils.RunTask(FetchRole());
                        }
                        else
                        {
                            Console.WriteLine($"WARNING: unexpected type for accessible-role: {value.GetType()}");
                        }
                        break;
                    }
                default:
                    break;
            }
        }

        internal void AtSpiStateChanged(AtSpiSignal signal)
        {
            if (!StateKnown)
                return;
            var new_state = AtSpiState.SetState(State, signal.detail, signal.detail1 != 0);
            if (new_state is null)
                return;
            if (StructuralComparisons.StructuralEqualityComparer.Equals(State, new_state))
                return;
            State = new_state;
            if (Element.MatchesDebugCondition())
            {
                var action = (signal.detail1 != 0) ? "added" : "removed";
                Utils.DebugWriteLine($"{Element}.spi_state: {new AtSpiState(State)} ({signal.detail} {action})");
            }
            Element.PropertyChanged("spi_state");
        }

        private async Task FetchRole()
        {
            int result;
            try
            {
                await Connection.RegisterEvent("object:property-change:accessible-role");

                result = await CallMethod(Connection.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "GetRole", ReadMessageInt32);
            }
            catch (DBusException e)
            {
                if (!AtSpiElement.IsExpectedException(e))
                    throw;
                return;
            }
            AtSpiPropertyChange("accessible-role", result);
        }

        private void AncestorBoundsChanged()
        {
            if (!(SupportedInterfaces is null))
            {
                foreach (var provider in Element.Providers)
                {
                    if (provider is ComponentProvider component)
                    {
                        component.AncestorBoundsChanged();
                        break;
                    }
                }
            }
            foreach (var child in Element.Children)
            {
                foreach (var provider in child.Providers)
                {
                    if (provider is AccessibleProvider acc)
                    {
                        acc.AncestorBoundsChanged();
                    }
                }
            }
        }

        internal void AtSpiBoundsChanged(AtSpiSignal signal)
        {
            // This should arguably be on ComponentProvider, but we need
            // the event to update bounds of descendents even if there is
            // no ComponentProvider for this element.
            AncestorBoundsChanged();
        }

        internal Task<IDisposable> LimitPolling(bool value_known)
        {
            return Connection.LimitPolling(Peer, value_known);
        }
    }
}
