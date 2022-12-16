using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Tmds.DBus;

using Xalia.AtSpi.DBus;
using Xalia.Gudl;
using Xalia.Sdl;
using Xalia.UiDom;

namespace Xalia.AtSpi
{
    internal class AtSpiConnection : UiDomRoot
    {
        internal Connection connection;

        public override string DebugId => "AtSpiConnection";

        IRegistry registry;

        private AtSpiConnection(Connection connection, GudlStatement[] rules, IUiDomApplication application) : base(rules, application)
        {
            this.connection = connection;
        }

        internal static async Task<string> GetAtSpiBusAddress()
        {
            string result = Environment.GetEnvironmentVariable("AT_SPI_BUS_ADDRESS");

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
                var session = Connection.Session;

                // Request that accessibility support be enabled, before fetching address.
                var status = session.CreateProxy<IStatus>("org.a11y.Bus", "/org/a11y/bus");
                await status.SetIsEnabledAsync(true);

                var launcher = session.CreateProxy<IBus>("org.a11y.Bus", "/org/a11y/bus");
                result = await launcher.GetAddressAsync();
            }
            return result;
        }

        internal static async Task<AtSpiConnection> Connect(GudlStatement[] config, IUiDomApplication application)
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
            var result = new AtSpiConnection(connection, config, application);

            // Resolve the service name to an actual client. Signals will come from the client name, so
            // we need this to distinguish between signals from the AT-SPI root and signals from an
            // application's root, both of which use the object path "/org/a11y/atspi/accessible/root"
            await connection.ActivateServiceAsync("org.a11y.atspi.Registry");
            string registryClient = await connection.ResolveServiceOwnerAsync("org.a11y.atspi.Registry");

            result.registry = connection.CreateProxy<IRegistry>(registryClient, "/org/a11y/atspi/registry");

            // Register all the events we're interested in at the start, fine-grained management isn't worth it
            await result.registry.RegisterEventAsync("object:children-changed");
            await result.registry.RegisterEventAsync("object:state-changed");
            await result.registry.RegisterEventAsync("object:bounds-changed");
            await result.registry.RegisterEventAsync("object:text-changed");
            await result.registry.RegisterEventAsync("object:property-change:accessible-name");
            await result.registry.RegisterEventAsync("object:property-change:accessible-role");
            await result.registry.RegisterEventAsync("window:activate");
            await result.registry.RegisterEventAsync("window:deactivate");

            result.DesktopFrame = new AtSpiElement(result, registryClient, "/org/a11y/atspi/accessible/root");
            result.AddChild(0, result.DesktopFrame);

            return result;
        }

        public AtSpiElement DesktopFrame { get; private set; }

        private static UiDomValue AdjustScrollbarsMethod(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length >= 2)
            {
                var hscroll = context.Evaluate(arglist[0], root, depends_on) as AtSpiElement;
                var vscroll = context.Evaluate(arglist[1], root, depends_on) as AtSpiElement;

                if (hscroll is null && vscroll is null)
                    return UiDomUndefined.Instance;

                return new AtSpiAdjustScrollbarsRoutine(hscroll, vscroll);
            }
            return UiDomUndefined.Instance;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "adjust_scrollbars":
                    return new UiDomMethod("adjust_scrollbars", AdjustScrollbarsMethod);
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
