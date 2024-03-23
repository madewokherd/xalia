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

        enum SupportedState
        {
            Unqueried,
            Checking,
            NotSupported,
            Supported
        }

        private SupportedState fragment_supported;

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
    }
}