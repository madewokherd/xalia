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

        private static readonly Dictionary<string, ControlType> name_to_control_type;
        private static readonly UiDomEnum[] control_type_to_enum;

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
                else if (name == "text")
                    names = new[] { "text", "text_box", "textbox" };
                else if (name.Contains("_"))
                    names = new[] { name, name.Replace("_", "") };
                else
                    names = new[] { name };
                control_type_to_enum[i] = new UiDomEnum(names);
                foreach (string rolename in names)
                    name_to_control_type[rolename] = (ControlType)i;
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
            else if (value is bool b)
            {
                new_value = UiDomBoolean.FromBool(b);
            }
            else if (value is string s)
            {
                new_value = new UiDomString(s);
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
                return property_value[id];
            }
            return UiDomUndefined.Instance;
        }

        private object GetRawProperty(string name, PropertyId id, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            depends_on.Add((this, new IdentifierExpression(name)));
            if (property_known.TryGetValue(id, out var known) && known)
            {
                return property_raw_value[id];
            }
            return UiDomUndefined.Instance;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "role":
                case "control_type":
                case "controltype":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_control_type";
                case "uia_controltype":
                case "uia_control_type":
                    return GetProperty("uia_control_type", Root.Automation.PropertyLibrary.Element.ControlType, depends_on);
                case "enabled":
                case "is_enabled":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_enabled";
                case "uia_enabled":
                case "uia_is_enabled":
                    return GetProperty("uia_enabled", Root.Automation.PropertyLibrary.Element.IsEnabled, depends_on);
                case "selected":
                case "is_selected":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_selected";
                case "uia_selected":
                case "uia_is_selected":
                    return GetProperty("uia_selected", Root.Automation.PropertyLibrary.Element.IsEnabled, depends_on);
                case "bounds":
                case "bounding_rectangle":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_bounding_rectangle";
                case "uia_bounding_rectangle":
                    return GetProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on);
                case "x":
                case "abs_x":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_x";
                case "uia_x":
                case "uia_abs_x":
                    {
                        if (GetRawProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on) is System.Drawing.Rectangle r)
                        {
                            return new UiDomInt(r.Left);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "y":
                case "abs_y":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_y";
                case "uia_y":
                case "uia_abs_y":
                    {
                        if (GetRawProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on) is System.Drawing.Rectangle r)
                        {
                            return new UiDomInt(r.Top);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "width":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_width";
                case "uia_width":
                    {
                        if (GetRawProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on) is System.Drawing.Rectangle r)
                        {
                            return new UiDomInt(r.Width);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "height":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_height";
                case "uia_height":
                    {
                        if (GetRawProperty("uia_bounding_rectangle", Root.Automation.PropertyLibrary.Element.BoundingRectangle, depends_on) is System.Drawing.Rectangle r)
                        {
                            return new UiDomInt(r.Height);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "name":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_name";
                case "uia_name":
                    return GetProperty("uia_name", Root.Automation.PropertyLibrary.Element.Name, depends_on);
                case "focused":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_focused";
                case "uia_focused":
                    depends_on.Add((this, new IdentifierExpression("uia_focused")));
                    return UiDomBoolean.FromBool(ElementWrapper.Equals(Root.FocusedElement));
                case "foreground":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "msaa_foreground";
                case "msaa_foreground":
                    depends_on.Add((this, new IdentifierExpression("msaa_foreground")));
                    return UiDomBoolean.FromBool(ElementWrapper.Equals(Root.ForegroundElement));
                case "active":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "win32_active";
                case "win32_active":
                    depends_on.Add((this, new IdentifierExpression("win32_active")));
                    return UiDomBoolean.FromBool(ElementWrapper.Equals(Root.ActiveElement));
                case "set_focus":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_set_focus";
                case "uia_set_focus":
                    {
                        return new UiDomRoutineAsync(this, "uia_set_focus", DoSetFocus);
                    }
                case "focused_element":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_focused_element";
                case "uia_focused_element":
                    depends_on.Add((Root, new IdentifierExpression("uia_focused_element")));
                    return (UiDomValue)Root.LookupAutomationElement(Root.FocusedElement) ?? UiDomUndefined.Instance;
                case "foreground_element":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "msaa_foreground_element";
                case "msaa_foreground_element":
                    depends_on.Add((Root, new IdentifierExpression("msaa_foreground_element")));
                    return (UiDomValue)Root.LookupAutomationElement(Root.ForegroundElement) ?? UiDomUndefined.Instance;
                case "active_element":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "win32_active_element";
                case "win32_active_element":
                    depends_on.Add((Root, new IdentifierExpression("win32_active_element")));
                    return (UiDomValue)Root.LookupAutomationElement(Root.ActiveElement) ?? UiDomUndefined.Instance;
                case "menu_mode":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_menu_mode";
                case "uia_menu_mode":
                    depends_on.Add((Root, new IdentifierExpression("uia_menu_mode")));
                    return UiDomBoolean.FromBool(Root.UiaMenuMode);
                case "opened_menu":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_opened_menu";
                case "uia_opened_menu":
                    return Root.EvaluateIdentifier(id, Root, depends_on);
                case "in_menu":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_in_menu";
                case "uia_in_menu":
                    return Root.EvaluateIdentifier(id, Root, depends_on);
                case "in_submenu":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_in_submenu";
                case "uia_in_submenu":
                    return Root.EvaluateIdentifier(id, Root, depends_on);
                case "is_uia_element":
                    return UiDomBoolean.True;
                case "is_spi_element":
                case "is_atspi_element":
                case "is_at_spi_element":
                    return UiDomBoolean.False;
                case "invoke":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_invoke";
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
                case "select":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "uia_select";
                case "uia_select":
                    depends_on.Add((this, new IdentifierExpression("uia_supported_patterns")));
                    if (!(supported_patterns is null) && supported_patterns.Contains(Root.Automation.PatternLibrary.SelectionItemPattern))
                    {
                        return new UiDomRoutineAsync(this, "uia_select", Select);
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
    }
}
