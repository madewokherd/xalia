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

        private static readonly Dictionary<string, string> property_aliases;

        static AtSpiElement()
        {
            string[] aliases = {
                "x", "spi_abs_x",
                "y", "spi_abs_y",
                "width", "spi_abs_width",
                "height", "spi_abs_height",
                "abs_x", "spi_abs_x",
                "abs_y", "spi_abs_y",
                "abs_width", "spi_abs_width",
                "abs_height", "spi_abs_height",
                "action", "spi_action",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i=0; i<aliases.Length; i+=2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
        }

        public new AtSpiConnection Root { get; }
        public string Peer { get; }
        public string Path { get; }

        public bool AbsPosKnown { get; private set; }
        public int AbsX { get; private set; }
        public int AbsY { get; private set; }
        public int AbsWidth { get; private set; }
        public int AbsHeight { get; private set; }
        private bool watching_abs_pos;
        private int abs_pos_change_count;

        public string[] Actions { get; private set; }
        private bool fetching_actions;

        internal static Dictionary<string, string> name_mapping;

        protected override void SetAlive(bool value)
        {
            if (value)
            {
                Root.NotifyElementCreated(this);
            }
            else
            {
                watching_abs_pos = false;
                AbsPosKnown = false;
                Root.NotifyElementDestroyed(this);
            }
            base.SetAlive(value);
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            UiDomValue value;

            if (property_aliases.TryGetValue(id, out string aliased))
            {
                value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;
                id = aliased;
            }

            switch (id)
            {
                case "spi_abs_x":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsX);
                    return UiDomUndefined.Instance;
                case "spi_abs_y":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsY);
                    return UiDomUndefined.Instance;
                case "spi_abs_width":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsWidth);
                    return UiDomUndefined.Instance;
                case "spi_abs_height":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsHeight);
                    return UiDomUndefined.Instance;
                case "spi_action":
                    depends_on.Add((this, new IdentifierExpression("spi_action")));
                    if (!(Actions is null))
                        return new AtSpiActionList(this);
                    return UiDomUndefined.Instance;
            }

            value = base.EvaluateIdentifierCore(id, root, depends_on);
            if (!value.Equals(UiDomUndefined.Instance))
                return value;

            return UiDomUndefined.Instance;
        }

        protected override void DumpProperties()
        {
            if (AbsPosKnown)
            {
                Utils.DebugWriteLine($"  spi_abs_x: {AbsX}");
                Utils.DebugWriteLine($"  spi_abs_y: {AbsY}");
                Utils.DebugWriteLine($"  spi_abs_width: {AbsWidth}");
                Utils.DebugWriteLine($"  spi_abs_height: {AbsHeight}");
            }
            if (!(Actions is null))
                Utils.DebugWriteLine($"  spi_action: [{String.Join(",", Actions)}]");
            base.DumpProperties();
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_abs_pos":
                        if (!watching_abs_pos)
                        {
                            watching_abs_pos = true;
                            PollProperty(expression, FetchAbsPos, 2000);
                        }
                        break;
                    case "spi_action":
                        if (!fetching_actions)
                        {
                            fetching_actions = true;
                            Utils.RunTask(FetchActions());
                        }
                        break;
                }
            }
            base.WatchProperty(expression);
        }

        private async Task FetchActions()
        {
            string[] result;
            try
            {
                int count = (int)await GetProperty(Root.Connection, Peer, Path, IFACE_ACTION, "NActions");
                result = new string[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = await CallMethod(Root.Connection, Peer, Path, IFACE_ACTION,
                        "GetName", i, ReadMessageString);
                }
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e, "org.freedesktop.DBus.Error.Failed"))
                    throw;
                return;
            }
            Actions = result;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_action: ({string.Join(",", Actions)})");
            PropertyChanged("spi_action");
        }

        protected override void UnwatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_abs_pos":
                        EndPollProperty(expression);
                        watching_abs_pos = false;
                        AbsPosKnown = false;
                        break;
                }
            }
            base.UnwatchProperty(expression);
        }

        private Task FetchAbsPos()
        {
            return FetchAbsPos(false);
        }

        private async Task FetchAbsPos(bool from_event)
        {
            (int, int, int, int) result;
            int old_change_count = abs_pos_change_count;
            using (var poll = await LimitPolling(AbsPosKnown && !from_event))
            {
                if (!watching_abs_pos)
                    return;
                if (old_change_count != abs_pos_change_count)
                    return;
                try
                {
                    await Root.RegisterEvent("object:bounds-changed");

                    result = await CallMethod(Root.Connection, Peer, Path,
                        IFACE_COMPONENT, "GetExtents", (uint)0, ReadMessageExtents);
                }
                catch (DBusException e)
                {
                    if (!IsExpectedException(e))
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
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.spi_abs_(x,y,width,height): {result}");
                PropertyChanged("spi_abs_pos");
            }
        }

        private Task<IDisposable> LimitPolling(bool value_known)
        {
            return Root.LimitPolling(Peer, value_known);
        }

        static bool DebugExceptions = Environment.GetEnvironmentVariable("XALIA_DEBUG_EXCEPTIONS") != "0";

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

        private void AncestorBoundsChanged()
        {
            abs_pos_change_count++;
            if (watching_abs_pos)
            {
                Utils.RunTask(FetchAbsPos(true));
            }
            foreach (var child in Children)
            {
                if (child is AtSpiElement ch)
                {
                    ch.AncestorBoundsChanged();
                }
            }
        }

        internal void AtSpiBoundsChanged(AtSpiSignal signal)
        {
            AncestorBoundsChanged();
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
                if (!IsExpectedException(e))
                    throw;
                return (false, 0, 0);
            }
        }

        public async Task DoAction(int index)
        {
            bool success;
            try
            {
                success = await CallMethod(Root.Connection, Peer, Path,
                    IFACE_ACTION, "DoAction", index, ReadMessageBoolean);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (!success)
            {
                Utils.DebugWriteLine($"WARNING: {this}.spi_action({index}) failed");
            }
        }
    }
}
