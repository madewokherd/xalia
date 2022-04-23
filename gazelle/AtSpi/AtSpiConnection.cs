using System;
using System.Threading;
using System.Threading.Tasks;

using Tmds.DBus;

using Gazelle.AtSpi.DBus;
using Gazelle.UiDom;

namespace Gazelle.AtSpi
{
    internal class AtSpiConnection : UiDomObject
    {
        internal Connection connection;

        internal override string DebugId => "AtSpiConnection";

        internal AtSpiConnection(Connection connection) : base(true)
        {
            this.connection = connection;
            AddChild(0, new AtSpiObject(this, "org.a11y.atspi.Registry", "/org/a11y/atspi/accessible/root"));
        }

        internal static async Task<string> GetAtSpiBusAddress()
        {
            string result = Environment.GetEnvironmentVariable("AT_SPI_BUS_ADDRESS");
            // TODO: Try getting AT_SPI_BUS property from X11 root

            // Try getting bus address from session bus org.a11y.Bus interface
            if (string.IsNullOrWhiteSpace(result))
            {
                var session = Connection.Session;
                var launcher = session.CreateProxy<IBus>("org.a11y.Bus", "/org/a11y/bus");
                result = await launcher.GetAddressAsync();
            }
            return result;
        }

        internal static async Task<AtSpiConnection> Connect()
        {
            string bus = await GetAtSpiBusAddress();
            if (string.IsNullOrWhiteSpace(bus))
            {
                Console.WriteLine("AT-SPI bus could not be found. Did you enable assistive technologies in your desktop environment?");
                return null;
            }
            Console.WriteLine("AT-SPI bus found: {0}", bus);
            var options = new ClientConnectionOptions(bus);
            options.SynchronizationContext = SynchronizationContext.Current;
            var connection = new Connection(options);
            await connection.ConnectAsync();
            return new AtSpiConnection(connection);
        }
    }
}
