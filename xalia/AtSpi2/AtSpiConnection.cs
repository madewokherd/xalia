using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.Sdl;
using Xalia.UiDom;

using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class AtSpiConnection : UiDomRoot
    {
        public Connection Connection { get; }
        public AtSpiElement DesktopFrame { get; private set; }

        private AtSpiConnection(Connection connection, GudlStatement[] rules, IUiDomApplication application) : base(rules, application)
        {
            Connection = connection;
        }

        private Dictionary<(string, string), AtSpiElement> elements = new Dictionary<(string, string), AtSpiElement>();

        internal static async Task<string> GetAtSpiBusAddress()
        {
            string result = Environment.GetEnvironmentVariable("AT_SPI_BUS_ADDRESS");

            var session = Connection.Session;

            // Request that accessibility support be enabled, before fetching address.
            await SetProperty(session, SERVICE_BUS, PATH_BUS, IFACE_STATUS,
                "IsEnabled", true);

            // Try getting AT_SPI_BUS property from X11 root
            if (string.IsNullOrWhiteSpace(result))
            {
                var windowing = WindowingSystem.Instance;

                if (windowing is X11WindowingSystem x11)
                {
                    result = x11.GetAtSpiBusAddress();
                }
            }

            // Try getting bus address from session bus org.a11y.Bus interface
            if (string.IsNullOrWhiteSpace(result))
            {
                result = await CallMethod(session, SERVICE_BUS, PATH_BUS, IFACE_BUS,
                    "GetAddress", ReadMessageString);
            }

            return result;
        }

        internal static async Task<AtSpiConnection> Connect(GudlStatement[] config, IUiDomApplication application)
        {
            string bus = await GetAtSpiBusAddress();
            if (string.IsNullOrWhiteSpace(bus))
            {
                Utils.DebugWriteLine("AT-SPI bus could not be found. Did you enable assistive technologies in your desktop environment?");
                return null;
            }

            var connection = new Connection(bus);
            await connection.ConnectAsync();

            var result = new AtSpiConnection(connection, config, application);

            await CallMethod(connection, SERVICE_DBUS, PATH_DBUS, IFACE_DBUS,
                "StartServiceByName", SERVICE_REGISTRY, (uint)0);

            var registryClient = await CallMethod(connection, SERVICE_DBUS, PATH_DBUS, IFACE_DBUS,
                "GetNameOwner", SERVICE_REGISTRY,
                ReadMessageString);

            await RegisterEvent(connection, registryClient, "object:children-changed");
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "ChildrenChanged", result.OnChildrenChanged);

            await RegisterEvent(connection, registryClient, "object:property-change:accessible-role");
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "PropertyChange", result.OnPropertyChange);

            await RegisterEvent(connection, registryClient, "object:state-changed");
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "StateChanged", result.OnStateChanged);

            result.DesktopFrame = new AtSpiElement(result, registryClient, PATH_ACCESSIBLE_ROOT);
            result.AddChild(0, result.DesktopFrame);

            return result;
        }

        private void OnStateChanged(AtSpiSignal signal)
        {
            var element = LookupElement((signal.peer, signal.path));
            if (element is null)
                return;
            element.AtSpiStateChanged(signal);
        }

        private static Task RegisterEvent(Connection connection, string registryClient, string name)
        {
            return CallMethod(connection, registryClient, PATH_REGISTRY, IFACE_REGISTRY,
                "RegisterEvent", name);
        }

        private void OnChildrenChanged(AtSpiSignal signal)
        {
            var element = LookupElement((signal.peer, signal.path));
            if (element is null)
                return;
            element.AtSpiChildrenChanged(signal);
        }

        private void OnPropertyChange(AtSpiSignal signal)
        {
            var element = LookupElement((signal.peer, signal.path));
            if (element is null)
                return;
            element.AtSpiPropertyChange(signal.detail, signal.value);
        }

        internal void NotifyElementCreated(AtSpiElement element)
        {
            elements.Add((element.Peer, element.Path), element);
        }

        internal void NotifyElementDestroyed(AtSpiElement element)
        {
            elements.Remove((element.Peer, element.Path));
        }

        internal AtSpiElement LookupElement((string, string) id)
        {
            if (elements.TryGetValue(id, out var element))
                return element;
            return null;
        }
    }
}
