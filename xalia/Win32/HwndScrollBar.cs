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
    internal class HwndScrollBar : UiDomProviderBase, IWin32Styles, IUiDomValueProvider
    {
        public HwndScrollBar(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;
        public CommandThread CommandThread => HwndProvider.CommandThread;

        static UiDomEnum role = new UiDomEnum(new string[] { "scroll_bar", "scrollbar" });

        public void GetStyleNames(int style, List<string> names)
        {
            if ((style & SBS_SIZEBOX) != 0)
                names.Add("sizebox");
            if ((style & SBS_SIZEGRIP) != 0)
                names.Add("sizegrip");
            if ((style & SBS_VERT) != 0)
                names.Add("vert");
            if ((style & (SBS_SIZEBOX|SBS_SIZEGRIP)) != 0)
            {
                if ((style & SBS_TOPALIGN) != 0)
                    names.Add("sizeboxtopleftalign");
                if ((style & SBS_BOTTOMALIGN) != 0)
                    names.Add("sizeboxbottomrightalign");
            }
            else if ((style & SBS_VERT) != 0)
            {
                if ((style & SBS_TOPALIGN) != 0)
                    names.Add("leftalign");
                if ((style & SBS_BOTTOMALIGN) != 0)
                    names.Add("rightalign");
            }
            else
            {
                names.Add("horz");
                if ((style & SBS_TOPALIGN) != 0)
                    names.Add("topalign");
                if ((style & SBS_BOTTOMALIGN) != 0)
                    names.Add("bottomalign");
            }
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_scroll_bar":
                case "is_hwnd_scrollbar":
                    return UiDomBoolean.True;
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
                case "role":
                case "control_type":
                    return role;
                case "horz":
                case "horizontal":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP)) == 0);
                case "vert":
                case "vertical":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & SBS_VERT) != 0);
                case "topalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP | SBS_TOPALIGN)) == SBS_TOPALIGN);
                case "bottomalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP | SBS_BOTTOMALIGN)) == SBS_BOTTOMALIGN);
                case "leftalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP | SBS_TOPALIGN)) == (SBS_VERT | SBS_TOPALIGN));
                case "rightalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_VERT | SBS_SIZEBOX | SBS_SIZEGRIP | SBS_BOTTOMALIGN)) == (SBS_VERT | SBS_BOTTOMALIGN));
                case "sizeboxtopleftalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_SIZEBOX | SBS_SIZEGRIP)) != 0 &&
                        (HwndProvider.Style & (SBS_VERT | SBS_TOPALIGN)) == SBS_TOPALIGN);
                case "sizeboxbottomrightalign":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (SBS_SIZEBOX | SBS_SIZEGRIP)) != 0 &&
                        (HwndProvider.Style & (SBS_VERT | SBS_BOTTOMALIGN)) == SBS_BOTTOMALIGN);
                case "sizebox":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & SBS_SIZEBOX) != 0);
                case "sizegrip":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & SBS_SIZEGRIP) != 0);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        private int CalculateMinimumIncrement(SCROLLBARINFO sbi, SCROLLINFO si)
        {
            bool vertical = (HwndProvider.Style & SBS_VERT) != 0;

            var scrollbar_size = vertical ? sbi.rcScrollBar.height : sbi.rcScrollBar.width;

            var desired_scroll_pixels = 25.0 * HwndProvider.GetWindowMonitorDpi(vertical) / 96.0;

            var result = (int)Math.Round(desired_scroll_pixels * si.nPage / scrollbar_size);

            if (result == 0)
                return 1;

            return result;
        }

        public async Task<double> GetMinimumIncrementAsync(UiDomElement element)
        {
            try
            {
                return await CommandThread.OnBackgroundThread(() =>
                {
                    var sbi = new SCROLLBARINFO();
                    sbi.cbSize = Marshal.SizeOf<SCROLLBARINFO>();
                    if (!GetScrollBarInfo(Hwnd, OBJID_CLIENT, ref sbi))
                        throw new Win32Exception();

                    var si = new SCROLLINFO();
                    si.cbSize = Marshal.SizeOf<SCROLLINFO>();
                    si.fMask = SIF_PAGE;
                    if (!GetScrollInfo(Hwnd, SB_CTL, ref si))
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
            bool vertical = (HwndProvider.Style & SBS_VERT) != 0;
            await CommandThread.OnBackgroundThread(() =>
            {
                var si = new SCROLLINFO();
                si.cbSize = Marshal.SizeOf<SCROLLINFO>();
                si.fMask = SIF_POS;
                si.nPos = value;

                SetScrollInfo(Hwnd, SB_CTL, ref si, true);
            }, CommandThreadPriority.Query);
            int msg = vertical ? WM_VSCROLL : WM_HSCROLL;
            IntPtr hwnd = GetAncestor(Hwnd, GA_PARENT);
            await SendMessageAsync(hwnd, msg, MAKEWPARAM(SB_THUMBTRACK, unchecked((ushort)value)), Hwnd);
            await SendMessageAsync(hwnd, msg, MAKEWPARAM(SB_THUMBPOSITION, unchecked((ushort)value)), Hwnd); ;
            await SendMessageAsync(hwnd, msg, MAKEWPARAM(SB_ENDSCROLL, 0), Hwnd);
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
                    if (!GetScrollInfo(Hwnd, SB_CTL, ref bg_si))
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
    }
}
