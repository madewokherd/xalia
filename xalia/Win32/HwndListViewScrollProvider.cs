using System;
using System.Threading.Tasks;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListViewScrollProvider : UiDomProviderBase, IUiDomValueProvider
    {
        public HwndListViewScrollProvider(HwndListViewProvider parent, NonclientScrollProvider scroll)
        {
            Parent = parent;
            Scroll = scroll;
        }

        public HwndListViewProvider Parent { get; }
        public NonclientScrollProvider Scroll { get; }

        public bool Vertical => Scroll.Vertical;
        public IntPtr Hwnd => Parent.Hwnd;

        public async Task<double> GetMinimumIncrementAsync(UiDomElement element)
        {
            switch (await Parent.GetViewAsync())
            {
                case LV_VIEW_DETAILS:
                    if (Vertical)
                        // One item should always be reasonable
                        return 1.0;
                    break;
            }
            return 0.0; // fallback on default scrollbar implementation
        }

        double remainder;

        public async Task<bool> OffsetValueAsync(UiDomElement element, double offset)
        {
            bool result = true;
            offset += remainder;
            if (offset <= -1 || offset >= 1)
            {
                var int_offset = (int)Math.Round(offset);
                var remote_int_offset = int_offset;

                var view = await Parent.GetViewAsync();

                switch (view)
                {
                    case LV_VIEW_DETAILS:
                        if (Vertical)
                        {
                            // Can only scroll vertically in item increments. LVM_SCROLL expects
                            // pixels, but the win32 scroll info is by item index.
                            var bounds = await Parent.GetItemRectAsync(0, LVIR_SELECTBOUNDS);
                            remote_int_offset *= bounds.height;
                        }
                        break;
                }

                if (Vertical)
                    result = await SendMessageAsync(Hwnd, LVM_SCROLL, IntPtr.Zero, new IntPtr(remote_int_offset)) != IntPtr.Zero;
                else
                    result = await SendMessageAsync(Hwnd, LVM_SCROLL, new IntPtr(remote_int_offset), IntPtr.Zero) != IntPtr.Zero;

                offset -= int_offset;
            }
            remainder = offset;
            return result;
        }
    }
}