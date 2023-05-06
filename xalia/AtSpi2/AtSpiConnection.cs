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

        private HashSet<string> registered_events = new HashSet<string>();

        private Dictionary<string, int> poll_count = new Dictionary<string, int>();
        private Dictionary<string, IDisposable> poll_disposable = new Dictionary<string, IDisposable>();
        private Dictionary<string, Queue<TaskCompletionSource<IDisposable>>> poll_known_sources = new Dictionary<string, Queue<TaskCompletionSource<IDisposable>>>();
        private Dictionary<string, Queue<TaskCompletionSource<IDisposable>>> poll_unknown_sources = new Dictionary<string, Queue<TaskCompletionSource<IDisposable>>>();

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

            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "ChildrenChanged", result.OnChildrenChanged);
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "PropertyChange", result.OnPropertyChange);
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "StateChanged", result.OnStateChanged);
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "BoundsChanged", result.OnBoundsChanged);

            result.DesktopFrame = new AtSpiElement(result, registryClient, PATH_ACCESSIBLE_ROOT);
            result.AddChild(0, result.DesktopFrame);

            return result;
        }

        private void OnBoundsChanged(AtSpiSignal signal)
        {
            var element = LookupElement((signal.peer, signal.path));
            if (element is null)
                return;
            element.ProviderByType<AccessibleProvider>()?.AtSpiBoundsChanged(signal);
        }

        private void OnStateChanged(AtSpiSignal signal)
        {
            var element = LookupElement((signal.peer, signal.path));
            if (element is null)
                return;
            element.ProviderByType<AccessibleProvider>()?.AtSpiStateChanged(signal);
        }

        public Task RegisterEvent(string name)
        {
            if (registered_events.Contains(name))
                return Task.CompletedTask;
            registered_events.Add(name);
            return CallMethod(Connection, SERVICE_REGISTRY, PATH_REGISTRY, IFACE_REGISTRY,
                "RegisterEvent", name);
        }

        private void OnChildrenChanged(AtSpiSignal signal)
        {
            var element = LookupElement((signal.peer, signal.path));
            if (element is null)
                return;
            element.ProviderByType<AccessibleProvider>()?.AtSpiChildrenChanged(signal);
        }

        private void OnPropertyChange(AtSpiSignal signal)
        {
            var element = LookupElement((signal.peer, signal.path));
            if (element is null)
                return;
            element.ProviderByType<AccessibleProvider>()?.AtSpiPropertyChange(signal.detail, signal.value);
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

        private const int POLL_KNOWN_LIMIT = 32;
        private const int POLL_UNKNOWN_LIMIT = 64;

        internal Task<IDisposable> LimitPolling(string peer, bool value_known)
        {
            // Set a limit on the number of polling requests on the bus at once, so that we can
            // balance benefits from batching with added latency.
            int limit = value_known ? POLL_KNOWN_LIMIT : POLL_UNKNOWN_LIMIT;

            if (!poll_disposable.TryGetValue(peer, out var disposable))
            {
                disposable = new PollDispoable(this, peer);
                poll_disposable.Add(peer, disposable);
            }

            if (!poll_count.TryGetValue(peer, out var count))
            {
                count = 0;
                poll_count.Add(peer, count);
            }

            if (count < limit)
            {
                poll_count[peer] = count + 1;
                return Task.FromResult(disposable);
            }

            var source = new TaskCompletionSource<IDisposable>();
            var source_queues = value_known ? poll_known_sources : poll_unknown_sources;

            if (!source_queues.TryGetValue(peer, out var queue))
            {
                queue = new Queue<TaskCompletionSource<IDisposable>>();
                source_queues.Add(peer, queue);
            }

            queue.Enqueue(source);

            return source.Task;
        }

        private class PollDispoable : IDisposable
        {
            private string peer;
            private AtSpiConnection connection;

            public PollDispoable(AtSpiConnection connection, string peer)
            {
                this.connection = connection;
                this.peer = peer;
            }

            public void Dispose()
            {
                var count = connection.poll_count[peer];

                if (count <= POLL_UNKNOWN_LIMIT && connection.poll_unknown_sources.TryGetValue(peer, out var sources) &&
                    sources.Count != 0)
                {
                    sources.Dequeue().SetResult(connection.poll_disposable[peer]);
                    return;
                }
                if (count <= POLL_KNOWN_LIMIT && connection.poll_known_sources.TryGetValue(peer, out sources) &&
                    sources.Count != 0)
                {
                    sources.Dequeue().SetResult(connection.poll_disposable[peer]);
                    return;
                }
                connection.poll_count[peer] = count - 1;
            }
        }
    }
}
