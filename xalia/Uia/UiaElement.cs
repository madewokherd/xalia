using FlaUI.Core.Definitions;
using FlaUI.Core.Exceptions;
using FlaUI.Core.Identifiers;
using FlaUI.Core.WindowsAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Uia.Win32;
using Xalia.UiDom;
using static Xalia.Interop.Win32;
using IServiceProvider = Xalia.Interop.Win32.IServiceProvider;

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

        public string ProcessName { get; private set; }

        Dictionary<PropertyId, bool> fetching_property = new Dictionary<PropertyId, bool>();
        Dictionary<PropertyId, bool> property_known = new Dictionary<PropertyId, bool>();
        Dictionary<PropertyId, object> property_raw_value = new Dictionary<PropertyId, object>();
        Dictionary<PropertyId, UiDomValue> property_value = new Dictionary<PropertyId, UiDomValue>();

        bool watching_children;
        bool refreshing_children;
        bool inflight_structure_changed;

        bool polling_children;
        CancellationTokenSource children_poll_token;

        PatternId[] supported_patterns;
        bool fetching_supported_patterns;

        Dictionary<PropertyId, bool> polling_property = new Dictionary<PropertyId, bool>(0);
        Dictionary<PropertyId, CancellationTokenSource> property_poll_token = new Dictionary<PropertyId, CancellationTokenSource>(0);

        private double offset_remainder;

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
                "automation_id", "uia_automation_id",
                "enabled", "uia_enabled",
                "is_enabled", "uia_enabled",
                "focusable", "uia_keyboard_focusable",
                "keyboard_focusable", "uia_keyboard_focusable",
                "is_keyboard_focusable", "uia_keyboard_focusable",
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
                "orientation", "uia_orientation",
                "framework_id", "uia_framework_id",
                "frameworkid", "uia_framework_id",
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
                "application_version", "acc2_application_version",
                "toolkit_name", "acc2_toolkit_name",
                "toolkit_version", "acc2_toolkit_version",
                "set_foreground_window", "win32_set_foreground_window",
                "application_name", "win32_process_name",
                "process_name", "win32_process_name",
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
            {
                refreshing_children = false;
                inflight_structure_changed = false;
                return;
            }

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

            // Remove any remaining missing children
            while (i < Children.Count && Children[i] is UiaElement)
                RemoveChild(i);

            // Add any new children
            i = 0;
            foreach (var new_child in children)
            {
                if (Children.Count <= i || !ElementMatches(Children[i], new_child.UniqueId))
                {
                    if (!(Root.LookupAutomationElement(new_child) is null))
                    {
                        // Child element is a duplicate of another element somewhere in the tree.
                        continue;
                    }
                    AddChild(i, new UiaElement(new_child));
                }
                i += 1;
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
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"OnChildrenChanged for {this}");
            UpdateChildren();
        }

        internal void WatchChildren()
        {
            if (watching_children)
                return;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"WatchChildren for {this}");
            watching_children = true;
            Utils.RunTask(RefreshChildren());
        }

        internal void UnwatchChildren()
        {
            if (!watching_children)
                return;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {this}");
            watching_children = false;
            if (children_poll_token != null)
            {
                children_poll_token.Cancel();
                children_poll_token = null;
            }
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i] is UiaElement)
                    RemoveChild(i);
            }
        }

        private async Task PollProperty(string name, PropertyId propid)
        {
            if (!polling_property.TryGetValue(propid, out bool polling) || !polling)
                return;

            await FetchPropertyAsync(name, propid);

            var poll_token = new CancellationTokenSource();
            property_poll_token[propid] = poll_token;

            try
            {
                await Task.Delay(2000, poll_token.Token);
            }
            catch (TaskCanceledException)
            {
                property_poll_token[propid] = null;
                return;
            }

            property_poll_token[propid] = null;
            Utils.RunTask(PollProperty(name, propid));
        }

        private async Task PollChildren()
        {
            if (!polling_children)
                return;

            await RefreshChildren();

            children_poll_token = new CancellationTokenSource();

            try
            {
                await Task.Delay(2000, children_poll_token.Token);
            }
            catch (TaskCanceledException)
            {
                children_poll_token = null;
                return;
            }

            children_poll_token = null;
            Utils.RunTask(PollChildren());
        }

        protected override void DeclarationsChanged(Dictionary<string, (GudlDeclaration, UiDomValue)> all_declarations, HashSet<(UiDomElement, GudlExpression)> dependencies)
        {
            if (all_declarations.TryGetValue("recurse", out var recurse) && recurse.Item2.ToBool())
                WatchChildren();
            else
                UnwatchChildren();

            if (ElementWrapper.Hwnd != IntPtr.Zero)
            {
                int win32_element = -1, win32_listview = -1, win32_tabcontrol = -1, win32_trackbar = -1;

                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i] is Win32Trackbar)
                        win32_trackbar = i;
                    else if (Children[i] is Win32ListView)
                        win32_listview = i;
                    else if (Children[i] is Win32TabControl)
                        win32_tabcontrol = i;
                    else if (Children[i] is Win32Element)
                        win32_element = i;
                }

                if (all_declarations.TryGetValue("win32_use_element", out var use_element) && use_element.Item2.ToBool())
                {
                    if (win32_element == -1)
                    {
                        AddChild(Children.Count, new Win32Element(ElementWrapper.Hwnd, Root));
                    }
                }
                else
                {
                    if (win32_element != -1)
                    {
                        RemoveChild(win32_element);
                    }
                }

                if (all_declarations.TryGetValue("win32_use_tabcontrol", out var use_tabcontrol) && use_tabcontrol.Item2.ToBool())
                {
                    if (win32_tabcontrol == -1)
                    {
                        AddChild(Children.Count, new Win32TabControl(ElementWrapper.Hwnd, Root));
                    }
                }
                else
                {
                    if (win32_tabcontrol != -1)
                    {
                        RemoveChild(win32_tabcontrol);
                    }
                }

                if (all_declarations.TryGetValue("win32_use_trackbar", out var use_trackbar) && use_trackbar.Item2.ToBool())
                {
                    if (win32_trackbar == -1)
                    {
                        AddChild(Children.Count, new Win32Trackbar(ElementWrapper.Hwnd, Root));
                    }
                }
                else
                {
                    if (win32_trackbar != -1)
                    {
                        RemoveChild(win32_trackbar);
                    }
                }

                if (all_declarations.TryGetValue("win32_use_listview", out var use_listview) && use_listview.Item2.ToBool())
                {
                    if (win32_listview == -1)
                    {
                        AddChild(Children.Count, new Win32ListView(ElementWrapper.Hwnd, Root));
                    }
                }
                else
                {
                    if (win32_listview != -1)
                    {
                        RemoveChild(win32_listview);
                    }
                }
            }

            if (watching_children && all_declarations.TryGetValue("poll_children", out var poll_children) && poll_children.Item2.ToBool())
            {
                if (!polling_children)
                {
                    polling_children = true;
                    Utils.RunTask(PollChildren());
                }
            }
            else
            {
                if (polling_children)
                {
                    polling_children = false;
                    if (children_poll_token != null)
                    {
                        children_poll_token.Cancel();
                        children_poll_token = null;
                    }
                }
            }

            foreach (var kvp in all_declarations)
            {
                if (!kvp.Key.StartsWith("poll_") || !kvp.Value.Item2.ToBool())
                    continue;

                var prop_name = kvp.Key.Substring(5);

                if (!Root.names_to_property.TryGetValue(prop_name, out var propid))
                    continue;

                if (!polling_property.TryGetValue(propid, out bool polling) || !polling)
                {
                    polling_property[propid] = true;
                    Utils.RunTask(PollProperty(prop_name, propid));
                }
            }

            // Search for properties to stop polling
            foreach (var kvp in new List<KeyValuePair<PropertyId, bool>>(polling_property))
            {
                var propid = kvp.Key;
                string prop_name = Root.properties_to_name[propid];

                if (!kvp.Value)
                    // not being currently polled
                    continue;

                if (all_declarations.TryGetValue("poll_"+prop_name, out var polling) &&
                    polling.Item2.ToBool())
                    // still being polled
                    continue;

                polling_property[propid] = false;
                if (property_poll_token.TryGetValue(propid, out var token) && token != null)
                {
                    token.Cancel();
                    property_poll_token[propid] = null;
                }
            }

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
            else if (name == "uia_orientation" && value is int ori)
            {
                value = (OrientationType)ori;
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
            else if (value is OrientationType or)
            {
                switch (or)
                {
                    case OrientationType.None:
                        new_value = new UiDomEnum(new[] { "none" });
                        break;
                    case OrientationType.Horizontal:
                        new_value = new UiDomEnum(new[] { "horizontal" });
                        break;
                    case OrientationType.Vertical:
                        new_value = new UiDomEnum(new[] { "vertical" });
                        break;
                    default:
                        new_value = UiDomUndefined.Instance;
                        break;
                }
            }
            else
            {
                if (!(value is null))
                    Utils.DebugWriteLine($"Warning: value for {name} has unsupported type {value.GetType()}");
                new_value = UiDomUndefined.Instance;
            }

            UiDomValue old_value;
            if (!property_value.TryGetValue(propid, out old_value))
                old_value = UiDomUndefined.Instance;

            property_known[propid] = true;
            if (!old_value.Equals(new_value))
            {
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{DebugId}.{name}: {new_value}");
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
            try
            {
                supported_patterns = await Root.CommandThread.GetSupportedPatterns(ElementWrapper);

                PropertyChanged("uia_supported_patterns");
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
        }

        static bool DebugExceptions = Environment.GetEnvironmentVariable("XALIA_DEBUG_EXCEPTIONS") != "0";

        internal static bool IsExpectedException(Exception e)
        {
#if DEBUG
            if (DebugExceptions)
            {
                Utils.DebugWriteLine("WARNING: Exception:");
                Utils.DebugWriteLine(e);
            }
#endif
            if (e is FlaUI.Core.Exceptions.NotSupportedException)
            {
                return true;
            }
            if (e is InvalidOperationException)
            {
                return true;
            }
            if (e is COMException com)
            {
                switch (com.ErrorCode)
                {
                    case unchecked((int)0x80004005): // E_FAIL
                    case unchecked((int)0x80010012): // RPC_E_SERVER_DIED_DNE
                    case unchecked((int)0x80010108): // RPC_E_DISCONNECTED
                    case unchecked((int)0x800401FD): // CO_E_OBJNOTCONNECTED
                    case unchecked((int)0x80040201): // EVENT_E_ALL_SUBSCRIBERS_FAILED
                    case unchecked((int)0x800706BA): // RPC_E_SERVER_UNAVAILABLE
                    case unchecked((int)0x80131505): // UIA_E_TIMEOUT
                        return true;
                }
            }
#if DEBUG
            return false;
#else
            if (DebugExceptions)
            {
                Utils.DebugWriteLine("WARNING: Exception ignored:");
                Utils.DebugWriteLine(e);
            }
            return true;
#endif
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
                if (children_poll_token != null)
                {
                    children_poll_token.Cancel();
                    children_poll_token = null;
                }
                polling_children = false;
                while (property_poll_token.Count != 0)
                {
                    var kvp = property_poll_token.First();
                    if (!(kvp.Value is null))
                        kvp.Value.Cancel();
                    property_poll_token.Remove(kvp.Key);
                }
                property_poll_token.Clear();
                polling_property.Clear();
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
                case "uia_automation_id":
                    return GetProperty("uia_automation_id", Root.Automation.PropertyLibrary.Element.AutomationId, depends_on);
                case "uia_enabled":
                case "uia_is_enabled":
                    return GetProperty("uia_enabled", Root.Automation.PropertyLibrary.Element.IsEnabled, depends_on);
                case "uia_is_keyboard_focusable":
                case "uia_keyboard_focusable":
                    return GetProperty("uia_keyboard_focusable", Root.Automation.PropertyLibrary.Element.IsKeyboardFocusable, depends_on);
                case "uia_has_keyboard_focus":
                    return GetProperty("uia_has_keyboard_focus", Root.Automation.PropertyLibrary.Element.HasKeyboardFocus, depends_on);
                case "uia_offscreen":
                case "uia_is_offscreen":
                    return GetProperty("uia_offscreen", Root.Automation.PropertyLibrary.Element.IsOffscreen, depends_on);
                case "uia_visible":
                    return UiDomBoolean.FromBool(!GetProperty("uia_offscreen", Root.Automation.PropertyLibrary.Element.IsOffscreen, depends_on).ToBool());
                case "uia_selected":
                case "uia_is_selected":
                    return GetProperty("uia_selected", Root.Automation.PropertyLibrary.SelectionItem.IsSelected, depends_on);
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
                case "collapsed":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                        var st = GetRawProperty("uia_expand_collapse_state", Root.Automation.PropertyLibrary.ExpandCollapse.ExpandCollapseState, depends_on);
                        if (st is ExpandCollapseState ecs)
                        {
                            return UiDomBoolean.FromBool(ecs == ExpandCollapseState.Collapsed);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "expanded":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                        var st = GetRawProperty("uia_expand_collapse_state", Root.Automation.PropertyLibrary.ExpandCollapse.ExpandCollapseState, depends_on);
                        if (st is ExpandCollapseState ecs)
                        {
                            return UiDomBoolean.FromBool(ecs == ExpandCollapseState.Expanded);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "uia_orientation":
                    return GetProperty("uia_orientation", Root.Automation.PropertyLibrary.Element.Orientation, depends_on);
                case "horizontal":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                        var orientation = GetRawProperty("uia_orientation", Root.Automation.PropertyLibrary.Element.Orientation, depends_on);
                        if (orientation is OrientationType ore)
                        {
                            return UiDomBoolean.FromBool(ore == OrientationType.Horizontal);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "vertical":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                        var orientation = GetRawProperty("uia_orientation", Root.Automation.PropertyLibrary.Element.Orientation, depends_on);
                        if (orientation is OrientationType ore)
                        {
                            return UiDomBoolean.FromBool(ore == OrientationType.Vertical);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "uia_framework_id":
                    return GetProperty("uia_framework_id", Root.Automation.PropertyLibrary.Element.FrameworkId, depends_on);
                case "msaa_role":
                    return GetProperty("msaa_role", Root.Automation.PropertyLibrary.LegacyIAccessible.Role, depends_on);
                case "aria_role":
                    return GetProperty("aria_role", Root.Automation.PropertyLibrary.Element.AriaRole, depends_on);
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
                case "uia_adjust_scroll_container":
                    depends_on.Add((this, new IdentifierExpression("uia_supported_patterns")));
                    if (!(supported_patterns is null) && supported_patterns.Contains(Root.Automation.PatternLibrary.ScrollPattern))
                    {
                        return new UiaAdjustScrollContainer(this);
                    }
                    return UiDomUndefined.Instance;
                case "win32_set_foreground_window":
                    if (ElementWrapper.Hwnd != IntPtr.Zero)
                    {
                        return new UiDomRoutineSync(this, "win32_set_foreground_window", Win32SetForegroundWindow);
                    }
                    return UiDomUndefined.Instance;
                case "win32_process_name":
                    if (ElementWrapper.Pid != 0)
                    {
                        if (ProcessName is null)
                        {
                            using (var process = Process.GetProcessById(ElementWrapper.Pid))
                                ProcessName = process.ProcessName;
                        }
                        return new UiDomString(ProcessName);
                    }
                    return UiDomUndefined.Instance;
            }

            {
                var value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;
            }

            switch (id)
            {
                case "toolkit":
                    {
                        var acc2_toolkit = EvaluateIdentifierCore("acc2_toolkit_name", root, depends_on);
                        if (!acc2_toolkit.Equals(UiDomUndefined.Instance))
                        {
                            return acc2_toolkit;
                        }
                        return EvaluateIdentifierCore("uia_framework_id", root, depends_on);
                    }
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

        protected override void DumpProperties()
        {
            foreach (var kvp in property_known)
            {
                if (kvp.Value && property_value.TryGetValue(kvp.Key, out var val))
                {
                    Utils.DebugWriteLine($"  {Root.properties_to_name[kvp.Key]}: {val}");
                }
            }
            if (ElementWrapper.Equals(Root.FocusedElement))
                Utils.DebugWriteLine($"  uia_focused: true");
            if (ElementWrapper.Equals(Root.ForegroundElement))
                Utils.DebugWriteLine($"  msaa_foreground: true");
            if (ElementWrapper.Equals(Root.ActiveElement))
                Utils.DebugWriteLine($"  win32_active: true");
            if (!(supported_patterns is null))
                foreach (var patternid in supported_patterns)
                    Utils.DebugWriteLine($"  supported pattern: {patternid.Name}");
            if (!(ProcessName is null))
                Utils.DebugWriteLine($"  win32_process_name: {ProcessName}");
            base.DumpProperties();
        }

        private void Win32SetForegroundWindow(UiDomRoutineSync obj)
        {
            SetForegroundWindow(ElementWrapper.Hwnd);
        }

        public override async Task<(bool, int, int)> GetClickablePoint()
        {
            var result = await base.GetClickablePoint();
            if (result.Item1)
                return result;

            var rc = await Root.CommandThread.GetPropertyValue(ElementWrapper, Root.Automation.PropertyLibrary.Element.BoundingRectangle);
            if (!(rc is null) && rc is System.Drawing.Rectangle r)
            {
                int x = r.Left + r.Width / 2;
                int y = r.Right + r.Height / 2;
                return (true, x, y);
            }

            try
            {
                var clickable = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    return ElementWrapper.AutomationElement.GetClickablePoint();
                }, ElementWrapper.Pid+1);

                int x = clickable.X;
                int y = clickable.Y;
                return (true, x, y);
            }
            catch (NoClickablePointException) { }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }

            return (false, 0, 0);
        }

        private async Task MsaaDefaultAction(UiDomRoutineAsync obj)
        {
            var supported = await Root.CommandThread.OnBackgroundThread(() =>
            {
                try
                {
                    ElementWrapper.AutomationElement.Patterns.LegacyIAccessible.Pattern.DoDefaultAction();
                }
                catch (Exception e)
                {
                    if (!IsExpectedException(e))
                        throw;
                    return false;
                }

                return true;
            }, ElementWrapper.Pid+1);

            if (!supported)
            {
                Utils.DebugWriteLine($"WARNING: msaa_do_default_action not supported on {this}");
            }
        }

        private Task DoSetFocus(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.OnBackgroundThread(() =>
            {
                ElementWrapper.AutomationElement.Focus();
            }, ElementWrapper.Pid+1);
        }

        private async Task Invoke(UiDomRoutineAsync obj)
        {
            var supported = await Root.CommandThread.OnBackgroundThread(() =>
            {
                try
                {
                    ElementWrapper.AutomationElement.Patterns.Invoke.Pattern.Invoke();
                }
                catch (Exception e)
                {
                    if (!IsExpectedException(e))
                        throw;
                }

                return true;
            }, ElementWrapper.Pid+1);

            if (!supported)
            {
                Utils.DebugWriteLine($"WARNING: uia_invoke not supported on {this}");
            }
        }

        private Task Select(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.OnBackgroundThread(() =>
            {
                try
                {
                    ElementWrapper.AutomationElement.Patterns.SelectionItem.Pattern.Select();
                }
                catch (Exception e)
                {
                    if (!IsExpectedException(e))
                        throw;
                }
            }, ElementWrapper.Pid+1);
        }

        private Task Expand(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.OnBackgroundThread(() =>
            {
                try
                {
                    ElementWrapper.AutomationElement.Patterns.ExpandCollapse.Pattern.Expand();
                }
                catch (Exception e)
                {
                    if (!IsExpectedException(e))
                        throw;
                }
            }, ElementWrapper.Pid+1);
        }

        private Task Collapse(UiDomRoutineAsync obj)
        {
            return Root.CommandThread.OnBackgroundThread(() =>
            {
                try
                {
                    ElementWrapper.AutomationElement.Patterns.ExpandCollapse.Pattern.Collapse();
                }
                catch (Exception e)
                {
                    if (!IsExpectedException(e))
                        throw;
                }
            }, ElementWrapper.Pid+1);
        }

        private bool GetScrollInfoBackground(int flags, out IntPtr hwnd, out int which, out SCROLLINFO info)
        {
            hwnd = default;
            which = default;
            info = default;
            if (ElementWrapper.Hwnd != IntPtr.Zero)
            {
                hwnd = ElementWrapper.Hwnd;
                which = SB_CTL;
            }
            else if (Parent is UiaElement pue && pue.ElementWrapper.Hwnd != IntPtr.Zero)
            {
                hwnd = pue.ElementWrapper.Hwnd;
                string automation_id;
                try
                {
                    automation_id = ElementWrapper.AutomationElement.AutomationId;
                }
                catch (Exception e)
                {
                    if (!IsExpectedException(e))
                        throw;
                    return false;
                }
                switch (automation_id)
                {
                    case "NonClientHorizontalScrollBar":
                        which = SB_HORZ;
                        break;
                    case "NonClientVerticalScrollBar":
                        which = SB_VERT;
                        break;
                    default:
                        return false;
                }
            }
            else
            {
                return false;
            }

            info.cbSize = Marshal.SizeOf<SCROLLINFO>();
            info.fMask = flags;
            return GetScrollInfo(hwnd, which, ref info);
        }

        public override async Task<double> GetMinimumIncrement()
        {
            try
            {
                var result = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    // Try Win32 first

                    if (GetScrollInfoBackground(SIF_PAGE, out var unused, out var unused2, out var info))
                    {
                        return Math.Max(1.0, info.nPage / 10.0);
                    }

                    var range = ElementWrapper.AutomationElement.Patterns.RangeValue.Pattern;
                    return Math.Max(range.SmallChange, range.LargeChange / 10);
                }, ElementWrapper.Pid+1);
                if (result != 0)
                    return result;
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
            return await base.GetMinimumIncrement();
        }

        public override async Task OffsetValue(double ofs)
        {
            try
            {
                await Root.CommandThread.OnBackgroundThread(() =>
                {
                    // Try Win32 first
                    if (GetScrollInfoBackground(SIF_POS|SIF_PAGE|SIF_RANGE, out var hwnd, out var which, out var info))
                    {
                        var scroll_current = info.nPos;

                        var scroll_new = scroll_current + ofs + offset_remainder;

                        int max;
                        if (info.nPage == 0)
                            max = info.nMax;
                        else
                            max = info.nMax - info.nPage + 1;

                        if (scroll_new > max)
                            scroll_new = max;
                        else if (scroll_new < info.nMin)
                            scroll_new = info.nMin;

                        int scroll_new_int = (int)Math.Round(scroll_new);

                        if (scroll_new_int != scroll_current)
                        {
                            info.fMask = SIF_POS;
                            info.nPos = scroll_new_int;
                            SetScrollInfo(hwnd, which, ref info, true);

                            // We have to also send a WM_HSCROLL or WM_VSCROLL for the app to notice
                            int msg;
                            IntPtr ctrl_hwnd, msg_hwnd;
                            switch (which)
                            {
                                case SB_CTL:
                                    msg = ((int)GetWindowLong(hwnd, GWL_STYLE) & SBS_VERT) == SBS_VERT ? WM_VSCROLL : WM_HSCROLL;
                                    ctrl_hwnd = hwnd;
                                    msg_hwnd = GetAncestor(ctrl_hwnd, GA_PARENT);
                                    break;
                                case SB_HORZ:
                                    msg = WM_HSCROLL;
                                    ctrl_hwnd = IntPtr.Zero;
                                    msg_hwnd = hwnd;
                                    break;
                                case SB_VERT:
                                    msg = WM_VSCROLL;
                                    ctrl_hwnd = IntPtr.Zero;
                                    msg_hwnd = hwnd;
                                    break;
                                default:
                                    throw new InvalidOperationException();
                            }

                            SendMessageW(msg_hwnd, msg, MAKEWPARAM(SB_THUMBTRACK, (ushort)scroll_new_int), ctrl_hwnd);
                            SendMessageW(msg_hwnd, msg, MAKEWPARAM(SB_THUMBPOSITION, (ushort)scroll_new_int), ctrl_hwnd);
                            SendMessageW(msg_hwnd, msg, MAKEWPARAM(SB_ENDSCROLL, 0), ctrl_hwnd);
                        }

                        offset_remainder = scroll_new - scroll_new_int;
                        return;
                    }

                    var range = ElementWrapper.AutomationElement.Patterns.RangeValue.Pattern;

                    var current_value = range.Value;

                    var new_value = current_value + ofs;

                    if (ofs > 0)
                    {
                        var maximum_value = range.Maximum;

                        if (new_value > maximum_value)
                            new_value = maximum_value;
                    }
                    else
                    {
                        var minimum_value = range.Minimum;

                        if (new_value < minimum_value)
                            new_value = minimum_value;
                    }

                    if (new_value != current_value)
                        range.SetValue(new_value);
                }, ElementWrapper.Pid+1);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
        }
    }
}
