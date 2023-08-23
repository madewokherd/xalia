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

        public Task<double> GetMinimumIncrementAsync(UiDomElement element)
        {
            return Task.FromResult(0.0);
        }

        double remainder;

        public async Task<bool> OffsetValueAsync(UiDomElement element, double offset)
        {
            bool result = true;
            offset += remainder;
            if (offset <= -1 || offset >= 1)
            {
                var int_offset = (int)Math.Round(offset);
                if (Vertical)
                    result = await SendMessageAsync(Hwnd, LVM_SCROLL, IntPtr.Zero, new IntPtr(int_offset)) != IntPtr.Zero;
                else
                    result = await SendMessageAsync(Hwnd, LVM_SCROLL, new IntPtr(int_offset), IntPtr.Zero) != IntPtr.Zero;
                offset -= int_offset;
            }
            remainder = offset;
            return result;
        }
    }
}