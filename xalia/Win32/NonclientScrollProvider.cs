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
        };

        public bool Vertical { get; }

        private bool _watchingLocation;
        private bool _locationKnown;
        private int _sbiChangeCount;
        private SCROLLBARINFO _sbi;

        public override void DumpProperties(UiDomElement element)
        {
            if (_locationKnown)
                Utils.DebugWriteLine($"  win32_pos: ({_sbi.rcScrollBar.left},{_sbi.rcScrollBar.top})-({_sbi.rcScrollBar.right},{_sbi.rcScrollBar.bottom})");
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
                    if (_locationKnown)
                        return new UiDomInt(_sbi.rcScrollBar.left);
                    return UiDomUndefined.Instance;
                case "win32_y":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (_locationKnown)
                        return new UiDomInt(_sbi.rcScrollBar.top);
                    return UiDomUndefined.Instance;
                case "win32_width":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (_locationKnown)
                        return new UiDomInt(_sbi.rcScrollBar.width);
                    return UiDomUndefined.Instance;
                case "win32_height":
                    depends_on.Add((element, new IdentifierExpression("win32_pos")));
                    if (_locationKnown)
                        return new UiDomInt(_sbi.rcScrollBar.height);
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
                case "enabled":
                case "visible":
                    return UiDomBoolean.True;
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
                            _watchingLocation = true;
                            if (!_locationKnown)
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
                            _watchingLocation = false;
                            return true;
                        }
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        private async Task RefreshScrollBarInfo()
        {
            var old_change_count = _sbiChangeCount;
            SCROLLBARINFO sbi;
            try
            {
                sbi = await Connection.CommandThread.OnBackgroundThread(() =>
                {
                    if (_sbiChangeCount != old_change_count)
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
            if (_sbiChangeCount != old_change_count || sbi.cbSize == 0)
                return;

            UpdateScrollBarInfo(sbi);
        }

        private void UpdateScrollBarInfo(SCROLLBARINFO sbi)
        {
            var old_sbi = _sbi;
            _sbi = sbi;

            bool new_location_known = !sbi.rcScrollBar.IsEmpty();
            if (new_location_known != _locationKnown ||
                !sbi.rcScrollBar.Equals(old_sbi.rcScrollBar))
            {
                _locationKnown = new_location_known;
                if (Element.MatchesDebugCondition())
                {
                    if (new_location_known)
                        Utils.DebugWriteLine($"{Element}.win32_pos: ({sbi.rcScrollBar.left},{sbi.rcScrollBar.top})-({sbi.rcScrollBar.right},{sbi.rcScrollBar.bottom})");
                    else
                        Utils.DebugWriteLine($"{Element}.win32_pos: undefined");
                }
                Element.PropertyChanged("win32_pos");
            }
        }

        public void ParentLocationChanged()
        {
            _sbiChangeCount++;
            if (_watchingLocation)
                Utils.RunTask(RefreshScrollBarInfo());
            else
                _locationKnown = false;
        }
    }
}