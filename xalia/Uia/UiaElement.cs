﻿using System;
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
    internal class UiaElement : UiDomElement
    {
        static long NextDebugId;

        public UiaElement(AutomationElement element, UiaConnection root) : base(root)
        {
            AutomationElement = element;
            Root = root;
            // There doesn't seem to be any reliable non-blocking away to get an element id, so we make one up
            long debug_id = NextDebugId;
            NextDebugId++;
            DebugId = $"UIA:{debug_id}";
        }

        public AutomationElement AutomationElement { get; }

        public override string DebugId { get; }

        public new UiaConnection Root { get; }

        Dictionary<PropertyId, bool> watching_property = new Dictionary<PropertyId, bool>();
        Dictionary<PropertyId, bool> property_known = new Dictionary<PropertyId, bool>();
        Dictionary<PropertyId, UiDomValue> property_value = new Dictionary<PropertyId, UiDomValue>();
        Dictionary<PropertyId, PropertyChangedEventHandlerBase> property_change_handlers = new Dictionary<PropertyId, PropertyChangedEventHandlerBase>();

        bool watching_children;
        bool refreshing_children;
        bool inflight_structure_changed;
        StructureChangedEventHandlerBase structure_changed_event;

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
                    names = new[] { "tab", "page_tab", "pagetab" };
                else if (name == "tab_item")
                    names = new[] { "tab_item", "tabitem", "page_tab_list", "pagetablist" };
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

            var children = await Root.CommandThread.GetChildren(AutomationElement);

            if (!watching_children)
                return;

            // First remove any existing children that are missing or out of order
            int i = 0;
            foreach (var new_child in children)
            {
                if (!Children.Exists((UiDomElement element) => ElementMatches(element, new_child)))
                    continue;
                while (!ElementMatches(Children[i], new_child))
                {
                    RemoveChild(i);
                }
                i++;
            }

            // Add any new children
            for (i = 0; i < children.Length; i++)
            {
                if (Children.Count <= i || !ElementMatches(Children[i], children[i]))
                {
                    AddChild(i, new UiaElement(children[i], Root));
                }
            }

            refreshing_children = false;

            if (inflight_structure_changed)
            {
                inflight_structure_changed = false;
                Utils.RunTask(RefreshChildren());
            }
        }

        private bool ElementMatches(UiDomElement uidom, AutomationElement ae)
        {
            if (uidom is UiaElement uia)
                return uia.AutomationElement.Equals(ae);
            return false;
        }

        internal async Task WatchChildrenTask()
        {
            StructureChangedEventHandlerBase structure_changed_event = await Root.EventThread.RegisterChildrenChangedEventAsync(AutomationElement, OnChildrenChanged);

            if (this.structure_changed_event != null)
                Utils.RunTask(Root.EventThread.UnregisterEventHandler(this.structure_changed_event));

            this.structure_changed_event = structure_changed_event;

            await RefreshChildren();
        }

        private void OnChildrenChanged(AutomationElement arg1, StructureChangeType arg2, int[] arg3)
        {
#if DEBUG
            Console.WriteLine("OnChildrenChanged for {0}", DebugId);
#endif
            Utils.RunTask(RefreshChildren());
        }

        internal void WatchChildren()
        {
            if (watching_children)
                return;
#if DEBUG
            Console.WriteLine("WatchChildren for {0}", DebugId);
#endif
            watching_children = true;
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

            if (structure_changed_event != null)
            {
                Utils.RunTask(Root.EventThread.UnregisterEventHandler(structure_changed_event));
                structure_changed_event = null;
            }
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                RemoveChild(i);
            }
        }

        protected override void DeclarationsChanged(Dictionary<string, UiDomValue> all_declarations, HashSet<(UiDomElement, GudlExpression)> dependencies)
        {
            if (all_declarations.TryGetValue("recurse", out var recurse) && recurse.ToBool())
                WatchChildren();
            else
                UnwatchChildren();

            base.DeclarationsChanged(all_declarations, dependencies);
        }

        private async Task WatchPropertyAsync(string name, PropertyId propid, Func<object, UiDomValue> conversion)
        {
            property_change_handlers[propid] =
                await Root.EventThread.RegisterPropertyChangedEventAsync(AutomationElement, propid,
                (AutomationElement element, PropertyId _propid, object value) =>
                {
                    OnPropertyChange(name, propid, conversion, value);
                });

            object current_value = await Root.CommandThread.GetPropertyValue(AutomationElement, propid);
            UiDomValue ui_dom_value = conversion(current_value);

            UiDomValue old_value;
            if (!property_value.TryGetValue(propid, out old_value))
                old_value = UiDomUndefined.Instance;

            if (!old_value.Equals(ui_dom_value))
            {
                property_known[propid] = true;
                property_value[propid] = ui_dom_value;

                PropertyChanged(name);
            }
        }

        private void OnPropertyChange(string name, PropertyId propid, Func<object, UiDomValue> conversion, object value)
        {
            UiDomValue new_value = conversion(value);

            UiDomValue old_value;
            if (!property_value.TryGetValue(propid, out old_value))
                old_value = UiDomUndefined.Instance;

            if (!old_value.Equals(new_value))
            {
                property_known[propid] = true;
                property_value[propid] = new_value;
                PropertyChanged(name);
            }
        }

        private void WatchProperty(string name, PropertyId propid, Func<object, UiDomValue> conversion)
        {
            if (!watching_property.TryGetValue(propid, out var watching) || !watching)
            {
                watching_property[propid] = true;
                Utils.RunTask(WatchPropertyAsync(name, propid, conversion));
            }
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "uia_control_type":
                        {
                            WatchProperty("uia_control_type", Root.Automation.PropertyLibrary.Element.ControlType, ConvertControlType);
                            break;
                        }
                }
            }

            base.WatchProperty(expression);
        }

        private void UnwatchProperty(PropertyId propid)
        {
            if (watching_property.TryGetValue(propid, out var watching) && watching)
            {
                watching_property[propid] = false;
                property_known[propid] = false;
                property_value[propid] = UiDomUndefined.Instance;

                if (property_change_handlers.TryGetValue(propid, out var handler))
                {
                    property_change_handlers.Remove(propid);
                    Utils.RunTask(Root.EventThread.UnregisterEventHandler(handler));
                }
            }
        }

        protected override void UnwatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "uia_control_type":
                        {
                            UnwatchProperty(Root.Automation.PropertyLibrary.Element.ControlType);
                            break;
                        }
                }
            }
            base.UnwatchProperty(expression);
        }

        protected override void SetAlive(bool value)
        {
            if (value)
            {
                Root.elements_by_uia[AutomationElement] = this;
            }
            else
            {
                property_known.Clear();
                property_value.Clear();
                watching_property.Clear();
                foreach (var kvp in property_change_handlers)
                {
                    Utils.RunTask(Root.EventThread.UnregisterEventHandler(kvp.Value));
                }
                property_change_handlers.Clear();
                UnwatchChildren();
                Root.elements_by_uia.Remove(AutomationElement);
            }
            base.SetAlive(value);
        }

        private UiDomValue ConvertControlType(object arg)
        {
            if (arg is ControlType ct && (int)ct < control_type_to_enum.Length)
            {
                return control_type_to_enum[(int)ct];
            }
            return UiDomUndefined.Instance;
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

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "role":
                case "control_type":
                case "controltype":
                case "uia_controltype":
                case "uia_control_type":
                    return GetProperty("uia_control_type", Root.Automation.PropertyLibrary.Element.ControlType, depends_on);
                case "uia_focused":
                case "focused":
                    depends_on.Add((this, new IdentifierExpression("uia_focused")));
                    return UiDomBoolean.FromBool(AutomationElement.Equals(Root.FocusedElement));
                case "uia_focused_element":
                case "focused_element":
                    depends_on.Add((Root, new IdentifierExpression("uia_focused_element")));
                    return (UiDomValue)Root.LookupAutomationElement(Root.FocusedElement) ?? UiDomUndefined.Instance;
            }

            if (name_to_control_type.ContainsKey(id))
            {
                return Evaluate(new BinaryExpression(
                    new IdentifierExpression("uia_control_type"),
                    new IdentifierExpression(id),
                    GudlToken.Dot), depends_on);
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
