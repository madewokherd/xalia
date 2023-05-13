using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class ValueProvider : IUiDomProvider
    {
        public ValueProvider(AccessibleProvider accessible)
        {
            Accessible = accessible;
        }

        public AccessibleProvider Accessible { get; }

        public AtSpiConnection Connection => Accessible.Connection;
        public string Peer => Accessible.Peer;
        public string Path => Accessible.Path;
        public UiDomElement Element => Accessible.Element;

        // Sync with AccessibleProvider.other_interface_properties
        private static readonly Dictionary<string, string> property_aliases = new Dictionary<string, string>
        {
            { "minimum_value", "spi_minimum_value" },
        };

        public double MinimumValue { get; private set; }
        public bool MinimumValueKnown { get; private set; }
        private bool _watchingMinimumValue;

        public void DumpProperties(UiDomElement element)
        {
            if (MinimumValueKnown)
                Utils.DebugWriteLine($"  spi_minimum_value: {MinimumValue}");
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "spi_minimum_value":
                    depends_on.Add((element, new IdentifierExpression("spi_minimum_value")));
                    if (MinimumValueKnown)
                        return new UiDomDouble(MinimumValue);
                    break;
            }
            return UiDomUndefined.Instance;
        }

        public UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (property_aliases.TryGetValue(identifier, out var aliased))
            {
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            }
            return UiDomUndefined.Instance;
        }

        public Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public string[] GetTrackedProperties()
        {
            return null;
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
        }

        public void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
        }

        public bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_minimum_value":
                        MinimumValueKnown = false;
                        _watchingMinimumValue = false;
                        element.EndPollProperty(new IdentifierExpression("spi_minimum_value"));
                        return true;
                }
            }
            return false;
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_minimum_value":
                        _watchingMinimumValue = true;
                        element.PollProperty(new IdentifierExpression("spi_minimum_value"), FetchMinimumValue, 2000);
                        return true;
                }
            }
            return false;
        }

        private async Task FetchMinimumValue()
        {
            double result;
            try
            {
                result = (double)await GetProperty(Connection.Connection, Peer, Path, IFACE_VALUE, "MinimumValue");
            }
            catch (DBusException e) {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }

            if (!_watchingMinimumValue)
                return;

            if (!MinimumValueKnown || MinimumValue != result)
            {
                MinimumValueKnown = true;
                MinimumValue = result;
                Element.PropertyChanged("spi_minimum_value", MinimumValue);
            }
        }
    }
}
