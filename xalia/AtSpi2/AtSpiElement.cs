using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class AtSpiElement : UiDomElement
    {
        public AtSpiElement(AtSpiConnection root, string peer, string path): base($"{peer}:{path}", root)
        {
            Root = root;
            Peer = peer;
            Path = path;
            AddProvider(new AccessibleProvider(this, root, peer, path));
        }

        public new AtSpiConnection Root { get; }
        public string Peer { get; }
        public string Path { get; }

        internal static Dictionary<string, string> name_mapping;

        protected override void SetAlive(bool value)
        {
            if (value)
            {
                Root.NotifyElementCreated(this);
            }
            else
            {
                Root.NotifyElementDestroyed(this);
            }
            base.SetAlive(value);
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            base.WatchProperty(expression);
        }

        protected override void UnwatchProperty(GudlExpression expression)
        {
            base.UnwatchProperty(expression);
        }

        private Task<IDisposable> LimitPolling(bool value_known)
        {
            return Root.LimitPolling(Peer, value_known);
        }

        public async override Task<(bool, int, int)> GetClickablePoint()
        {
            var result = await base.GetClickablePoint();
            if (result.Item1)
                return result;

            try
            {
                var bounds = await CallMethod(Root.Connection, Peer, Path,
                    IFACE_COMPONENT, "GetExtents", (uint)0, ReadMessageExtents);
                return (true, bounds.Item1 + bounds.Item3 / 2, bounds.Item2 + bounds.Item4 / 2);
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return (false, 0, 0);
            }
        }
    }
}
