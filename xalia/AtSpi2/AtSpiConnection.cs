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
    internal class AtSpiConnection : IUiDomProvider
    {
        public Connection Connection { get; }
        public UiDomRoot Root { get; }
        public UiDomElement DesktopFrame { get; private set; }

        private HashSet<string> registered_events = new HashSet<string>();

        private Dictionary<string, int> poll_count = new Dictionary<string, int>();
        private Dictionary<string, IDisposable> poll_disposable = new Dictionary<string, IDisposable>();
        private Dictionary<string, Queue<TaskCompletionSource<IDisposable>>> poll_known_sources = new Dictionary<string, Queue<TaskCompletionSource<IDisposable>>>();
        private Dictionary<string, Queue<TaskCompletionSource<IDisposable>>> poll_unknown_sources = new Dictionary<string, Queue<TaskCompletionSource<IDisposable>>>();

        private AtSpiConnection(Connection connection, UiDomRoot root)
        {
            Connection = connection;
            Root = root;
        }

        private Dictionary<(string, string), UiDomElement> elements = new Dictionary<(string, string), UiDomElement>();

        static bool DebugExceptions = Environment.GetEnvironmentVariable("XALIA_DEBUG_EXCEPTIONS") != "0";

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

        internal UiDomElement CreateElement(string peer, string path)
        {
            UiDomElement result = new UiDomElement($"{peer}:{path}", Root);
            result.AddProvider(new AccessibleProvider(result, this, peer, path));
            elements.Add((peer, path), result);
            return result;
        }

        internal UiDomElement CreateElement((string, string) id)
        {
            return CreateElement(id.Item1, id.Item2);
        }

        internal static async Task<AtSpiConnection> Connect(UiDomRoot root)
        {
            string bus = await GetAtSpiBusAddress();
            if (string.IsNullOrWhiteSpace(bus))
            {
                Utils.DebugWriteLine("AT-SPI bus could not be found. Did you enable assistive technologies in your desktop environment?");
                return null;
            }

            var connection = new Connection(bus);
            await connection.ConnectAsync();

            var result = new AtSpiConnection(connection, root);
            root.AddGlobalProvider(result);

            await CallMethod(connection, SERVICE_DBUS, PATH_DBUS, IFACE_DBUS,
                "StartServiceByName", SERVICE_REGISTRY, (uint)0);

            var registryClient = await CallMethod(connection, SERVICE_DBUS, PATH_DBUS, IFACE_DBUS,
                "GetNameOwner", SERVICE_REGISTRY,
                ReadMessageString);

            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "AttributesChanged", result.OnAttributesChanged);
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "ChildrenChanged", result.OnChildrenChanged);
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "PropertyChange", result.OnPropertyChange);
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "StateChanged", result.OnStateChanged);
            await MatchAtSpiSignal(connection, IFACE_EVENT_OBJECT, "BoundsChanged", result.OnBoundsChanged);
            await MatchAtSpiSignal(connection, IFACE_EVENT_WINDOW, "Activate", result.OnWindowActivate);
            await MatchAtSpiSignal(connection, IFACE_EVENT_WINDOW, "Deactivate", result.OnWindowDeactivate);

            result.DesktopFrame = result.CreateElement(registryClient, PATH_ACCESSIBLE_ROOT);
            root.AddChild(0, result.DesktopFrame);

            return result;
        }

        private void OnAttributesChanged(AtSpiSignal signal)
        {
            var element = LookupElement((signal.peer, signal.path));
            if (element is null)
                return;
            element.ProviderByType<AccessibleProvider>()?.AtSpiOnAttributesChanged(signal);
        }

        private void OnWindowActivate(AtSpiSignal signal)
        {
            signal.detail = "active";
            signal.detail1 = 1;
            OnStateChanged(signal);
        }

        private void OnWindowDeactivate(AtSpiSignal signal)
        {
            signal.detail = "active";
            signal.detail1 = 0;
            OnStateChanged(signal);
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

        internal void NotifyElementDestroyed(AccessibleProvider provider)
        {
            elements.Remove((provider.Peer, provider.Path));
        }

        internal UiDomElement LookupElement((string, string) id)
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

        internal static bool IsExpectedException(DBusException e, params string[] extra_errors)
        {
#if DEBUG
            if (DebugExceptions)
            {
                Utils.DebugWriteLine("WARNING: DBus exception:");
                Utils.DebugWriteLine(e);
            }
#endif
            switch (e.ErrorName)
            {
                case "org.freedesktop.DBus.Error.NoReply":
                case "org.freedesktop.DBus.Error.UnknownObject":
                case "org.freedesktop.DBus.Error.UnknownInterface":
                case "org.freedesktop.DBus.Error.ServiceUnknown":
                    return true;
                default:
                    foreach (var err in extra_errors)
                    {
                        if (e.ErrorName == err)
                            return true;
                    }
#if DEBUG
                    return false;
#else
                    if (DebugExceptions)
                    {
                        Utils.DebugWriteLine("WARNING: DBus exception ignored:");
                        Utils.DebugWriteLine(e);
                    }
                    return true;
#endif
            }
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return UiDomUndefined.Instance;
        }

        public UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return UiDomUndefined.Instance;
        }

        public void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
        }

        public void DumpProperties(UiDomElement element)
        {
        }

        public Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public string[] GetTrackedProperties()
        {
            return null;
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
