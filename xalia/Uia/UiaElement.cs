﻿using FlaUI.Core.Definitions;
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
        static string[] tracked_properties = { "recurse", "poll_children",
            "win32_use_element", "win32_use_trackbar", "win32_use_tabcontrol", "win32_use_listview",
            "msaa_use_element",
        };

        public UiaElement(UiaElementWrapper wrapper) : base(wrapper.Connection)
        {
            ElementWrapper = wrapper;
            RegisterTrackedProperties(tracked_properties);
            RegisterTrackedProperties(Root.PropertyPollNames);
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

        PatternId[] supported_patterns;
        bool fetching_supported_patterns;

        Dictionary<PropertyId, bool> polling_property = new Dictionary<PropertyId, bool>(0);
        Dictionary<PropertyId, CancellationTokenSource> property_poll_token = new Dictionary<PropertyId, CancellationTokenSource>(0);

        private double offset_remainder;

        string application_name, application_version, toolkit_name, toolkit_version;
        bool fetching_application_name, fetching_application_version, fetching_toolkit_name, fetching_toolkit_version;
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

        private static readonly Dictionary<string, string> property_aliases;

        private bool msaa_use_element;
        private int msaa_use_element_change_count;

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

            var children = (await Root.CommandThread.GetChildren(ElementWrapper)).ToList();

            if (!watching_children)
            {
                refreshing_children = false;
                inflight_structure_changed = false;
                return;
            }

            // Ignore any duplicate children
            HashSet<string> seen_children = new HashSet<string>();
            int i = 0;
            while (i < children.Count)
            {
                if (!seen_children.Add(children[i].UniqueId))
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
            if (polling_children)
                PollProperty(new IdentifierExpression("children"), RefreshChildren, 2000);
        }

        internal void UnwatchChildren()
        {
            if (!watching_children)
                return;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {this}");
            watching_children = false;
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i] is UiaElement)
                    RemoveChild(i);
            }
            if (polling_children)
                EndPollProperty(new IdentifierExpression("children"));
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

        protected override void TrackedPropertyChanged(string name, UiDomValue new_value)
        {
            switch (name)
            {
                case "recurse":
                    if (new_value.ToBool())
                        WatchChildren();
                    else
                        UnwatchChildren();
                    break;
                case "poll_children":
                    if (new_value.ToBool() != polling_children)
                    {
                        polling_children = new_value.ToBool();
                        if (watching_children)
                        {
                            if (polling_children)
                                PollProperty(new IdentifierExpression("poll_children"), RefreshChildren, 2000);
                            else
                                EndPollProperty(new IdentifierExpression("poll_children"));
                        }
                    }
                    break;
                case "win32_use_element":
                    UseElementPropertyChanged(new_value,
                        // have to do slow check because Win32Element has subclasses
                        (UiDomElement e) => { return e.GetType() == typeof(Win32Element); },
                        () => { return new Win32Element(ElementWrapper.Hwnd, Root); });
                    break;
                case "win32_use_trackbar":
                    UseElementPropertyChanged(new_value,
                        (UiDomElement e) => { return e is Win32Trackbar; },
                        () => { return new Win32Trackbar(ElementWrapper.Hwnd, Root); });
                    break;
                case "win32_use_tabcontrol":
                    UseElementPropertyChanged(new_value,
                        (UiDomElement e) => { return e is Win32TabControl; },
                        () => { return new Win32TabControl(ElementWrapper.Hwnd, Root); });
                    break;
                case "win32_use_listview":
                    UseElementPropertyChanged(new_value,
                        (UiDomElement e) => { return e is Win32ListView; },
                        () => { return new Win32ListView(ElementWrapper.Hwnd, Root); });
                    break;
                case "msaa_use_element":
                    {
                        bool use_msaa = new_value.ToBool();
                        if (msaa_use_element != use_msaa)
                        {
                            msaa_use_element = use_msaa;
                            msaa_use_element_change_count++;

                            if (use_msaa)
                                Utils.RunTask(AddMsaaElement());
                            else
                            {
                                for (int i=0; i<Children.Count; i++)
                                {
                                    if (Children[i] is MsaaElement)
                                    {
                                        RemoveChild(i);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }
            if (name.StartsWith("poll_") && Root.names_to_property.TryGetValue(name.Substring(5), out var propid))
            {
                var prop_name = Root.properties_to_name[propid];

                bool new_polling = new_value.ToBool();
                bool old_polling = polling_property.TryGetValue(propid, out bool polling) && polling;

                if (new_polling != old_polling)
                {
                    polling_property[propid] = new_polling;
                    if (new_polling)
                        Utils.RunTask(PollProperty(prop_name, propid));
                    else
                    {
                        if (property_poll_token.TryGetValue(propid, out var token) && token != null)
                        {
                            token.Cancel();
                            property_poll_token[propid] = null;
                        }
                    }
                }
            }
            base.TrackedPropertyChanged(name, new_value);
        }

        private void UseElementPropertyChanged(UiDomValue new_value, Predicate<UiDomElement> predicate, Func<Win32Element> ctor)
        {
            bool new_use_element = new_value.ToBool();
            int idx = Children.FindIndex(predicate);
            bool old_use_element = (idx != -1);

            if (new_use_element == old_use_element)
                return;

            if (new_use_element)
                AddChild(Children.Count, ctor());
            else
                RemoveChild(idx);
        }

        private async Task AddMsaaElement()
        {
            MsaaElementWrapper wrapper;
            int old_change_count = msaa_use_element_change_count;
            try
            {
                wrapper = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    return MsaaElementWrapper.FromUiaElementBackground(ElementWrapper);
                }, ElementWrapper);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }

            if (old_change_count != msaa_use_element_change_count)
                return;

            AddChild(Children.Count, new MsaaElement(wrapper, Root));
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
                new_value = MsaaElement.MsaaRoleToValue((int)role);
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
                    case "acc2_application_name":
                        {
                            if (!fetching_application_name)
                            {
                                fetching_application_name = true;
                                Utils.RunTask(FetchApplicationName());
                            }
                            break;
                        }
                    case "acc2_application_version":
                        {
                            if (!fetching_application_version)
                            {
                                fetching_application_version = true;
                                Utils.RunTask(FetchApplicationVersion());
                            }
                            break;
                        }
                    case "acc2_toolkit_name":
                        {
                            if (!fetching_toolkit_name)
                            {
                                fetching_toolkit_name = true;
                                Utils.RunTask(FetchToolkitName());
                            }
                            break;
                        }
                    case "acc2_toolkit_version":
                        {
                            if (!fetching_toolkit_version)
                            {
                                fetching_toolkit_version = true;
                                Utils.RunTask(FetchToolkitVersion());
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

        internal async Task PropertyMaybeChanged(PropertyId propid)
        {
            if ((property_known.TryGetValue(propid, out var known) && known) ||
                (fetching_property.TryGetValue(propid, out var fetching) && fetching))
            {
                await FetchPropertyAsync(Root.properties_to_name[propid], propid);
            }
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
                    case unchecked((int)0x80020003): // DISP_E_MEMBERNOTFOUND
                    case unchecked((int)0x800401FD): // CO_E_OBJNOTCONNECTED
                    case unchecked((int)0x80040201): // EVENT_E_ALL_SUBSCRIBERS_FAILED
                    case unchecked((int)0x800706B5): // RPC_S_UNKNOWN_IF
                    case unchecked((int)0x800706BA): // RPC_E_SERVER_UNAVAILABLE
                    case unchecked((int)0x800706BE): // RPC_S_CALL_FAILED
                    case unchecked((int)0x80131505): // UIA_E_TIMEOUT
                        return true;
                }
            }
            if (e is UnauthorizedAccessException)
            {
                return true;
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

        private IAccessibleApplication QueryAccessibleApplicationBackground()
        {
            IntPtr pIAA;

            try
            {
                object acc = Root.GetIAccessibleBackground(ElementWrapper.AutomationElement, out var _unused);

                if (acc is null)
                    return null;

                IServiceProvider sp = (IServiceProvider)acc;

                if (sp is null)
                {
                    // Unsure how this can happen
                    return null;
                }

                Guid iid = IID_IAccessibleApplication;

                pIAA = sp.QueryService(ref iid, ref iid);
            }
            catch (InvalidCastException) // E_NOINTERFACE
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (Exception e)
            {
                if (IsExpectedException(e))
                    return null;
                throw;
            }

            return (IAccessibleApplication)Marshal.GetTypedObjectForIUnknown(pIAA, typeof(IAccessibleApplication));
        }

        private async Task FetchApplicationName()
        {
            try
            {
                application_name = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    return QueryAccessibleApplicationBackground()?.appName;
                }, ElementWrapper);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
        }

        private async Task FetchApplicationVersion()
        {
            try
            {
                application_version = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    return QueryAccessibleApplicationBackground()?.appVersion;
                }, ElementWrapper);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
        }

        private async Task FetchToolkitName()
        {
            try
            {
                toolkit_name = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    return QueryAccessibleApplicationBackground()?.toolkitName;
                }, ElementWrapper);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
        }

        private async Task FetchToolkitVersion()
        {
            try
            {
                toolkit_version = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    return QueryAccessibleApplicationBackground()?.toolkitVersion;
                }, ElementWrapper);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
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
                Root.NotifyElementDefunct(this);
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
                case "acc2_application_name":
                    depends_on.Add((this, new IdentifierExpression(id)));
                    if (application_name is null)
                        return UiDomUndefined.Instance;
                    return new UiDomString(application_name);
                case "acc2_application_version":
                    depends_on.Add((this, new IdentifierExpression(id)));
                    if (application_version is null)
                        return UiDomUndefined.Instance;
                    return new UiDomString(application_version);
                case "acc2_toolkit_name":
                    depends_on.Add((this, new IdentifierExpression(id)));
                    if (toolkit_name is null)
                        return UiDomUndefined.Instance;
                    return new UiDomString(toolkit_name);
                case "acc2_toolkit_version":
                    depends_on.Add((this, new IdentifierExpression(id)));
                    if (toolkit_version is null)
                        return UiDomUndefined.Instance;
                    return new UiDomString(toolkit_version);
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
            if (!(application_name is null))
                Utils.DebugWriteLine($"  acc2_application_name: {application_name}");
            if (!(application_version is null))
                Utils.DebugWriteLine($"  acc2_application_verion: {application_version}");
            if (!(toolkit_name is null))
                Utils.DebugWriteLine($"  acc2_toolkit_name: {toolkit_name}");
            if (!(toolkit_version is null))
                Utils.DebugWriteLine($"  acc2_toolkit_version: {toolkit_version}");
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
