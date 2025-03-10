﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class AccessibleProvider : UiDomProviderBase, IUiDomValueProvider
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
        private Task wait_for_supported_task;

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
            { "name", "spi_name" },
            { "description", "spi_description" },
            { "accessible_id", "spi_accessible_id" },
            { "id", "spi_accessible_id" },
            { "attributes", "spi_attributes" },
            { "select", "spi_select" },
            { "deselect", "spi_deselect" },
            { "toggle_selected", "spi_toggle_selected" },
        };

        private static readonly HashSet<string> other_interface_properties = new HashSet<string>()
        {
            // ActionProvider
            "action", "spi_action", "do_default_action", "spi_do_default_action",
            // ApplicationProvider
            "toolkit_name", "spi_toolkit_name",
            // ComponentProvider
            "x", "y", "width", "height",
            "abs_x", "abs_y", "abs_width", "abs_height",
            "spi_abs_x", "spi_abs_y", "spi_abs_width", "spi_abs_height",
            "grab_focus", "set_focus", "spi_grab_focus",
            // SelectionProvider
            "clear_selection", "spi_clear_selection",
            // ValueProvider
            "minimum_value", "spi_minimum_value", "maximum_value", "spi_maximum_value",
            "minimum_increment", "small_change", "spi_minimum_increment",
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
                    names = new[] { "page_tab", "pagetab", "tab_item", "tabitem" };
                else if (name == "page_tab_list")
                    names = new[] { "page_tab_list", "pagetablist", "tab" };
                else if (name == "text")
                    names = new[] { "text", "text_box", "textbox", "edit" };
                else if (name == "table_cell")
                    names = new[] { "table_cell", "tablecell", "cell" };
                else if (name == "table_row_header")
                    names = new[] { "table_row_header", "tablerowheader", "row_header", "rowheader" };
                else if (name == "table_column_header")
                    names = new[] { "table_column_header", "tablecolumnheader", "column_header", "columnheader" };
                else if (name == "table_row")
                    names = new[] { "table_row", "tablerow", "row" };
                else if (name == "link")
                    names = new[] { "link", "hyperlink" };
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
        private enum GetChildrenMethod
        {
            Auto,
            GetChildren,
            GetChildAtIndex
        }
        private GetChildrenMethod recurse_method;
        private bool children_known;

        public bool RoleKnown { get; private set; }
        public int Role { get; private set; }
        private bool fetching_role;

        public bool StateKnown { get; private set; }
        public uint[] State { get; private set; }
        private bool fetching_state;

        public bool NameKnown { get; private set; }
        public string Name { get; private set; }
        private bool fetching_name;

        public bool DescriptionKnown { get; private set; }
        public string Description { get; private set; }
        private bool fetching_description;

        public bool AccessibleIdKnown { get; private set; }
        public string AccessibleId { get; private set; }
        private bool fetching_accessible_id;

        public bool AttributesKnown { get; private set; }
        public Dictionary<string,string> Attributes { get; private set; }
        private bool watching_attributes;
        private int attributes_change_count;

        public bool ApplicationKnown { get; private set; }
        public string ApplicationPeer { get; private set; }
        public string ApplicationPath { get; private set; }
        private bool fetching_application;

        public static UiDomValue ValueFromRole(int role)
        {
            if (role > 0 && role < role_to_enum.Length)
                return role_to_enum[role];
            else
                return new UiDomInt(role);
        }

        public UiDomValue RoleAsValue => RoleKnown ? ValueFromRole(Role) : UiDomUndefined.Instance;

        public override void DumpProperties(UiDomElement element)
        {
            if (RoleKnown)
                Utils.DebugWriteLine($"  spi_role: {RoleAsValue}");
            if (StateKnown)
                Utils.DebugWriteLine($"  spi_state: {new AtSpiState(State)}");
            if (NameKnown)
                Utils.DebugWriteLine($"  spi_name: \"{Name}\"");
            if (DescriptionKnown && Description != string.Empty)
                Utils.DebugWriteLine($"  spi_description: \"{Description}\"");
            if (AccessibleIdKnown && AccessibleId != string.Empty)
                Utils.DebugWriteLine($"  spi_accessible_id: \"{AccessibleId}\"");
            if (AttributesKnown)
            {
                foreach (var kvp in Attributes)
                {
                    Utils.DebugWriteLine($"  spi_attributes.{kvp.Key}: \"{kvp.Value}\"");
                }
            }
            if (!(SupportedInterfaces is null))
                Utils.DebugWriteLine($"  spi_supported: [{String.Join(",", SupportedInterfaces)}]");
            if (ApplicationKnown)
                Utils.DebugWriteLine($"  spi_application: {ApplicationPeer}:{ApplicationPath}");
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
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
                case "spi_name":
                    depends_on.Add((element, new IdentifierExpression("spi_name")));
                    if (NameKnown)
                        return new UiDomString(Name);
                    return UiDomUndefined.Instance;
                case "spi_description":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (DescriptionKnown)
                        return new UiDomString(Description);
                    return UiDomUndefined.Instance;
                case "spi_accessible_id":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (AccessibleIdKnown)
                        return new UiDomString(AccessibleId);
                    return UiDomUndefined.Instance;
                case "spi_supported":
                    depends_on.Add((element, new IdentifierExpression("spi_supported")));
                    if (!(SupportedInterfaces is null))
                    {
                        return new AtSpiSupported(SupportedInterfaces);
                    }
                    return UiDomUndefined.Instance;
                case "spi_application":
                    depends_on.Add((element, new IdentifierExpression("spi_application")));
                    if (ApplicationKnown)
                    {
                        var application = Connection.LookupElement((ApplicationPeer, ApplicationPath));
                        if (!(application is null))
                            return application;
                    }
                    return UiDomUndefined.Instance;
                case "spi_attributes":
                    depends_on.Add((element, new IdentifierExpression("spi_attributes")));
                    if (AttributesKnown)
                        return new AtSpiAttributes(Attributes);
                    return UiDomUndefined.Instance;
                case "spi_select":
                    if (!(element.Parent is null)) {
                        depends_on.Add((element.Parent, new IdentifierExpression("spi_supported")));
                        var parent_acc = element.Parent.ProviderByType<AccessibleProvider>();
                        if (!(parent_acc is null) && !(parent_acc.SupportedInterfaces is null) &&
                            parent_acc.SupportedInterfaces.Contains(IFACE_SELECTION))
                            return new UiDomRoutineAsync(element, "spi_select", SelectAsync);
                    }
                    break;
                case "spi_deselect":
                    if (!(element.Parent is null)) {
                        depends_on.Add((element.Parent, new IdentifierExpression("spi_supported")));
                        var parent_acc = element.Parent.ProviderByType<AccessibleProvider>();
                        if (!(parent_acc is null) && !(parent_acc.SupportedInterfaces is null) &&
                            parent_acc.SupportedInterfaces.Contains(IFACE_SELECTION))
                            return new UiDomRoutineAsync(element, "spi_deselect", DeselectAsync);
                    }
                    break;
                case "spi_toggle_selected":
                    if (!(element.Parent is null)) {
                        depends_on.Add((element.Parent, new IdentifierExpression("spi_supported")));
                        var parent_acc = element.Parent.ProviderByType<AccessibleProvider>();
                        if (!(parent_acc is null) && !(parent_acc.SupportedInterfaces is null) &&
                            parent_acc.SupportedInterfaces.Contains(IFACE_SELECTION))
                            return new UiDomRoutineAsync(element, "spi_toggle_selected", ToggleSelectedAsync);
                    }
                    break;
            }
            if (other_interface_properties.Contains(identifier))
                depends_on.Add((element, new IdentifierExpression("spi_supported")));
            return UiDomUndefined.Instance;
        }

        private static async Task SelectAsync(UiDomRoutineAsync obj)
        {
            var acc = obj.Element.ProviderByType<AccessibleProvider>();
            var parent_acc = obj.Element.Parent?.ProviderByType<AccessibleProvider>();
            if (parent_acc is null)
                return;
            try
            {
                var index = await CallMethod(acc.Connection.Connection, acc.Peer, acc.Path, IFACE_ACCESSIBLE,
                    "GetIndexInParent", ReadMessageInt32);

                var success = await CallMethod(parent_acc.Connection.Connection, parent_acc.Peer, parent_acc.Path, IFACE_SELECTION,
                    "SelectChild", index, ReadMessageBoolean);

                if (!success)
                {
                    Utils.DebugWriteLine($"WARNING: SelectChild failed for {obj.Element}");
                }
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
            }
        }


        private static async Task DeselectAsync(UiDomRoutineAsync obj)
        {
            var acc = obj.Element.ProviderByType<AccessibleProvider>();
            var parent_acc = obj.Element.Parent?.ProviderByType<AccessibleProvider>();
            if (parent_acc is null)
                return;
            try
            {
                var index = await CallMethod(acc.Connection.Connection, acc.Peer, acc.Path, IFACE_ACCESSIBLE,
                    "GetIndexInParent", ReadMessageInt32);

                var success = await CallMethod(parent_acc.Connection.Connection, parent_acc.Peer, parent_acc.Path, IFACE_SELECTION,
                    "DeselectChild", index, ReadMessageBoolean);

                if (!success)
                {
                    Utils.DebugWriteLine($"WARNING: DeselectChild failed for {obj.Element}");
                }
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
            }
        }

        private static async Task ToggleSelectedAsync(UiDomRoutineAsync obj)
        {
            var acc = obj.Element.ProviderByType<AccessibleProvider>();
            var parent_acc = obj.Element.Parent?.ProviderByType<AccessibleProvider>();
            try
            {
                var index = await CallMethod(acc.Connection.Connection, acc.Peer, acc.Path, IFACE_ACCESSIBLE,
                    "GetIndexInParent", ReadMessageInt32);

                var is_selected = await CallMethod(acc.Connection.Connection, parent_acc.Peer,
                    parent_acc.Path, IFACE_SELECTION, "IsChildSelected", index, ReadMessageBoolean);

                bool success;
                if (is_selected)
                {
                    var num_selected = await GetPropertyInt(acc.Connection.Connection, parent_acc.Peer,
                        parent_acc.Path, IFACE_SELECTION, "NSelectedChildren");

                    if (num_selected == 1)
                        success = await CallMethod(acc.Connection.Connection, parent_acc.Peer, parent_acc.Path,
                            IFACE_SELECTION, "ClearSelection", ReadMessageBoolean);
                    else
                        success = await CallMethod(parent_acc.Connection.Connection, parent_acc.Peer, parent_acc.Path,
                            IFACE_SELECTION, "DeselectChild", index, ReadMessageBoolean);
                }
                else
                    success = await CallMethod(parent_acc.Connection.Connection, parent_acc.Peer, parent_acc.Path,
                        IFACE_SELECTION, "SelectChild", index, ReadMessageBoolean);

                if (!success)
                {
                    Utils.DebugWriteLine($"WARNING: Toggling selection failed for {obj.Element}");
                }
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
            }
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                    {
                        if (element.EvaluateIdentifier("poll_children", element.Root, depends_on).ToBool())
                            return new UiDomString("spi_get_children_poll");
                        else
                            return new UiDomString("spi_get_children");
                    }
                    break;
                case "toolkit_name":
                    depends_on.Add((element, new IdentifierExpression("spi_application")));
                    if (ApplicationKnown)
                    {
                        var application = Connection.LookupElement((ApplicationPeer, ApplicationPath));
                        if (!(application is null))
                            return application.EvaluateIdentifier("spi_toolkit_name", element.Root, depends_on);
                    }
                    return UiDomUndefined.Instance;
                case "application_name":
                case "process_name":
                    depends_on.Add((element, new IdentifierExpression("spi_application")));
                    if (ApplicationKnown)
                    {
                        var application = Connection.LookupElement((ApplicationPeer, ApplicationPath));
                        if (!(application is null))
                            return application.EvaluateIdentifier("spi_name", element.Root, depends_on);
                    }
                    return UiDomUndefined.Instance;
                case "do_default_action":
                    depends_on.Add((element, new IdentifierExpression("spi_state")));
                    if (StateKnown && AtSpiState.IsStateSet(State, "selectable"))
                        return element.EvaluateIdentifier("spi_select", element.Root, depends_on);
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

        public override async Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            if (SupportedInterfaces is null)
            {
                // This is implemented in ComponentAccessible, so wait for it to be created
                await WaitForSupported();
                var com = element.ProviderByType<ComponentProvider>();
                if (!(com is null))
                {
                    return await com.GetClickablePointAsync(element);
                }
            }
            return (false, 0, 0);
        }

        public override string[] GetTrackedProperties()
        {
            return _trackedProperties;
        }

        public override void NotifyElementRemoved(UiDomElement element)
        {
            Connection.NotifyElementDestroyed(this);
            watching_children = false;
            children_known = false;
        }

        private async Task<List<(string, string)>> GetChildListGetChildren()
        {
            try
            {
                return await CallMethod(Connection.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "GetChildren", ReadMessageElementList);
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return new List<(string, string)>();
            }
            catch (InvalidCastException)
            {
                return new List<(string, string)>();
            }
        }

        private async Task<List<(string, string)>> GetChildListGetChildAtIndex()
        {
            try
            {
                var child_count = await GetPropertyInt(Connection.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "ChildCount");
                var children = new List<(string, string)>(child_count);
                for (int i = 0; i < child_count; i++)
                {
                    children.Add(await CallMethod(Connection.Connection, Peer, Path,
                        IFACE_ACCESSIBLE, "GetChildAtIndex", i, ReadMessageElement));
                }

                return children;
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return new List<(string, string)>();
            }
            catch (InvalidCastException)
            {
                return new List<(string, string)>();
            }
        }

        private async Task<List<(string, string)>> GetChildListAuto()
        {
            var children = await GetChildListGetChildren();
            if (children.Count == 0)
            {
                // This happens for AtkSocket/AtkPlug
                // https://gitlab.gnome.org/GNOME/at-spi2-core/-/issues/98

                children = await GetChildListGetChildAtIndex();
            }
            return children;
        }

        private async Task<List<(string, string)>> GetChildList()
        {
            switch (recurse_method)
            {
                case GetChildrenMethod.Auto:
                    return await GetChildListAuto();
                case GetChildrenMethod.GetChildren:
                    return await GetChildListGetChildren();
                case GetChildrenMethod.GetChildAtIndex:
                    return await GetChildListGetChildAtIndex();
            }
            throw new InvalidOperationException("recurse_method is invalid"); // shouldn't happen
        }

        private async Task PollChildrenTask()
        {
            if (!watching_children)
                return;

            await Connection.RegisterEvent("object:children-changed");

            List<(string, string)> children = await GetChildList();

            if (!watching_children)
                return;

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
                var existing = Connection.LookupElement(children[i]);
                if (!(existing is null) && existing.Parent != Element)
                {
                    // duplicate elsewhere in tree
                    children.RemoveAt(i);
                    continue;
                }
                i++;
            }

            Element.SyncRecurseMethodChildren(children, KeyToElementId, Connection.CreateElement);

            children_known = true;
        }

        private string KeyToElementId((string, string) arg)
        {
            return $"{arg.Item1}:{arg.Item2}";
        }

        internal void WatchChildren(bool method_changed)
        {
            if (watching_children)
            {
                if (method_changed)
                    Utils.RunTask(PollChildrenTask());
                return;
            }
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"WatchChildren for {Element}");
            Element.SetRecurseMethodProvider(this);
            watching_children = true;
            children_known = false;
            Utils.RunTask(PollChildrenTask());
        }

        internal void UnwatchChildren()
        {
            if (!watching_children)
                return;
            Element.EndPollProperty(new IdentifierExpression("spi_children"));
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {Element}");
            watching_children = false;
            Element.UnsetRecurseMethodProvider(this);
        }

        internal void AtSpiChildrenChanged(AtSpiSignal signal)
        {
            if (!children_known)
                return;
            var index = signal.detail1;
            var child = (signal.value.GetItem(0).GetString(), signal.value.GetItem(1).GetObjectPathAsString());
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
                        Element.AddChild(index, Connection.CreateElement(child.Item1, child.Item2), true);
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
                        Element.RemoveChild(index, true);
                        break;
                    }
                default:
                    Utils.DebugWriteLine($"WARNING: unknown detail on ChildrenChanged event: {signal.detail}");
                    break;
            }
        }

        public override void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            switch (name)
            {
                case "recurse_method":
                    {
                        if (new_value is UiDomString st)
                        {
                            GetChildrenMethod new_method;
                            bool poll = true;
                            switch (st.Value)
                            {
                                case "spi_auto":
                                    poll = false;
                                    goto case "spi_auto_poll";
                                case "spi_auto_poll":
                                    new_method = GetChildrenMethod.Auto;
                                    break;
                                case "spi_get_children":
                                    poll = false;
                                    goto case "spi_get_children_poll";
                                case "spi_get_children_poll":
                                    new_method = GetChildrenMethod.GetChildren;
                                    break;
                                case "spi_child_at_index":
                                    poll = false;
                                    goto case "spi_child_at_index_poll";
                                case "spi_child_at_index_poll":
                                    new_method = GetChildrenMethod.GetChildAtIndex;
                                    break;
                                default:
                                    UnwatchChildren();
                                    return;
                            }

                            bool method_changed = (new_method != recurse_method);
                            recurse_method = new_method;
                            WatchChildren(method_changed);
                            if (poll)
                                element.PollProperty(new IdentifierExpression("spi_children"), PollChildrenTask, 2000);
                            else
                                element.EndPollProperty(new IdentifierExpression("spi_children"));
                        }
                        else
                            UnwatchChildren();
                        break;
                    }
            }
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_attributes":
                        watching_attributes = false;
                        return true;
                }
            }
            return false;
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
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
                    case "spi_name":
                        if (!fetching_name)
                        {
                            fetching_name = true;
                            Utils.RunTask(FetchName());
                        }
                        return true;
                    case "spi_description":
                        if (!fetching_description)
                        {
                            fetching_description = true;
                            Utils.RunTask(FetchDescription());
                        }
                        return true;
                    case "spi_accessible_id":
                        if (!fetching_accessible_id)
                        {
                            fetching_accessible_id = true;
                            Utils.RunTask(FetchAccessibleId());
                        }
                        return true;
                    case "spi_supported":
                        if (!fetching_supported)
                        {
                            // Somehow, FetchSupported can complete without waiting, which
                            // would modify the providers list while it's being iterated to
                            // call WatchProperty, so delay it to prevent that.
                            Utils.RunIdle(DoFetchSupported);
                        }
                        break;
                    case "spi_application":
                        if (!fetching_application)
                        {
                            fetching_application = true;
                            Utils.RunTask(FetchApplication());
                        }
                        break;
                    case "spi_attributes":
                        watching_attributes = true;
                        if (!AttributesKnown)
                            Utils.RunTask(FetchAttributes());
                        return true;
                }
            }
            return false;
        }

        private void DoFetchSupported()
        {
            if (!fetching_supported)
            {
                fetching_supported = true;
                wait_for_supported_task = FetchSupported();
                Utils.RunTask(wait_for_supported_task);
            }
        }

        private async Task FetchAttributes()
        {
            var old_change_count = attributes_change_count;
            Dictionary<string, string> result;
            try
            {
                await Connection.RegisterEvent("object:attributes-changed");

                result = await CallMethod(Connection.Connection, Peer, Path, IFACE_ACCESSIBLE,
                    "GetAttributes", ReadMessageStringDictionary);
            }
            catch (DBusException e)
            {
                if (AtSpiConnection.IsExpectedException(e))
                    return;
                throw;
            }

            if (attributes_change_count != old_change_count)
                return;

            if (!AttributesKnown || !Utils.DictionariesEqual(Attributes, result))
            {
                AttributesKnown = true;
                Attributes = result;
                if (Element.MatchesDebugCondition())
                {
                    foreach (var kvp in Attributes)
                    {
                        Utils.DebugWriteLine($"{Element}.spi_attributes.{kvp.Key}: \"{kvp.Value}\"");
                    }
                }
                Element.PropertyChanged("spi_attributes");
            }
        }

        private async Task FetchApplication()
        {
            (string, string) result;
            try
            {
                result = await CallMethod(Connection.Connection, Peer, Path, IFACE_ACCESSIBLE,
                    "GetApplication", ReadMessageElement);
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }
            ApplicationKnown = true;
            ApplicationPeer = result.Item1;
            ApplicationPath = result.Item2;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"{Element}.spi_application: {result.Item1}:{result.Item2}");
            Element.PropertyChanged("spi_application");
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
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"{Element}.spi_supported: ({string.Join(",", SupportedInterfaces)})");
            Element.PropertyChanged("spi_supported");
            foreach (var iface in SupportedInterfaces)
            {
                bool seen_action = false;
                bool seen_application = false;
                bool seen_component = false;
                bool seen_selection = false;
                bool seen_value = false;
                switch (iface)
                {
                    case IFACE_ACTION:
                        if (!seen_action)
                            Element.AddProvider(new ActionProvider(this), 0);
                        seen_action = true;
                        break;
                    case IFACE_APPLICATION:
                        if (!seen_application)
                            Element.AddProvider(new ApplicationProvider(this), 0);
                        seen_application = true;
                        break;
                    case IFACE_COMPONENT:
                        if (!seen_component)
                            Element.AddProvider(new ComponentProvider(this), 0);
                        seen_component = true;
                        break;
                    case IFACE_SELECTION:
                        if (!seen_selection)
                            Element.AddProvider(new SelectionProvider(this), 0);
                        seen_selection = true;
                        break;
                    case IFACE_VALUE:
                        if (!seen_value)
                            Element.AddProvider(new ValueProvider(this), 0);
                        seen_value = true;
                        break;
                }
            }
        }

        private Task WaitForSupported()
        {
            if (!(SupportedInterfaces is null))
                return Task.CompletedTask;
            if (!fetching_supported)
            {
                fetching_supported = true;
                wait_for_supported_task = FetchSupported();
            }
            return wait_for_supported_task;
        }

        private async Task FetchState()
        {
            uint[] result;
            try
            {
                await Connection.RegisterEvent("object:state-changed");
                await Connection.RegisterEvent("window:activate");
                await Connection.RegisterEvent("window:deactivate");

                result = await CallMethod(Connection.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "GetState", ReadMessageUint32Array);
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
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

        private async Task FetchName()
        {
            VariantValue result;
            try
            {
                await Connection.RegisterEvent("object:property-change:accessible-name");

                result = await GetProperty(Connection.Connection, Peer, Path, IFACE_ACCESSIBLE, "Name");
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }
            if (result.Type == VariantValueType.Invalid)
            {
                // This would infinitely recurse
                return;
            }
            AtSpiPropertyChange("accessible-name", result);
        }

        private async Task FetchDescription()
        {
            VariantValue result;
            try
            {
                await Connection.RegisterEvent("object:property-change:accessible-description");

                result = await GetProperty(Connection.Connection, Peer, Path, IFACE_ACCESSIBLE, "Description");
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }
            if (result.Type == VariantValueType.Invalid)
            {
                // This would infinitely recurse
                return;
            }
            AtSpiPropertyChange("accessible-description", result);
        }

        private async Task FetchAccessibleId()
        {
            string result;
            try
            {
                // No event for this? Probably not supposed to change during the element's lifetime.

                result = await GetPropertyString(Connection.Connection, Peer, Path, IFACE_ACCESSIBLE, "AccessibleId");
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }

            AccessibleId = result;
            AccessibleIdKnown = true;
            Element.PropertyChanged("spi_accessible_id", result);
        }

        internal void AtSpiPropertyChange(string detail, VariantValue value)
        {
            switch (detail)
            {
                case "accessible-name":
                    {
                        if (value.Type == VariantValueType.String)
                        {
                            string sval = value.GetString();
                            if (!NameKnown || sval != Name)
                            {
                                NameKnown = true;
                                Name = sval;
                                Element.PropertyChanged("spi_name", sval);
                            }
                        }
                        else if (value.Type == VariantValueType.Invalid)
                        {
                            if (fetching_name || NameKnown)
                                Utils.RunTask(FetchName());
                        }
                        else
                        {
                            Utils.DebugWriteLine($"WARNING: unexpected type for accessible-name: {value.GetType()}");
                        }
                        break;
                    }
                case "accessible-description":
                    {
                        if (value.Type == VariantValueType.String)
                        {
                            string sval = value.GetString();
                            if (!DescriptionKnown || sval != Description)
                            {
                                DescriptionKnown = true;
                                Description = sval;
                                Element.PropertyChanged("spi_description", sval);
                            }
                        }
                        else if (value.Type == VariantValueType.Invalid)
                        {
                            if (fetching_description || DescriptionKnown)
                                Utils.RunTask(FetchDescription());
                        }
                        else
                        {
                            Utils.DebugWriteLine($"WARNING: unexpected type for accessible-description: {value.GetType()}");
                        }
                        break;
                    }
                case "accessible-role":
                    {
                        int ival;
                        if (value.Type == VariantValueType.UInt32)
                            ival = (int)value.GetUInt32();
                        else if (value.Type == VariantValueType.Int32)
                            ival = value.GetInt32();
                        else if (value.Type == VariantValueType.Invalid)
                        {
                            if (fetching_role || RoleKnown)
                                Utils.RunTask(FetchRole());
                            break;
                        }
                        else
                        {
                            Utils.DebugWriteLine($"WARNING: unexpected type for accessible-role: {value}");
                            break;
                        }
                        RoleKnown = true;
                        Role = ival;
                        if (Element.MatchesDebugCondition())
                            Utils.DebugWriteLine($"{Element}.spi_role: {RoleAsValue}");
                        Element.PropertyChanged("spi_role");
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
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }
            AtSpiPropertyChange("accessible-role", result);
        }

        private void AncestorBoundsChanged()
        {
            if (!(SupportedInterfaces is null))
            {
                Element.ProviderByType<ComponentProvider>()?.AncestorBoundsChanged();
            }
            foreach (var child in Element.Children)
            {
                child.ProviderByType<AccessibleProvider>()?.AncestorBoundsChanged();
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

        public async Task<double> GetMinimumIncrementAsync(UiDomElement element)
        {
            if (SupportedInterfaces is null)
            {
                // This is implemented in ValueProvider, so wait for it to be created
                await WaitForSupported();
                var value = element.ProviderByType<ValueProvider>();
                if (!(value is null))
                {
                    return await value.GetMinimumIncrementAsync(element);
                }
            }
            return 0;
        }

        public async Task<bool> OffsetValueAsync(UiDomElement element, double offset)
        {
            if (SupportedInterfaces is null)
            {
                // This is implemented in ValueProvider, so wait for it to be created
                await WaitForSupported();
                var value = element.ProviderByType<ValueProvider>();
                if (!(value is null))
                {
                    return await value.OffsetValueAsync(element, offset);
                }
            }
            return false;
        }

        internal void AtSpiOnAttributesChanged(AtSpiSignal signal)
        {
            attributes_change_count++;
            if (watching_attributes)
            {
                Utils.RunTask(FetchAttributes());
            }
            else
            {
                AttributesKnown = false;
            }
        }
    }
}
