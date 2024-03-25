using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class UiaProvider : UiDomProviderBase
    {
        public UiaProvider(HwndProvider root_hwnd, UiDomElement element, IRawElementProviderSimple prov)
        {
            RootHwnd = root_hwnd;
            Element = element;
            Provider = prov;
        }

        public HwndProvider RootHwnd { get; }
        public UiDomElement Element { get; }
        public Win32Connection Connection => RootHwnd.Connection;
        public int Tid => RootHwnd.Tid;
        public CommandThread CommandThread => RootHwnd.CommandThread;

        public IRawElementProviderSimple Provider { get; }

        private static string[] tracked_properties = new string[] { "recurse_method" };

        static Dictionary<string, string> property_aliases = new Dictionary<string, string>
        {
            { "role", "uia_control_type" },
            { "control_type", "uia_control_type" },
            { "enabled", "uia_is_enabled" },
            { "offscreen", "uia_is_offscreen" },
            { "x", "uia_x" },
            { "y", "uia_y" },
            { "width", "uia_width" },
            { "height", "uia_height" },
        };

        internal static readonly string[] control_type_names =
        {
            "button",
            "calendar",
            "check_box",
            "combo_box",
            "edit",
            "hyperlink",
            "image",
            "list_item",
            "list",
            "menu",
            "menu_bar",
            "menu_item",
            "progress_bar",
            "radio_button",
            "scroll_bar",
            "slider",
            "spinner",
            "status_bar",
            "tab",
            "tab_item",
            "text",
            "tool_bar",
            "tool_tip",
            "tree",
            "tree_item",
            "custom",
            "group",
            "thumb",
            "data_grid",
            "data_item",
            "document",
            "split_button",
            "window",
            "pane",
            "header",
            "header_item",
            "table",
            "title_bar",
            "separator",
            "semantic_zoom",
            "app_bar"
        };

        internal static readonly UiDomEnum[] control_type_roles;
        internal static readonly Dictionary<string, int> name_to_control_type;

        static UiaProvider()
        {
            name_to_control_type = new Dictionary<string, int>();
            control_type_roles = new UiDomEnum[control_type_names.Length];
            for (int i = 0; i < control_type_roles.Length; i++)
            {
                string name = control_type_names[i];
                string[] names;
                if (name == "button")
                    names = new string[] { "button", "push_button", "pushbutton" };
                else if (name == "tab_item")
                    names = new string[] { "tab_item", "tabitem", "page_tab", "pagetab" };
                else if (name == "tab")
                    names = new string[] { "tab", "page_tab_list", "pagetablist" };
                else if (name == "text")
                    names = new string[] { "text", "label" };
                else if (name.Contains("_"))
                    names = new string[] { name, name.Replace("-", "") };
                else
                    names = new string[] { name };
                control_type_roles[i] = new UiDomEnum(names);
                foreach (var rolename in names)
                    name_to_control_type[rolename] = 50000 + i;
            }
        }

        private bool watching_children;

        private struct PropertyInfo
        {
            public int id;
            public string name;
            public bool watching;
            public bool known;
            public object value;

            public PropertyInfo(int id, string name)
            {
                this.id = id;
                this.name = name;
                watching = false;
                known = false;
                value = null;
            }
        }

        private PropertyInfo[] properties = // keep synced with Property enum
        {
            new PropertyInfo(UIA_ControlTypePropertyId, "uia_control_type"),
            new PropertyInfo(UIA_IsEnabledPropertyId, "uia_is_enabled"),
            new PropertyInfo(UIA_IsOffscreenPropertyId, "uia_is_offscreen"),
        };

        private enum Property
        {
            ControlType,
            Enabled,
            Offscreen,
        }

        enum SupportedState
        {
            Unqueried,
            Checking,
            NotSupported,
            Supported
        }

        private SupportedState fragment_supported;

        private UiaRect bounding_rectangle;
        private bool watching_bounding_rectangle;
        private bool bounding_rectangle_known;

        public override string[] GetTrackedProperties()
        {
            return tracked_properties;
        }

        public override void DumpProperties(UiDomElement element)
        {
            switch (fragment_supported)
            {
                case SupportedState.Unqueried:
                case SupportedState.Checking:
                    break;
                case SupportedState.NotSupported:
                    Utils.DebugWriteLine("  is_uia_fragment: false");
                    break;
                case SupportedState.Supported:
                    Utils.DebugWriteLine("  is_uia_fragment: true");
                    break;
            }
            for (int i=0; i < properties.Length; i++)
            {
                var prop = properties[i];
                if (prop.known)
                    Utils.DebugWriteLine($"  {prop.name}: {EvaluateProperty((Property)i)}");
            }
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_uia_element":
                    return UiDomBoolean.True;
                case "is_uia_fragment":
                    depends_on.Add((Element, new IdentifierExpression(identifier)));
                    switch (fragment_supported)
                    {
                        case SupportedState.Unqueried:
                        case SupportedState.Checking:
                            break;
                        case SupportedState.NotSupported:
                            return UiDomBoolean.False;
                        case SupportedState.Supported:
                            return UiDomBoolean.True;
                    }
                    break;
                case "uia_control_type":
                    return EvaluateProperty(Property.ControlType, depends_on);
                case "uia_is_enabled":
                    return EvaluateProperty(Property.Enabled, depends_on);
                case "uia_is_offscreen":
                    return EvaluateProperty(Property.Offscreen, depends_on);
                case "uia_x":
                case "uia_y":
                case "uia_width":
                case "uia_height":
                    depends_on.Add((Element, new IdentifierExpression("is_uia_fragment")));
                    if (fragment_supported != SupportedState.Supported)
                        break;
                    depends_on.Add((Element, new IdentifierExpression("uia_bounding_rectangle")));
                    if (bounding_rectangle_known)
                    {
                        switch (identifier)
                        {
                            case "uia_x":
                                return new UiDomDouble(bounding_rectangle.left);
                            case "uia_y":
                                return new UiDomDouble(bounding_rectangle.top);
                            case "uia_width":
                                return new UiDomDouble(bounding_rectangle.width);
                            case "uia_height":
                                return new UiDomDouble(bounding_rectangle.height);
                        }
                    }
                    break;
            }
            return RootHwnd.ChildEvaluateIdentifier(identifier, depends_on);
        }

        private UiDomValue EvaluateProperty(Property propid, HashSet<(UiDomElement, GudlExpression)> depends_on = null)
        {
            var result = GetPropertyValue(propid, depends_on);

            switch (propid)
            {
                case Property.ControlType:
                    if (result is int i && 50000 <= i && i < 50000 + control_type_roles.Length)
                        return control_type_roles[i-50000];
                    break;
            }

            return VariantToUiDomValue(result);
        }

        private UiDomValue VariantToUiDomValue(object result)
        {
            if (result is null)
                return UiDomUndefined.Instance;

            if (result is int i)
                return new UiDomInt(i);

            if (result is double d)
                return new UiDomDouble(d);

            if (result is string s)
                return new UiDomString(s);

            if (result is bool b)
                return UiDomBoolean.FromBool(b);

            Utils.DebugWriteLine($"Unhandled UIA property type {result.GetType().FullName}");

            return UiDomUndefined.Instance;
        }

        private object GetPropertyValue(Property propid, HashSet<(UiDomElement, GudlExpression)> depends_on = null)
        {
            var prop = properties[(int)propid];

            if (!(depends_on is null))
                depends_on.Add((Element, new IdentifierExpression(prop.name)));

            if (prop.known)
                return prop.value;

            return null;
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                    {
                        depends_on.Add((Element, new IdentifierExpression("is_uia_fragment")));
                        if (fragment_supported == SupportedState.Supported)
                            return new UiDomString("uia");
                    }
                    break;
                case "visible":
                    if (GetPropertyValue(Property.Offscreen, depends_on) is bool b)
                        return UiDomBoolean.FromBool(!b);
                    else if (properties[(int)Property.Offscreen].known &&
                        Element.ProviderByType<AccessibleProvider>() is null &&
                        Element.ProviderByType<HwndProvider>() is null)
                        // Defer to other providers for this when possible
                        return UiDomBoolean.True;
                    break;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
            {
                return Element.EvaluateIdentifier(aliased, Element.Root, depends_on);
            }
            if (name_to_control_type.TryGetValue(identifier, out var controlType))
            {
                if (GetPropertyValue(Property.ControlType, depends_on) is int i)
                    return UiDomBoolean.FromBool(i == controlType);
            }
            return RootHwnd.ChildEvaluateIdentifierLate(identifier, depends_on);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "is_uia_fragment":
                        if (fragment_supported == SupportedState.Unqueried)
                        {
                            fragment_supported = SupportedState.Checking;
                            Utils.RunTask(CheckFragmentSupport());
                        }
                        return true;
                    case "uia_control_type":
                        WatchProperty(Property.ControlType);
                        return true;
                    case "uia_is_enabled":
                        WatchProperty(Property.Enabled);
                        return true;
                    case "uia_is_offscreen":
                        WatchProperty(Property.Offscreen);
                        return true;
                    case "uia_bounding_rectangle":
                        if (!watching_bounding_rectangle)
                        {
                            watching_bounding_rectangle = true;
                            if (!bounding_rectangle_known)
                                Utils.RunTask(FetchBoundingRectangle());
                        }
                        return true;
                }
            }
            return base.WatchProperty(element, expression);
        }

        private async Task FetchBoundingRectangle()
        {
            var result = await CommandThread.OnBackgroundThread(() =>
            {
                var fragment = Provider as IRawElementProviderFragment;

                if (!(fragment is null))
                {
                    return (fragment.BoundingRectangle, true);
                }

                return default;
            }, CommandThreadPriority.Query);

            if (bounding_rectangle_known || !result.Item2)
            {
                return;
            }

            bounding_rectangle_known = true;
            bounding_rectangle = result.Item1;

            Element.PropertyChanged("uia_bounding_rectangle",
                $"{bounding_rectangle.left},{bounding_rectangle.top} {bounding_rectangle.width}x{bounding_rectangle.height}");
        }

        private void WatchProperty(Property propid)
        {
            int idx = (int)propid;

            var prop = properties[idx];

            if (!prop.watching)
            {
                prop.watching = true;
                if (!prop.known)
                {
                    Utils.RunTask(FetchProperty(propid));
                }
            }
        }

        private async Task FetchProperty(Property propid)
        {
            var idx = (int)propid;

            var value = await CommandThread.OnBackgroundThread(() =>
            {
                return Provider.GetPropertyValue(properties[idx].id);
            }, CommandThreadPriority.Query);

            if (properties[idx].known)
                // Assume we got this from an event which is more up to date
                return;

            properties[idx].value = value;
            properties[idx].known = true;
            Element.PropertyChanged(properties[idx].name, GetPropertyValue(propid));
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "uia_control_type":
                        UnwatchProperty(Property.ControlType);
                        return true;
                    case "uia_is_enabled":
                        UnwatchProperty(Property.Enabled);
                        return true;
                    case "uia_is_offscreen":
                        UnwatchProperty(Property.Offscreen);
                        return true;
                    case "uia_bounding_rectangle":
                        watching_bounding_rectangle = false;
                        bounding_rectangle_known = false;
                        return true;
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        private void UnwatchProperty(Property propid)
        {
            properties[(int)propid].watching = false;
            properties[(int)propid].known = false;
        }

        private async Task CheckFragmentSupport()
        {
            bool supported = await CommandThread.OnBackgroundThread(() =>
            {
                var iface = Provider as IRawElementProviderFragment;
                return !(iface is null);
            }, CommandThreadPriority.Query);

            if (supported)
                fragment_supported = SupportedState.Supported;
            else
                fragment_supported = SupportedState.NotSupported;
            Element.PropertyChanged("is_uia_fragment", supported);
        }

        public override void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            switch (name)
            {
                case "recurse_method":
                    if (new_value is UiDomString st && st.Value == "uia")
                        WatchChildren();
                    else
                        UnwatchChildren();
                    break;
            }
            base.TrackedPropertyChanged(element, name, new_value);
        }

        private void WatchChildren()
        {
            if (watching_children)
                return;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"WatchChildren for {Element} (uia)");
            Element.SetRecurseMethodProvider(this);
            watching_children = true;
            Utils.RunTask(PollChildren());
        }

        private async Task PollChildren()
        {
            var children = await CommandThread.OnBackgroundThread(() =>
            {
                var result = new List<ElementIdentifier>();

                var fragment = Provider as IRawElementProviderFragment;

                if (!(fragment is null))
                {
                    IRawElementProviderFragment child = fragment.Navigate(NavigateDirection.FirstChild);

                    while (!(child is null))
                    {
                        result.Add(ElementIdFromFragmentBackground(child));
                        child = child.Navigate(NavigateDirection.NextSibling);
                    }
                }

                return result;
            }, CommandThreadPriority.Query);

            if (!watching_children)
                return;

            Element.SyncRecurseMethodChildren(children, Connection.GetElementName, Connection.CreateElement);
        }

        private ElementIdentifier ElementIdFromFragmentBackground(IRawElementProviderFragment child)
        {
            var result = new ElementIdentifier();

            var simple = (IRawElementProviderSimple)child;

            // Is this an HWND?
            int hwnd = 0;
            var host_prov = simple.HostRawElementProvider;
            if (!(host_prov is null))
            {
                var value = host_prov.GetPropertyValue(UIA_NativeWindowHandlePropertyId);
                if (!(value is null))
                {
                    hwnd = (int)value;
                }
            }

            if (hwnd == 0)
            {
                var value = simple.GetPropertyValue(UIA_NativeWindowHandlePropertyId);
                if (!(value is null))
                {
                    hwnd = (int)value;
                }
            }

            if (hwnd != 0 && (IntPtr)hwnd != RootHwnd.Hwnd)
            {
                result.root_hwnd = (IntPtr)hwnd;
                result.is_root_hwnd = true;
                return result;
            }

            // Is this a bridged MSAA element?
            if (UiaIAccessibleFromProvider(simple, UIA_IAFP_UNWRAP_BRIDGE, out var acc, out var childid) == 0)
            {
                return AccessibleProvider.ElementIdFromVariantBackground(childid, acc, RootHwnd.Hwnd);
            }

            // Non-hwnd UIA element
            result.root_hwnd = RootHwnd.Hwnd;
            result.is_root_hwnd = false;
            result.prov = simple;
            result.runtime_id = child.GetRuntimeId();

            if (result.runtime_id is null)
            {
                // not sure how to handle this situation
                throw new Exception("runtime_id is null");
            }

            return result;
        }

        private void UnwatchChildren()
        {
            if (!watching_children)
                return;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {Element} (uia)");
            watching_children = false;
            Element.UnsetRecurseMethodProvider(this);
        }
    }
}