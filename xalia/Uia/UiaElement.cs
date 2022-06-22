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
    internal class UiaElement : UiDomElement
    {
        static long NextDebugId;

        public UiaElement(AutomationElement element, UiaConnection root) : base(root)
        {
            AutomationElement = element;
            Root = root;
            // There doesn't seem to be any reliable non-blocking away to get an element id, so we make one up
            long debug_id = Interlocked.Increment(ref NextDebugId);
            DebugId = $"UIA:{debug_id}";
        }

        public AutomationElement AutomationElement { get; }

        public override string DebugId { get; }

        public new UiaConnection Root { get; }

        Dictionary<PropertyId, bool> watching_property = new Dictionary<PropertyId, bool>();
        Dictionary<PropertyId, bool> property_known = new Dictionary<PropertyId, bool>();
        Dictionary<PropertyId, UiDomValue> property_value = new Dictionary<PropertyId, UiDomValue>();
        Dictionary<PropertyId, PropertyChangedEventHandlerBase> property_change_handlers = new Dictionary<PropertyId, PropertyChangedEventHandlerBase>();

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
