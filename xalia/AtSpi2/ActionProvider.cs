using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class ActionProvider : IUiDomProvider
    {
        public ActionProvider(AccessibleProvider accessible)
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
            { "action", "spi_action" },
        };

        public string[] Actions { get; private set; }
        private bool fetching_actions;

        public void DumpProperties(UiDomElement element)
        {
            if (!(Actions is null))
                Utils.DebugWriteLine($"  spi_action: [{string.Join(",", Actions)}]");
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "spi_action":
                    depends_on.Add((element, new IdentifierExpression("spi_action")));
                    if (!(Actions is null))
                        return new AtSpiActionList(this);
                    return UiDomUndefined.Instance;
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
            return false;
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_action":
                        if (!fetching_actions)
                        {
                            fetching_actions = true;
                            Utils.RunTask(FetchActions());
                        }
                        return true;
                }
            }
            return false;
        }

        private async Task FetchActions()
        {
            string[] result;
            try
            {
                int count = (int)await GetProperty(Connection.Connection, Peer, Path, IFACE_ACTION, "NActions");
                result = new string[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = await CallMethod(Connection.Connection, Peer, Path, IFACE_ACTION,
                        "GetName", i, ReadMessageString);
                }
            }
            catch (DBusException e)
            {
                if (!AtSpiElement.IsExpectedException(e, "org.freedesktop.DBus.Error.Failed"))
                    throw;
                return;
            }
            Actions = result;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"{Element}.spi_action: ({string.Join(",", Actions)})");
            Element.PropertyChanged("spi_action");
        }

        public async Task DoAction(int index)
        {
            bool success;
            try
            {
                success = await CallMethod(Connection.Connection, Peer, Path,
                    IFACE_ACTION, "DoAction", index, ReadMessageBoolean);
            }
            catch (DBusException e)
            {
                if (!AtSpiElement.IsExpectedException(e))
                    throw;
                return;
            }
            if (!success)
            {
                Utils.DebugWriteLine($"WARNING: {Element}.spi_action({index}) failed");
            }
        }
    }
}
