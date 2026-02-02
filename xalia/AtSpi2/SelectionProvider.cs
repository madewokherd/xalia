using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class SelectionProvider : UiDomProviderBase
    {
        public SelectionProvider(AccessibleProvider accessibleProvider)
        {
            AccessibleProvider = accessibleProvider;
        }

        public AccessibleProvider AccessibleProvider { get; }
        public AtSpiConnection Connection => AccessibleProvider.Connection;
        public string Peer => AccessibleProvider.Peer;
        public string Path => AccessibleProvider.Path;

        // Sync with AccessibleProvider.other_interface_properties
        private static readonly Dictionary<string, string> property_aliases = new Dictionary<string, string>
        {
            { "clear_selection", "spi_clear_selection" },
        };

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "spi_clear_selection":
                    return new UiDomRoutineAsync(element, "spi_clear_selection", ClearSelectionAsync);
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (property_aliases.TryGetValue(identifier, out var aliased))
            {
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        private static async Task ClearSelectionAsync(UiDomRoutineAsync obj)
        {
            var provider = obj.Element.ProviderByType<SelectionProvider>();
            try
            {
                var result = await CallMethod(provider.Connection.Connection, provider.Peer, provider.Path, IFACE_SELECTION,
                    "ClearSelection", ReadMessageBoolean);

                if (!result)
                {
                    Utils.DebugWriteLine($"WARNING: ClearSelection failed for {obj.Element}");
                }
            }
            catch (DBusErrorReplyException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
            }
        }
    }
}