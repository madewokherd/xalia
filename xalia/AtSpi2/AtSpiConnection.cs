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
        private AtSpiConnection(GudlStatement[] rules, IUiDomApplication application) : base(rules, application)
        {

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
                var buffer = CreateMethodCall(session, "org.a11y.Bus", "/org/a11y/bus", "org.a11y.Bus",
                    "GetAddress");

                result = await session.CallMethodAsync(buffer, ReadMessageString);
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
            Utils.DebugWriteLine($"AT-SPI bus found: {bus}");
            return null;
        }
    }
}
