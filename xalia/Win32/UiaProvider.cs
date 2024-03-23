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

        private bool watching_children;

        enum SupportedState
        {
            Unqueried,
            Checking,
            NotSupported,
            Supported
        }

        private SupportedState fragment_supported;

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
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
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
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
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
                }
            }
            return base.WatchProperty(element, expression);
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