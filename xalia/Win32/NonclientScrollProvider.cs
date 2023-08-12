using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class NonclientScrollProvider : NonclientProvider
    {
        public NonclientScrollProvider(HwndProvider hwndProvider, UiDomElement element, bool vertical) : base(hwndProvider, element)
        {
            Vertical = vertical;
        }

        static UiDomEnum role = new UiDomEnum(new string[] { "scroll_bar", "scrollbar" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "vertical", "win32_vertical" },
            { "horizontal", "win32_horizontal" },
            { "x", "win32_x" },
            { "y", "win32_y" },
            { "width", "win32_width" },
            { "height", "win32_height" },
            { "state", "win32_state" },
        };

        public bool Vertical { get; }

        const int SBI_LOCATION = 0;
        const int SBI_STATE = 1;
        const int SBI_NUM_CONSTANTS = 2;

        private int _watchingSbi; // bitfield of 1<<SBI_*
        private int _sbiKnown; // bitfield of 1<<SBI_*
        private int[] _sbiChangeCount = new int[SBI_NUM_CONSTANTS]; // indexed by SBI_*
        private SCROLLBARINFO _sbi;

        private bool SbiKnown(int which)
        {
            return (_sbiKnown & (1 << which)) != 0;
        }

        private void SetSbiKnown(int which)
        {
            _sbiKnown |= 1 << which;
        }

        private void ClearSbiKnown(int which)
        {
            _sbiKnown &= ~(1 << which);
        }

        private bool WatchingSbi(int which)
        {
            return (_watchingSbi & (1 << which)) != 0;
        }

        private void SetWatchingSbi(int which)
        {
            _watchingSbi |= 1 << which;
        }

        private void ClearWatchingSbi(int which)
        {
            _watchingSbi &= ~(1 << which);
        }

        public override void DumpProperties(UiDomElement element)
        {
            if (SbiKnown(SBI_LOCATION))
                Utils.DebugWriteLine($"  win32_pos: ({_sbi.rcScrollBar.left},{_sbi.rcScrollBar.top})-({_sbi.rcScrollBar.right},{_sbi.rcScrollBar.bottom})");
            if (SbiKnown(SBI_STATE))
                Utils.DebugWriteLine($"  win32_state: {_sbi.rgstate[0]}");
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_nonclient_scrollbar":
                case "is_nonclient_scroll_bar":
                    return UiDomBoolean.True;
                case "win32_vertical":
                    return UiDomBoolean.FromBool(Vertical);
                case "win32_horizontal":
                    return UiDomBoolean.FromBool(!Vertical);
                case "win32_x":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (SbiKnown(SBI_LOCATION))
                        return new UiDomInt(_sbi.rcScrollBar.left);
                    return UiDomUndefined.Instance;
                case "win32_y":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (SbiKnown(SBI_LOCATION))
                        return new UiDomInt(_sbi.rcScrollBar.top);
                    return UiDomUndefined.Instance;
                case "win32_width":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (SbiKnown(SBI_LOCATION))
                        return new UiDomInt(_sbi.rcScrollBar.width);
                    return UiDomUndefined.Instance;
                case "win32_height":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (SbiKnown(SBI_LOCATION))
                        return new UiDomInt(_sbi.rcScrollBar.height);
                    return UiDomUndefined.Instance;
                case "win32_state":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (SbiKnown(SBI_STATE))
                        return new UiDomInt(_sbi.rgstate[0]);
                    return UiDomUndefined.Instance;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "scrollbar":
                case "scroll_bar":
                    return UiDomBoolean.True;
                case "enabled":
                    depends_on.Add((element, new IdentifierExpression("win32_state")));
                    if (SbiKnown(SBI_STATE))
                        return UiDomBoolean.FromBool((_sbi.rgstate[0] & STATE_SYSTEM_UNAVAILABLE) == 0);
                    return UiDomUndefined.Instance;
                case "visible":
                    depends_on.Add((element, new IdentifierExpression("win32_state")));
                    if (SbiKnown(SBI_STATE))
                        return UiDomBoolean.FromBool((_sbi.rgstate[0] & (STATE_SYSTEM_INVISIBLE|STATE_SYSTEM_OFFSCREEN)) == 0);
                    return UiDomUndefined.Instance;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_pos":
                        {
                            SetWatchingSbi(SBI_LOCATION);
                            if (!SbiKnown(SBI_LOCATION))
                                Utils.RunTask(RefreshScrollBarInfo());
                            return true;
                        }
                    case "win32_state":
                        {
                            SetWatchingSbi(SBI_STATE);
                            if (!SbiKnown(SBI_STATE))
                                Utils.RunTask(RefreshScrollBarInfo());
                            return true;
                        }
                }
            }
            return base.WatchProperty(element, expression);
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_pos":
                        {
                            ClearWatchingSbi(SBI_LOCATION);
                            return true;
                        }
                    case "win32_state":
                        {
                            ClearWatchingSbi(SBI_STATE);
                            return true;
                        }
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        private bool AnySbiInfoCurrent(int[] old_change_count)
        {
            for (int i = 0; i < SBI_NUM_CONSTANTS; i++)
            {
                if (old_change_count[i] == _sbiChangeCount[i])
                {
                    return true;
                }
            }
            return false;
        }

        private async Task RefreshScrollBarInfo()
        {
            var old_change_count = (int[])_sbiChangeCount.Clone();
            SCROLLBARINFO sbi;
            try
            {
                sbi = await Connection.CommandThread.OnBackgroundThread(() =>
                {
                    if (!AnySbiInfoCurrent(old_change_count))
                        return default;
                    SCROLLBARINFO _sbi = new SCROLLBARINFO();
                    _sbi.cbSize = Marshal.SizeOf<SCROLLBARINFO>();
                    if (!GetScrollBarInfo(Hwnd, Vertical ? OBJID_VSCROLL : OBJID_HSCROLL, ref _sbi))
                        throw new Win32Exception();
                    return _sbi;
                }, Tid);
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
                return;
            }
            if (sbi.cbSize == 0 || !AnySbiInfoCurrent(old_change_count))
                return;

            UpdateScrollBarInfo(sbi, old_change_count);
        }

        private void UpdateScrollBarInfo(SCROLLBARINFO sbi, int[] old_change_count)
        {
            var old_sbi = _sbi;
            _sbi = sbi;

            bool new_location_known = !sbi.rcScrollBar.IsEmpty();
            if (old_change_count[SBI_LOCATION] == _sbiChangeCount[SBI_LOCATION] &&
                (new_location_known != SbiKnown(SBI_LOCATION) ||
                 !sbi.rcScrollBar.Equals(old_sbi.rcScrollBar)))
            {
                if (new_location_known)
                    SetSbiKnown(SBI_LOCATION);
                else
                    ClearSbiKnown(SBI_LOCATION);
                if (Element.MatchesDebugCondition())
                {
                    if (new_location_known)
                        Utils.DebugWriteLine($"{Element}.win32_pos: ({sbi.rcScrollBar.left},{sbi.rcScrollBar.top})-({sbi.rcScrollBar.right},{sbi.rcScrollBar.bottom})");
                    else
                        Utils.DebugWriteLine($"{Element}.win32_pos: undefined");
                }
                Element.PropertyChanged("win32_pos");
            }

            if (old_change_count[SBI_STATE] == _sbiChangeCount[SBI_STATE] &&
                (!SbiKnown(SBI_STATE) || sbi.rgstate[0] != old_sbi.rgstate[0]))
            {
                SetSbiKnown(SBI_STATE);
                Element.PropertyChanged("win32_state", sbi.rgstate[0]);
            }
        }

        private void SbiChanged(int which)
        {
            _sbiChangeCount[which]++;
            if (WatchingSbi(which))
                Utils.RunTask(RefreshScrollBarInfo());
            else
                ClearSbiKnown(which);
        }

        public void ParentLocationChanged()
        {
            SbiChanged(SBI_LOCATION);
            SbiChanged(SBI_STATE); // Sometimes resizing enables/disables scrollbar with no state changed event
        }

        public void MsaaStateChange()
        {
            SbiChanged(SBI_STATE);
        }
    }
}