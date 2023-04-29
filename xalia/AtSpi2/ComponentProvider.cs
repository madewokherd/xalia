using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class ComponentProvider : IUiDomProvider
    {
        public ComponentProvider(AccessibleProvider accessible)
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
            { "x", "spi_abs_x" },
            { "y", "spi_abs_y" },
            { "width", "spi_abs_width" },
            { "height", "spi_abs_height" },
            { "abs_x", "spi_abs_x" },
            { "abs_y", "spi_abs_y" },
            { "abs_width", "spi_abs_width" },
            { "abs_height", "spi_abs_height" },
        };

        public bool AbsPosKnown { get; private set; }
        public int AbsX { get; private set; }
        public int AbsY { get; private set; }
        public int AbsWidth { get; private set; }
        public int AbsHeight { get; private set; }
        private bool watching_abs_pos;
        private int abs_pos_change_count;

        public void DumpProperties(UiDomElement element)
        {
            if (AbsPosKnown)
            {
                Utils.DebugWriteLine($"  spi_abs_x: {AbsX}");
                Utils.DebugWriteLine($"  spi_abs_y: {AbsY}");
                Utils.DebugWriteLine($"  spi_abs_width: {AbsWidth}");
                Utils.DebugWriteLine($"  spi_abs_height: {AbsHeight}");
            }
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "spi_abs_x":
                    depends_on.Add((element, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsX);
                    return UiDomUndefined.Instance;
                case "spi_abs_y":
                    depends_on.Add((element, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsY);
                    return UiDomUndefined.Instance;
                case "spi_abs_width":
                    depends_on.Add((element, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsWidth);
                    return UiDomUndefined.Instance;
                case "spi_abs_height":
                    depends_on.Add((element, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsHeight);
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

        public async Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            (int, int, int, int) extents;
            try
            {
                extents = await CallMethod(Connection.Connection, Peer, Path,
                    IFACE_COMPONENT, "GetExtents", ATSPI_COORD_TYPE_SCREEN, ReadMessageExtents);
            }
            catch (DBusException e)
            {
                if (!AtSpiConnection.IsExpectedException(e))
                    throw;
                return (false, 0, 0);
            }
            return (true, extents.Item1 + extents.Item3 / 2, extents.Item2 + extents.Item4 / 2);
        }

        public string[] GetTrackedProperties()
        {
            return null;
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
            watching_abs_pos = false;
            AbsPosKnown = false;
        }

        public void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
        }

        public bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_abs_pos":
                        element.EndPollProperty(expression);
                        watching_abs_pos = false;
                        AbsPosKnown = false;
                        return true;
                }
            }
            return false;
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_abs_pos":
                        if (!watching_abs_pos)
                        {
                            watching_abs_pos = true;
                            element.PollProperty(expression, FetchAbsPos, 2000);
                        }
                        return true;
                }
            }
            return false;
        }

        private Task FetchAbsPos()
        {
            return FetchAbsPos(false);
        }

        private async Task FetchAbsPos(bool from_event)
        {
            (int, int, int, int) result;
            int old_change_count = abs_pos_change_count;
            using (var poll = await Accessible.LimitPolling(AbsPosKnown && !from_event))
            {
                if (!watching_abs_pos)
                    return;
                if (old_change_count != abs_pos_change_count)
                    return;
                try
                {
                    await Connection.RegisterEvent("object:bounds-changed");

                    result = await CallMethod(Connection.Connection, Peer, Path,
                        IFACE_COMPONENT, "GetExtents", ATSPI_COORD_TYPE_SCREEN, ReadMessageExtents);
                }
                catch (DBusException e)
                {
                    if (!AtSpiConnection.IsExpectedException(e))
                        throw;
                    return;
                }
            }
            if (old_change_count != abs_pos_change_count)
                return;
            if (watching_abs_pos && (!AbsPosKnown || result != (AbsX, AbsY, AbsWidth, AbsHeight)))
            {
                AbsPosKnown = true;
                AbsX = result.Item1;
                AbsY = result.Item2;
                AbsWidth = result.Item3;
                AbsHeight = result.Item4;
                if (Element.MatchesDebugCondition())
                    Utils.DebugWriteLine($"{Element}.spi_abs_(x,y,width,height): {result}");
                Element.PropertyChanged("spi_abs_pos");
            }
        }

        internal void AncestorBoundsChanged()
        {
            abs_pos_change_count++;
            if (watching_abs_pos)
            {
                Utils.RunTask(FetchAbsPos(true));
            }
        }
    }
}
