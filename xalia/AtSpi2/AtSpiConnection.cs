using Superpower.Model;
using System;
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

        private AtSpiConnection(Connection connection, GudlStatement[] rules, IUiDomApplication application) : base(rules, application)
        {
            Connection = connection;
        }

        internal static async Task<string> GetAtSpiBusAddress()
        {
            string result = Environment.GetEnvironmentVariable("AT_SPI_BUS_ADDRESS");

            var session = Connection.Session;

            // Request that accessibility support be enabled, before fetching address.
            await SetProperty(session, "org.a11y.Bus", "/org/a11y/bus", "org.a11y.Status",
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
                result = await CallMethod(session, "org.a11y.Bus", "/org/a11y/bus", "org.a11y.Bus",
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

            await CallMethod(connection, "org.freedesktop.DBus", "/org/freedesktop/DBus",
                "org.freedesktop.DBus", "StartServiceByName", "org.a11y.atspi.Registry", (uint)0);

            var registryClient = await CallMethod(connection, "org.freedesktop.DBus",
                "/org/freedesktop/DBus", "org.freedesktop.DBus", "GetNameOwner", "org.a11y.atspi.Registry",
                ReadMessageString);
            Console.WriteLine($"registry client: {registryClient}");

            return result;
        }
    }
}
