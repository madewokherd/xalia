using System;
using System.Threading.Tasks;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListBoxScrollProvider : UiDomProviderBase, IUiDomValueProvider
    {
        public HwndListBoxScrollProvider(NonclientScrollProvider scroll, HwndListBoxProvider parent)
        {
            Scroll = scroll;
            Parent = parent;
        }

        public NonclientScrollProvider Scroll { get; }
        public HwndListBoxProvider Parent { get; }

        public Task<double> GetMinimumIncrementAsync(UiDomElement element)
        {
            return Task.FromResult(1.0);
        }

        double remainder;

        public async Task<bool> OffsetValueAsync(UiDomElement element, double offset)
        {
            bool result = true;
            offset += remainder;
            if (offset <= -1 || offset >= 1)
            {
                var view_info = await Parent.GetViewInfoAsync();
                var int_offset = (int)Math.Truncate(offset);

                var new_index = view_info.top_index + int_offset;

                if (new_index < 0)
                    new_index = 0;
                else if (new_index >= view_info.item_count)
                    new_index = view_info.item_count - 1;

                if (new_index != view_info.top_index)
                    await SendMessageAsync(Parent.Hwnd, LB_SETTOPINDEX, new IntPtr(new_index), IntPtr.Zero);

                offset -= int_offset;
            }
            remainder = offset;
            return result;
        }
    }
}