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
    internal class HwndListBoxItemProvider : UiDomProviderBase, IWin32LocationChange, IUiDomScrollToProvider
    {
        public HwndListBoxItemProvider(HwndListBoxProvider parent, UiDomElement element)
        {
            Parent = parent;
            Element = element;
        }

        public HwndListBoxProvider Parent { get; }

        public UiDomElement Element { get; }

        public UiDomRoot Root => Parent.Root;
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public HwndProvider HwndProvider => Parent.HwndProvider;
        public CommandThread CommandThread => HwndProvider.CommandThread;

        public int ChildId
        {
            get
            {
                return Element.IndexInParent + Parent.FirstChildId;
            }
        }

        static readonly UiDomEnum role = new UiDomEnum(new string[] { "list_item", "listitem" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "x", "win32_x" },
            { "y", "win32_y" },
            { "width", "win32_width" },
            { "height", "win32_height" },
        };

        private RECT bounds_rect;
        private bool watching_bounds;
        private bool bounds_known;

        public override void DumpProperties(UiDomElement element)
        {
            Utils.DebugWriteLine($"  msaa_child_id: {ChildId}");
            Parent.HwndProvider.ChildDumpProperties();
            if (bounds_known)
            {
                Utils.DebugWriteLine($"  win32_x: {bounds_rect.left}");
                Utils.DebugWriteLine($"  win32_y: {bounds_rect.top}");
                Utils.DebugWriteLine($"  win32_width: {bounds_rect.width}");
                Utils.DebugWriteLine($"  win32_height: {bounds_rect.height}");
            }
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_list_box_item":
                case "is_hwnd_listbox_item":
                case "is_hwnd_subelement":
                    return UiDomBoolean.True;
                case "win32_x":
                    depends_on.Add((Element, new IdentifierExpression("win32_bounds")));
                    if (bounds_known)
                        return new UiDomInt(bounds_rect.left);
                    break;
                case "win32_y":
                    depends_on.Add((Element, new IdentifierExpression("win32_bounds")));
                    if (bounds_known)
                        return new UiDomInt(bounds_rect.top);
                    break;
                case "win32_width":
                    depends_on.Add((Element, new IdentifierExpression("win32_bounds")));
                    if (bounds_known)
                        return new UiDomInt(bounds_rect.width);
                    break;
                case "win32_height":
                    depends_on.Add((Element, new IdentifierExpression("win32_bounds")));
                    if (bounds_known)
                        return new UiDomInt(bounds_rect.height);
                    break;
            }
            return Parent.HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "role":
                case "control_type":
                    return role;
                case "listitem":
                case "list_item":
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return Parent.HwndProvider.ChildEvaluateIdentifierLate(identifier, depends_on);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_bounds":
                        watching_bounds = true;
                        if (!bounds_known)
                            Utils.RunTask(RefreshBounds(CommandThreadPriority.Query));
                        return true;
                }
            }
            return base.WatchProperty(element, expression);
        }

        unsafe private RECT GetItemRectBackground()
        {
            RECT rect = new RECT();
            IntPtr result = SendMessageW(HwndProvider.Hwnd, LB_GETITEMRECT,
                (IntPtr)(ChildId - 1), (IntPtr)(&rect));
            if (unchecked((int)result) == LB_ERR)
            {
                return new RECT();
            }
            return rect;
        }

        private async Task RefreshBounds(CommandThreadPriority priority)
        {
            Utils.DebugWriteLine("RefreshBounds --> GetItemRectBackground");
            var rc = await CommandThread.OnBackgroundThread(GetItemRectBackground,
                priority);
            Utils.DebugWriteLine("RefreshBounds <-- GetItemRectBackground");

            if (rc.IsEmpty())
                return;

            rc = HwndProvider.ClientRectToScreen(rc);

            if (!bounds_known || !rc.Equals(bounds_rect))
            {
                bounds_known = true;
                bounds_rect = rc;
                Element.PropertyChanged("win32_bounds", $"{rc.left},{rc.top} {rc.width}x{rc.height}");
            }
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_bounds":
                        watching_bounds = false;
                        return true;
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        public void InvalidateBounds()
        {
            if (bounds_known)
            {
                if (watching_bounds)
                    Utils.RunTask(RefreshBounds(CommandThreadPriority.Query));
                else
                    bounds_known = false;
            }
        }

        public void MsaaLocationChange()
        {
            InvalidateBounds();
        }

        public override async Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            if (!bounds_known)
                await RefreshBounds(CommandThreadPriority.User);
            if (bounds_known)
            {
                var bounds = bounds_rect;
                return (true, bounds.left + bounds.width / 2, bounds.top + bounds.height / 2);
            }
            return await base.GetClickablePointAsync(element);
        }

        public async Task<bool> ScrollToAsync()
        {
            if ((HwndProvider.Style & (WS_VSCROLL | WS_HSCROLL)) == 0)
                return true;

            SCROLLINFO info;
            try
            {
                info = await CommandThread.OnBackgroundThread(() =>
                {
                    var si = new SCROLLINFO();
                    si.cbSize = Marshal.SizeOf<SCROLLINFO>();
                    si.fMask = SIF_PAGE | SIF_POS | SIF_RANGE;
                    if (!GetScrollInfo(Hwnd, ((HwndProvider.Style & LBS_MULTICOLUMN) != 0) ? SB_VERT : SB_HORZ, ref si))
                        throw new Win32Exception();

                    return si;
                }, CommandThreadPriority.User);
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
                return false;
            }

            if (info.cbSize == 0)
                return false;

            int index = ChildId - 1;
            int value;
            if (index < info.nPos)
                value = index;
            else if (index >= info.nPos + info.nPage)
                value = index - info.nPage + 1;
            else
                // Item already in view
                return true;

            int msg = ((HwndProvider.Style & LBS_MULTICOLUMN) != 0) ? WM_HSCROLL : WM_VSCROLL;
            await SendMessageAsync(Hwnd, msg, MAKEWPARAM(SB_THUMBTRACK, unchecked((ushort)value)), IntPtr.Zero);
            await SendMessageAsync(Hwnd, msg, MAKEWPARAM(SB_THUMBPOSITION, unchecked((ushort)value)), IntPtr.Zero); ;
            await SendMessageAsync(Hwnd, msg, MAKEWPARAM(SB_ENDSCROLL, 0), IntPtr.Zero);

            // We may not get a notification of the change
            Utils.RunIdle(UpdatedScroll);

            return true;
        }

        private void UpdatedScroll()
        {
            Parent.MsaaScrolled(0);
        }
    }
}
