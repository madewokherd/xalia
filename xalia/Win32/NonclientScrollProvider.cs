using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class NonclientScrollProvider : NonclientProvider, IUiDomValueProvider
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
            { "minimum_value", "win32_minimum_value" },
            { "maximum_value", "win32_maximum_value" },
            { "large_change", "win32_page" },
            { "value", "win32_value" },
        };

        public bool Vertical { get; }

        const int SBI_LOCATION = 0;
        const int SBI_STATE = 1;
        const int SBI_NUM_CONSTANTS = 2;

        private int _watchingSbi; // bitfield of 1<<SBI_*
        private int _sbiKnown; // bitfield of 1<<SBI_*
        private int[] _sbiChangeCount = new int[SBI_NUM_CONSTANTS]; // indexed by SBI_*
        private SCROLLBARINFO _sbi;

        private bool _watchingSi;
        private bool _siKnown;
        private int _siChangeCount;
        private SCROLLINFO _si;

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
            if (_siKnown)
            {
                Utils.DebugWriteLine($"  win32_minimum_value: {_si.nMin}");
                Utils.DebugWriteLine($"  win32_maximum_value: {_si.max_value}");
                Utils.DebugWriteLine($"  win32_page: {_si.nPage}");
                Utils.DebugWriteLine($"  win32_value: {_si.nPos}");
            }
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
                case "win32_minimum_value":
                    depends_on.Add((element, new IdentifierExpression("win32_value")));
                    if (_siKnown)
                        return new UiDomInt(_si.nMin);
                    return UiDomUndefined.Instance;
                case "win32_maximum_value":
                    depends_on.Add((element, new IdentifierExpression("win32_value")));
                    if (_siKnown)
                        return new UiDomInt(_si.max_value);
                    return UiDomUndefined.Instance;
                case "win32_page":
                    depends_on.Add((element, new IdentifierExpression("win32_value")));
                    if (_siKnown)
                        return new UiDomInt(_si.nPage);
                    return UiDomUndefined.Instance;
                case "win32_value":
                    depends_on.Add((element, new IdentifierExpression("win32_value")));
                    if (_siKnown)
                        return new UiDomInt(_si.nPos);
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
                case "minimum_increment":
                case "small_change":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    depends_on.Add((element, new IdentifierExpression("win32_value")));
                    // win32 does not provide a way to determine this, so guess based on the size and nPage
                    if (SbiKnown(SBI_LOCATION) && _siKnown)
                    {
                        return new UiDomInt(CalculateMinimumIncrement(_sbi, _si));
                    }
                    return UiDomUndefined.Instance;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        private int CalculateMinimumIncrement(SCROLLBARINFO sbi, SCROLLINFO si)
        {
            var scrollbar_size = Vertical ? sbi.rcScrollBar.height : sbi.rcScrollBar.width;

            var desired_scroll_pixels = 25.0 * HwndProvider.GetWindowMonitorDpi(Vertical) / 96.0;

            var result = (int)Math.Round(desired_scroll_pixels * si.nPage / scrollbar_size);

            if (result == 0)
                return 1;

            return result;
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
                    case "win32_value":
                        {
                            _watchingSi = true;
                            if (!_siKnown)
                                Utils.RunTask(RefreshScrollInfo());
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
                    case "win32_value":
                        {
                            _watchingSi = false;
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
                sbi = await CommandThread.OnBackgroundThread(() =>
                {
                    if (!AnySbiInfoCurrent(old_change_count))
                        return default;
                    SCROLLBARINFO _sbi = new SCROLLBARINFO();
                    _sbi.cbSize = Marshal.SizeOf<SCROLLBARINFO>();
                    if (!GetScrollBarInfo(Hwnd, Vertical ? OBJID_VSCROLL : OBJID_HSCROLL, ref _sbi))
                        throw new Win32Exception();
                    return _sbi;
                }, CommandThreadPriority.Query);
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e,
                    0 // Wine bug: GetScrollBarInfo doesn't work cross-process
                    ))
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
        }

        public void MsaaStateChange()
        {
            SbiChanged(SBI_STATE);
        }

        private async Task RefreshScrollInfo()
        {
            int old_change_count = _siChangeCount;
            SCROLLINFO new_si;
            try
            {
                new_si = await CommandThread.OnBackgroundThread(() =>
                {
                    if (old_change_count != _siChangeCount)
                        return default;
                    var si = new SCROLLINFO();
                    si.cbSize = Marshal.SizeOf<SCROLLINFO>();
                    si.fMask = SIF_PAGE | SIF_POS | SIF_RANGE;
                    if (!GetScrollInfo(Hwnd, Vertical ? SB_VERT : SB_HORZ, ref si))
                        throw new Win32Exception();

                    return si;
                }, CommandThreadPriority.Query);
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
                return;
            }

            if (new_si.cbSize == 0)
                return;

            var old_si = _si;
            _si = new_si;

            if (Element.MatchesDebugCondition())
            {
                if (!_siKnown || old_si.nMin != new_si.nMin)
                    Utils.DebugWriteLine($"{Element}.win32_minimum_value: {new_si.nMin}");
                if (!_siKnown || old_si.max_value != new_si.max_value)
                    Utils.DebugWriteLine($"{Element}.win32_maximum_value: {new_si.max_value}");
                if (!_siKnown || old_si.nPage != new_si.nPage)
                    Utils.DebugWriteLine($"{Element}.win32_page: {new_si.nPage}");
                if (!_siKnown || old_si.nPos != new_si.nPos)
                    Utils.DebugWriteLine($"{Element}.win32_value: {new_si.nPos}");
            }

            _siKnown = true;
            Element.PropertyChanged("win32_value");
        }

        internal void MsaaValueChange()
        {
            SbiChanged(SBI_STATE); // Sometimes resizing enables/disables scrollbar with no state changed event
            _siChangeCount++;
            if (_watchingSi)
                Utils.RunTask(RefreshScrollInfo());
            else
                _siKnown = false;
        }

        public async Task<double> GetMinimumIncrementAsync(UiDomElement element)
        {
            try
            {
                return await CommandThread.OnBackgroundThread(() =>
                {
                    var sbi = new SCROLLBARINFO();
                    sbi.cbSize = Marshal.SizeOf<SCROLLBARINFO>();
                    if (!GetScrollBarInfo(Hwnd, Vertical ? OBJID_VSCROLL : OBJID_HSCROLL, ref sbi))
                        throw new Win32Exception();

                    var si = new SCROLLINFO();
                    si.cbSize = Marshal.SizeOf<SCROLLINFO>();
                    si.fMask = SIF_PAGE;
                    if (!GetScrollInfo(Hwnd, Vertical ? SB_VERT : SB_HORZ, ref si))
                        throw new Win32Exception();

                    return CalculateMinimumIncrement(sbi, si);
                }, CommandThreadPriority.User);
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
                return 1.0;
            }
        }

        public async Task SetValueAsync(int value)
        {
            int msg = Vertical ? WM_VSCROLL : WM_HSCROLL;
            await SendMessageAsync(Hwnd, msg, MAKEWPARAM(SB_THUMBTRACK, unchecked((ushort)value)), IntPtr.Zero);
            await SendMessageAsync(Hwnd, msg, MAKEWPARAM(SB_THUMBPOSITION, unchecked((ushort)value)), IntPtr.Zero); ;
            await SendMessageAsync(Hwnd, msg, MAKEWPARAM(SB_ENDSCROLL, 0), IntPtr.Zero);
        }

        double _offsetRemainder;

        public async Task<bool> OffsetValueAsync(UiDomElement element, double offset)
        {
            try
            {
                SCROLLINFO si = await CommandThread.OnBackgroundThread(() =>
                {
                    var bg_si = new SCROLLINFO();
                    bg_si.cbSize = Marshal.SizeOf<SCROLLINFO>();
                    bg_si.fMask = SIF_POS | SIF_PAGE | SIF_RANGE;
                    if (!GetScrollInfo(Hwnd, Vertical ? SB_VERT : SB_HORZ, ref bg_si))
                        throw new Win32Exception();

                    return bg_si;
                }, CommandThreadPriority.User);

                if (si.max_value <= si.nMin)
                    return false;

                double new_pos = si.nPos + offset + _offsetRemainder;

                if (new_pos < si.nMin)
                    new_pos = si.nMin;
                else if (new_pos > si.max_value)
                    new_pos = si.max_value;

                int new_pos_int = (int)Math.Round(new_pos);

                if (new_pos_int != si.nPos)
                {
                    await SetValueAsync(new_pos_int);
                }

                _offsetRemainder = new_pos - new_pos_int;

                return true;
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
                return false;
            }
        }

        public override async Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            if (SbiKnown(SBI_LOCATION))
            {
                return (true, _sbi.rcScrollBar.left + _sbi.rcScrollBar.width / 2,
                    _sbi.rcScrollBar.top + _sbi.rcScrollBar.height / 2);
            }
            return await CommandThread.OnBackgroundThread(() =>
            {
                var sbi = new SCROLLBARINFO();
                sbi.cbSize = Marshal.SizeOf<SCROLLBARINFO>();
                if (!GetScrollBarInfo(Hwnd, Vertical ? OBJID_VSCROLL : OBJID_HSCROLL, ref sbi))
                    throw new Win32Exception();
                return (true, sbi.rcScrollBar.left + sbi.rcScrollBar.width / 2,
                    sbi.rcScrollBar.top + sbi.rcScrollBar.height / 2);
            }, CommandThreadPriority.User);
        }
    }
}