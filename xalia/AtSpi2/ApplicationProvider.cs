using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class ApplicationProvider : UiDomProviderBase
    {
        public ApplicationProvider(AccessibleProvider accessible)
        {
            Accessible = accessible;
        }

        public AccessibleProvider Accessible { get; }

        public AtSpiConnection Connection => Accessible.Connection;
        public string Peer => Accessible.Peer;
        public string Path => Accessible.Path;
        public UiDomElement Element => Accessible.Element;

        public bool ToolkitNameKnown { get; private set; }
        public string ToolkitName { get; private set; }
        private bool fetching_toolkit_name;

        // Sync with AccessibleProvider.other_interface_properties
        private static readonly Dictionary<string, string> property_aliases = new Dictionary<string, string>
        {
            { "toolkit_name", "spi_toolkit_name" },
        };

        public override void DumpProperties(UiDomElement element)
        {
            if (ToolkitNameKnown)
                Utils.DebugWriteLine($"  spi_toolkit_name: \"{ToolkitName}\"");
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "spi_toolkit_name":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (ToolkitNameKnown)
                        return new UiDomString(ToolkitName);
                    return UiDomUndefined.Instance;
            }
            return UiDomUndefined.Instance;
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (property_aliases.TryGetValue(identifier, out var aliased))
            {
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            }
            return UiDomUndefined.Instance;
        }

        private async Task FetchToolkitName()
        {
            VariantValue result;
            try
            {
                result = await GetProperty(Connection.Connection, Peer, Path,
                    IFACE_APPLICATION, "ToolkitName");
            }
            catch (DBusErrorReplyException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }

            if (result.Type == VariantValueType.String)
            {
                string st = result.GetString();
                ToolkitNameKnown = true;
                ToolkitName = st;
                Element.PropertyChanged("spi_toolkit_name", ToolkitName);
                return;
            }

            if (result.Type == VariantValueType.Invalid)
                Utils.DebugWriteLine($"WARNING: {Element} returned null for ToolkitName");
            else
                Utils.DebugWriteLine($"WARNING: {Element} returned {result} for ToolkitName");
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_toolkit_name":
                        if (!fetching_toolkit_name)
                        {
                            fetching_toolkit_name = true;
                            Utils.RunTask(FetchToolkitName());
                        }
                        break;
                }
            }
            return false;
        }
    }
}