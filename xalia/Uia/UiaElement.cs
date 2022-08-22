using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.EventHandlers;
using FlaUI.Core.Identifiers;
using FlaUI.Core.WindowsAPI;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Uia
{
    public class UiaElement : UiDomElement
    {
        public UiaElement(UiaElementWrapper wrapper) : base(wrapper.Connection)
        {
            ElementWrapper = wrapper;
        }

        public UiaElementWrapper ElementWrapper { get; }

        public override string DebugId
        {
            get
            {
                return ElementWrapper.UniqueId;
            }
        }

        public string ElementIdentifier
        {
            get
            {
                return ElementWrapper.UniqueId;
            }
        }

        public new UiaConnection Root
        {
            get
            {
                return ElementWrapper.Connection;
            }
        }

        Dictionary<PropertyId, bool> fetching_property = new Dictionary<PropertyId, bool>();
        Dictionary<PropertyId, bool> property_known = new Dictionary<PropertyId, bool>();
        Dictionary<PropertyId, object> property_raw_value = new Dictionary<PropertyId, object>();
        Dictionary<PropertyId, UiDomValue> property_value = new Dictionary<PropertyId, UiDomValue>();

        bool watching_children;
        bool refreshing_children;
        bool inflight_structure_changed;

        PatternId[] supported_patterns;
        bool fetching_supported_patterns;

        internal static readonly string[] control_type_names =
        {
            "unknown",
            "app_bar",
            "button",
            "calendar",
            "check_box",
            "combo_box",
            "custom",
            "data_grid",
            "data_item",
            "document",
            "edit",
            "group",
            "header",
            "header_item",
            "hyperlink",
            "image",
            "list",
            "list_item",
            "menu_bar",
            "menu",
            "menu_item",
            "pane",
            "progress_bar",
            "radio_button",
            "scroll_bar",
            "semantic_zoom",
            "separator",
            "slider",
            "spinner",
            "split_button",
            "status_bar",
            "tab",
            "tab_item",
            "table",
            "text",
            "thumb",
            "title_bar",
            "tool_bar",
            "tool_tip",
            "tree",
            "tree_item",
            "window",
        };

        internal static readonly string[] msaa_role_names =
        {
            "unknown",
            "title_bar",
            "menu_bar",
            "scroll_bar",
            "grip",
            "sound",
            "cursor",
            "caret",
            "alert",
            "window",
            "client",
            "menu_popup",
            "menu_item",
            "tool_tip",
            "application",
            "document",
            "pane",
            "chart",
            "dialog",
            "border",
            "grouping",
            "separator",
            "tool_bar",
            "status_bar",
            "table",
            "column_header",
            "row_header",
            "column",
            "row",
            "cell",
            "link",
            "help_balloon",
            "character",
            "list",
            "list_item",
            "outline",
            "outline_item",
            "page_tab",
            "property_page",
            "indicator",
            "graphic",
            "static_text",
            "text",
            "push_button",
            "check_button",
            "radio_button",
            "combo_box",
            "drop_list",
            "progress_bar",
            "dial",
            "hotkey_field",
            "slider",
            "spin_button",
            "diagram",
            "animation",
            "equation",
            "button_dropdown",
            "button_menu",
            "button_dropdown_grid",
            "white_space",
            "page_tab_list",
            "clock",
            "split_button",
            "ip_address",
            "outline_button",
        };

        private static readonly Dictionary<string, ControlType> name_to_control_type;
        private static readonly UiDomEnum[] control_type_to_enum;

        private static readonly UiDomEnum[] msaa_role_to_enum;

        private static readonly Dictionary<string, string> property_aliases;

        static UiaElement()
        {
            name_to_control_type = new Dictionary<string, ControlType>();
            control_type_to_enum = new UiDomEnum[control_type_names.Length];
            for (int i = 0; i < control_type_names.Length; i++)
            {
                string name = control_type_names[i];
                string[] names;
                if (name == "button")
                    names = new[] { "button", "push_button", "pushbutton" };
                else if (name == "tab")
                    names = new[] { "tab", "page_tab_list", "pagetablist" };
                else if (name == "tab_item")
                    names = new[] { "tab_item", "tabitem", "page_tab", "pagetab" };
                else if (name == "edit")
                    names = new[] { "edit", "text_box", "textbox" };
                else if (name.Contains("_"))
                    names = new[] { name, name.Replace("_", "") };
                else
                    names = new[] { name };
                control_type_to_enum[i] = new UiDomEnum(names);
                foreach (string rolename in names)
                    name_to_control_type[rolename] = (ControlType)i;
            }
            msaa_role_to_enum = new UiDomEnum[msaa_role_names.Length];
            for (int i = 0; i < msaa_role_names.Length; i++)
            {
                string name = msaa_role_names[i];
                string[] names;
                if (name.Contains("_"))
                    names = new[] { name, name.Replace("_", "") };
                else
                    names = new[] { name };
                msaa_role_to_enum[i] = new UiDomEnum(names);
            }
            string[] aliases = {
                "role", "uia_control_type",
                "control_type", "uia_control_type",
                "controltype", "uia_control_type",
                "enabled", "uia_enabled",
                "is_enabled", "uia_enabled",
                "offscreen", "uia_offscreen",
                "is_offscreen", "uia_offscreen",
                "visible", "uia_visible",
                "selected", "uia_selected",
                "is_selected", "uia_selected",
                "bounds", "uia_bounding_rectangle",
                "bounding_rectangle", "uia_bounding_rectangle",
                "x", "uia_x",
                "abs_x", "uia_x",
                "y", "uia_y",
                "abs_y", "uia_y",
                "width", "uia_width",
                "height", "uia_height",
                "name", "uia_name",
                "class_name", "uia_class_name",
                "expand_collapse_state", "uia_expand_collapse_state",
                "focused", "uia_focused",
                "foreground", "msaa_foreground",
                "active", "win32_active",
                "set_focus", "uia_set_focus",
                "focused_element", "uia_focused_element",
                "foreground_element", "uia_foreground_element",
                "active_element", "win32_active_element",
                "menu_mode", "uia_menu_mode",
                "opened_menu", "uia_opened_menu",
                "in_menu", "uia_in_menu",
                "in_submenu", "uia_in_submenu",
                "invoke", "ui_invoke",
                "select", "uia_select",
                "expand", "uia_expand",
                "collapse", "uia_collapse",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i=0; i<aliases.Length; i+=2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
        }

        internal async Task RefreshChildren()
        {
            if (!watching_children)
                return;

            if (refreshing_children)
            {
                // Don't send two refreshes at once, but do queue another in case
                // the result from the current one is out of date
                inflight_structure_changed = true;
                return;
            }

            refreshing_children = true;

            var children = await Root.CommandThread.GetChildren(ElementWrapper);

            if (!watching_children)
                return;

            // First remove any existing children that are missing or out of order
            int i = 0;
            foreach (var new_child in children)
            {
                if (!Children.Exists((UiDomElement element) => ElementMatches(element, new_child.UniqueId)))
                    continue;
                while (!ElementMatches(Children[i], new_child.UniqueId))
                {
                    RemoveChild(i);
                }
                i++;
            }

            // Add any new children
            for (i = 0; i < children.Length; i++)
            {
                if (Children.Count <= i || !ElementMatches(Children[i], children[i].UniqueId))
                {
                    AddChild(i, new UiaElement(children[i]));
                }
            }

            refreshing_children = false;

            if (inflight_structure_changed)
            {
                inflight_structure_changed = false;
                Utils.RunTask(RefreshChildren());
            }
        }

        private bool ElementMatches(UiDomElement uidom, string element_id)
        {
            if (uidom is UiaElement uia)
                return uia.ElementIdentifier == element_id;
            return false;
        }

        internal void UpdateChildren()
        {
            Utils.RunTask(RefreshChildren());
        }

        internal void OnChildrenChanged(StructureChangeType arg2, int[] arg3)
        {
#if DEBUG
            Console.WriteLine("OnChildrenChanged for {0}", DebugId);
#endif
            UpdateChildren();
        }

        internal void WatchChildren()
        {
            if (watching_children)
                return;
#if DEBUG
            Console.WriteLine("WatchChildren for {0}", DebugId);
#endif
            watching_children = true;
            Utils.RunTask(RefreshChildren());
        }

        protected override void DeclarationsChanged(Dictionary<string, UiDomValue> all_declarations, HashSet<(UiDomElement, GudlExpression)> dependencies)
        {
            if (all_declarations.TryGetValue("recurse", out var recurse) && recurse.ToBool())
                WatchChildren();

            base.DeclarationsChanged(all_declarations, dependencies);
        }

        private async Task FetchPropertyAsync(string name, PropertyId propid)
        {
            object current_value = await Root.CommandThread.GetPropertyValue(ElementWrapper, propid);
            OnPropertyChange(name, propid, current_value);
        }

        public void OnPropertyChange(string name, PropertyId propid, object value)
        {
            UiDomValue new_value;

            if (name == "uia_expand_collapse_state" && value is int ecsi)
            {
                value = (ExpandCollapseState)ecsi;
            }
            else if (name == "uia_control_type" && value is int cti)
            {
                value = (ControlType)cti;
            }
            else if (name == "msaa_role" && value is int rolei)
            {
                value = (AccessibilityRole)rolei;
            }

            if (value is System.Drawing.Rectangle r)
            {
                new_value = new UiDomString(r.ToString());
            }
            else if (value is System.Windows.Rect swr)
            {
                value = new System.Drawing.Rectangle((int)swr.Left, (int)swr.Top, (int)swr.Width, (int)swr.Height);
                new_value = new UiDomString(value.ToString());
            }
            else if (value is double[] dba && name == "uia_bounding_rectangle" && dba.Length == 4)
            {
                value = new System.Drawing.Rectangle((int)dba[0], (int)dba[1], (int)dba[2], (int)dba[3]);
                new_value = new UiDomString(value.ToString());
            }
            else if (value is ControlType ct && (int)ct < control_type_to_enum.Length)
            {
                new_value = control_type_to_enum[(int)ct];
            }
            else if (value is AccessibilityRole role)
            {
                new_value = msaa_role_to_enum[(int)role];
            }
            else if (value is bool b)
            {
                new_value = UiDomBoolean.FromBool(b);
            }
            else if (value is string s)
            {
                new_value = new UiDomString(s);
            }
            else if (value is ExpandCollapseState ecs)
            {
                switch (ecs)
                {
                    case ExpandCollapseState.Collapsed:
                        new_value = new UiDomEnum(new[] { "collapsed" });
                        break;
                    case ExpandCollapseState.Expanded:
                        new_value = new UiDomEnum(new[] { "expanded" });
                        break;
                    case ExpandCollapseState.PartiallyExpanded:
                        new_value = new UiDomEnum(new[] { "partially_expanded" });
                        break;
                    case ExpandCollapseState.LeafNode:
                        new_value = new UiDomEnum(new[] { "leaf_node" });
                        break;
                    default:
                        new_value = UiDomUndefined.Instance;
                        break;
                }
            }
            else
            {
                if (!(value is null))
                    Console.WriteLine($"Warning: value for {name} has unsupported type {value.GetType()}");
                new_value = UiDomUndefined.Instance;
            }

            UiDomValue old_value;
            if (!property_value.TryGetValue(propid, out old_value))
                old_value = UiDomUndefined.Instance;

            property_known[propid] = true;
            if (!old_value.Equals(new_value))
            {
#if DEBUG
                Console.WriteLine($"{DebugId}.{name}: {new_value}");
#endif
                property_value[propid] = new_value;
                property_raw_value[propid] = value;
                PropertyChanged(name);
            }
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "uia_supported_patterns":
                        {
                            if (!fetching_supported_patterns)
                            {
                                fetching_supported_patterns = true;
                                Utils.RunTask(FetchSupportedPatterns());
                            }
                            break;
                        }
                }

                if (Root.names_to_property.TryGetValue(id.Name, out var propid) &&
                    (!property_known.TryGetValue(propid, out var known) || !known) &&
                    (!fetching_property.TryGetValue(propid, out var fetching) || !fetching))
                {
                    fetching_property[propid] = true;
                    Utils.RunTask(FetchPropertyAsync(id.Name, propid));
                }
            }

            base.WatchProperty(expression);
        }

        private async Task FetchSupportedPatterns()
        {
            supported_patterns = await Root.CommandThread.GetSupportedPatterns(ElementWrapper);

            PropertyChanged("uia_supported_patterns");
        }

        protected override void SetAlive(bool value)
        {
            if (value)
            {
                Root.elements_by_id[ElementIdentifier] = this;
                if (ElementWrapper.Equals(Root.FocusedElement))
                {
                    Root.PropertyChanged("uia_focused_element");
                }
                if (ElementWrapper.Equals(Root.ForegroundElement))
                {
                    Root.PropertyChanged("msaa_foreground_element");
                }
                if (ElementWrapper.Equals(Root.ActiveElement))
                {
                    Root.PropertyChanged("win32_active_element");
                }
                if (ElementWrapper.Equals(Root.UiaOpenedMenu))
                {
                    Root.PropertyChanged("uia_opened_menu");
                }
            }
            else
            {
                property_known.Clear();
                property_value.Clear();
                property_raw_value.Clear();
                Root.elements_by_id.Remove(ElementIdentifier);
            }
            base.SetAlive(value);
        }

        private UiDomValue GetProperty(string name, PropertyId id, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            depends_on.Add((this, new IdentifierExpression(name)));
            if (property_known.TryGetValue(id, out var known) && known)
            {
                if (property_value.TryGetValue(id, out var val))
                    return val;
                return UiDomUndefined.Instance;
            }
            return UiDomUndefined.Instance;
        }

        private object GetRawProperty(string name, PropertyId id, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            depends_on.Add((this, new IdentifierExpression(name)));
            if (property_known.TryGetValue(id, out var known) && known)
            {
                if (property_raw_value.TryGetValue(id, out var val))
                    return val;
                return null;
            }
            return UiDomUndefined.Instance;
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
                case "uia_controltype":
                case "uia_control_type":
                    return GetProperty("uia_control_type", Root.Automation.PropertyLibrary.Element.ControlType, depends_on);
                case "uia_enabled":
                case "uia_is_enabled":
                    return GetProperty("uia_enabled", Root.Automation.PropertyLibrary.Element.IsEnabled, depends_on);
                case "uia_offscreen":
                case "uia_is_offscreen":
                    return GetProperty("uia_offscreen", Root.Automation.PropertyLibrary.Element.IsOffscreen, depends_on);
                case "uia_visible":
                    return UiDomBoolean.FromBool(!GetProperty("uia_offscreen", Root.Automation.PropertyLibrary.Element.IsEnabled, depends_on).ToBool());
                case "uia_selected":
                case "uia_is_selected":
                    return GetProperty("uia_selected", Root.Automation.PropertyLibrary.Element.IsEnabled, depends_on);
                case "uia_bounding_rectangle":
                    return GetProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on);
                case "uia_x":
                case "uia_abs_x":
                    {
                        if (GetRawProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on) is System.Drawing.Rectangle r)
                        {
                            return new UiDomInt(r.Left);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "uia_y":
                case "uia_abs_y":
                    {
                        if (GetRawProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on) is System.Drawing.Rectangle r)
                        {
                            return new UiDomInt(r.Top);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "uia_width":
                    {
                        if (GetRawProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on) is System.Drawing.Rectangle r)
                        {
                            return new UiDomInt(r.Width);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "uia_height":
                    {
                        if (GetRawProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on) is System.Drawing.Rectangle r)
                        {
                            return new UiDomInt(r.Height);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "uia_name":
                    return GetProperty("uia_name", Root.Automation.PropertyLibrary.Element.Name, depends_on);
                case "uia_class_name":
                    return GetProperty("uia_class_name", Root.Automation.PropertyLibrary.Element.ClassName, depends_on);
                case "uia_expand_collapse_state":
                    return GetProperty("uia_expand_collapse_state", Root.Automation.PropertyLibrary.ExpandCollapse.ExpandCollapseState, depends_on);
                case "msaa_role":
                    return GetProperty("msaa_role", Root.Automation.PropertyLibrary.LegacyIAccessible.Role, depends_on);
                case "uia_focused":
                    depends_on.Add((this, new IdentifierExpression("uia_focused")));
                    return UiDomBoolean.FromBool(ElementWrapper.Equals(Root.FocusedElement));
                case "msaa_foreground":
                    depends_on.Add((this, new IdentifierExpression("msaa_foreground")));
                    return UiDomBoolean.FromBool(ElementWrapper.Equals(Root.ForegroundElement));
                case "win32_active":
                    depends_on.Add((this, new IdentifierExpression("win32_active")));
                    return UiDomBoolean.FromBool(ElementWrapper.Equals(Root.ActiveElement));
                case "uia_set_focus":
                    {
                        return new UiDomRoutineAsync(this, "uia_set_focus", DoSetFocus);
                    }
                case "uia_focused_element":
                    depends_on.Add((Root, new IdentifierExpression("uia_focused_element")));
                    return (UiDomValue)Root.LookupAutomationElement(Root.FocusedElement) ?? UiDomUndefined.Instance;
                case "msaa_foreground_element":
                    depends_on.Add((Root, new IdentifierExpression("msaa_foreground_element")));
                    return (UiDomValue)Root.LookupAutomationElement(Root.ForegroundElement) ?? UiDomUndefined.Instance;
                case "win32_active_element":
                    depends_on.Add((Root, new IdentifierExpression("win32_active_element")));
                    return (UiDomValue)Root.LookupAutomationElement(Root.ActiveElement) ?? UiDomUndefined.Instance;
                case "uia_menu_mode":
                    depends_on.Add((Root, new IdentifierExpression("uia_menu_mode")));
                    return UiDomBoolean.FromBool(Root.UiaMenuMode);
                case "uia_opened_menu":
                    return Root.EvaluateIdentifier(id, Root, depends_on);
                case "uia_in_menu":
                    return Root.EvaluateIdentifier(id, Root, depends_on);
                case "uia_in_submenu":
                    return Root.EvaluateIdentifier(id, Root, depends_on);
                case "is_uia_element":
                    return UiDomBoolean.True;
                case "is_spi_element":
                case "is_atspi_element":
                case "is_at_spi_element":
                    return UiDomBoolean.False;
                case "uia_invoke":
                    depends_on.Add((this, new IdentifierExpression("uia_supported_patterns")));
                    if (!(supported_patterns is null) && supported_patterns.Contains(Root.Automation.PatternLibrary.InvokePattern))
                    {
                        return new UiDomRoutineAsync(this, "uia_invoke", Invoke);
                    }
                    return UiDomUndefined.Instance;
                case "msaa_do_default_action":
                    depends_on.Add((this, new IdentifierExpression("uia_supported_patterns")));
                    if (!(supported_patterns is null) && supported_patterns.Contains(Root.Automation.PatternLibrary.LegacyIAccessiblePattern))
                    {
                        return new UiDomRoutineAsync(this, "msaa_do_default_action", MsaaDefaultAction);
                    }
                    return UiDomUndefined.Instance;
                case "uia_select":
                    depends_on.Add((this, new IdentifierExpression("uia_supported_patterns")));
                    if (!(supported_patterns is null) && supported_patterns.Contains(Root.Automation.PatternLibrary.SelectionItemPattern))
                    {
                        return new UiDomRoutineAsync(this, "uia_select", Select);
                    }
                    return UiDomUndefined.Instance;
                case "uia_expand":
                    depends_on.Add((this, new IdentifierExpression("uia_supported_patterns")));
                    if (!(supported_patterns is null) && supported_patterns.Contains(Root.Automation.PatternLibrary.ExpandCollapsePattern))
                    {
                        return new UiDomRoutineAsync(this, "uia_expand", Expand);
                    }
                    return UiDomUndefined.Instance;
                case "uia_collapse":
                    depends_on.Add((this, new IdentifierExpression("uia_supported_patterns")));
                    if (!(supported_patterns is null) && supported_patterns.Contains(Root.Automation.PatternLibrary.ExpandCollapsePattern))
                    {
                        return new UiDomRoutineAsync(this, "uia_collapse", Collapse);
                    }
                    return UiDomUndefined.Instance;
            }

            {
                var value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;
            }

            if (name_to_control_type.ContainsKey(id))
            {
                return Evaluate(new BinaryExpression(
                    new IdentifierExpression("uia_control_type"),
                    new IdentifierExpression(id),
                    GudlToken.Dot), depends_on);
            }

            return UiDomUndefined.Instance;
        }

        private Task MsaaDefaultAction(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.OnBackgroundThread(() =>
            {
                ElementWrapper.AutomationElement.Patterns.LegacyIAccessible.Pattern.DoDefaultAction();
            }, ElementWrapper);
        }

        private Task DoSetFocus(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.OnBackgroundThread(() =>
            {
                ElementWrapper.AutomationElement.Focus();
            }, ElementWrapper);
        }

        private Task Invoke(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.Invoke(ElementWrapper);
        }

        private Task Select(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.OnBackgroundThread(() =>
            {
                ElementWrapper.AutomationElement.Patterns.SelectionItem.Pattern.Select();
            }, ElementWrapper);
        }

        private Task Expand(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.OnBackgroundThread(() =>
            {
                ElementWrapper.AutomationElement.Patterns.ExpandCollapse.Pattern.Expand();
            }, ElementWrapper);
        }

        private Task Collapse(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.OnBackgroundThread(() =>
            {
                ElementWrapper.AutomationElement.Patterns.ExpandCollapse.Pattern.Collapse();
            }, ElementWrapper);
        }
    }
}
