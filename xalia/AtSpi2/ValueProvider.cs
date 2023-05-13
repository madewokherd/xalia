using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class ValueProvider : UiDomProviderBase
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
            { "maximum_value", "spi_maximum_value" },
            { "minimum_increment", "spi_minimum_increment" },
            { "small_change", "spi_minimum_increment" },
        };

        public double MinimumValue { get; private set; }
        public bool MinimumValueKnown { get; private set; }
        private bool _watchingMinimumValue;

        public double MaximumValue { get; private set; }
        public bool MaximumValueKnown { get; private set; }
        private bool _watchingMaximumValue;

        public double MinimumIncrement { get; private set; }
        public bool MinimumIncrementKnown { get; private set; }
        private bool _watchingMinimumIncrement;

        public override void DumpProperties(UiDomElement element)
        {
            if (MinimumValueKnown)
                Utils.DebugWriteLine($"  spi_minimum_value: {MinimumValue}");
            if (MaximumValueKnown)
                Utils.DebugWriteLine($"  spi_maximum_value: {MaximumValue}");
            if (MinimumIncrementKnown)
                Utils.DebugWriteLine($"  spi_minimum_increment: {MinimumIncrement}");
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "spi_minimum_value":
                    depends_on.Add((element, new IdentifierExpression("spi_minimum_value")));
                    if (MinimumValueKnown)
                        return new UiDomDouble(MinimumValue);
                    break;
                case "spi_maximum_value":
                    depends_on.Add((element, new IdentifierExpression("spi_maximum_value")));
                    if (MaximumValueKnown)
                        return new UiDomDouble(MaximumValue);
                    break;
                case "spi_minimum_increment":
                    depends_on.Add((element, new IdentifierExpression("spi_minimum_increment")));
                    if (MinimumIncrementKnown)
                        return new UiDomDouble(MinimumIncrement);
                    break;
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

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
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
                    case "spi_maximum_value":
                        MaximumValueKnown = false;
                        _watchingMaximumValue = false;
                        element.EndPollProperty(new IdentifierExpression("spi_maximum_value"));
                        return true;
                    case "spi_minimum_increment":
                        MinimumIncrementKnown = false;
                        _watchingMinimumIncrement = false;
                        element.EndPollProperty(new IdentifierExpression("spi_minimum_increment"));
                        return true;
                }
            }
            return false;
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_minimum_value":
                        _watchingMinimumValue = true;
                        element.PollProperty(new IdentifierExpression("spi_minimum_value"), FetchMinimumValue, 2000);
                        return true;
                    case "spi_maximum_value":
                        _watchingMaximumValue = true;
                        element.PollProperty(new IdentifierExpression("spi_maximum_value"), FetchMaximumValue, 2000);
                        return true;
                    case "spi_minimum_increment":
                        _watchingMinimumIncrement = true;
                        element.PollProperty(new IdentifierExpression("spi_minimum_increment"), FetchMinimumIncrement, 2000);
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

        private async Task FetchMaximumValue()
        {
            double result;
            try
            {
                result = (double)await GetProperty(Connection.Connection, Peer, Path, IFACE_VALUE, "MaximumValue");
            }
            catch (DBusException e) {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }

            if (!_watchingMaximumValue)
                return;

            if (!MaximumValueKnown || MaximumValue != result)
            {
                MaximumValueKnown = true;
                MaximumValue = result;
                Element.PropertyChanged("spi_maximum_value", MaximumValue);
            }
        }

        private async Task FetchMinimumIncrement()
        {
            double result;
            try
            {
                result = (double)await GetProperty(Connection.Connection, Peer, Path, IFACE_VALUE, "MinimumIncrement");
            }
            catch (DBusException e) {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return;
            }

            if (!_watchingMinimumIncrement)
                return;

            if (!MinimumIncrementKnown || MinimumIncrement != result)
            {
                MinimumIncrementKnown = true;
                MinimumIncrement = result;
                Element.PropertyChanged("spi_minimum_increment", MinimumIncrement);
            }
        }
    }
}
