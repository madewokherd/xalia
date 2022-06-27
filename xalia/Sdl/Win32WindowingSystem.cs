using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using SDL2;

namespace Xalia.Sdl
{
    internal class Win32WindowingSystem : WindowingSystem
    {
        const string USER_LIB = "user32";

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        extern static IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        extern static IntPtr GetWindowLongW(IntPtr hwnd, int index);

        static IntPtr GetWindowLong(IntPtr hwnd, int index)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtrW(hwnd, index);
            else
                return GetWindowLongW(hwnd, index);
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        extern static IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr new_long);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        extern static IntPtr SetWindowLongW(IntPtr hwnd, int index, IntPtr new_long);

        static IntPtr SetWindowLong(IntPtr hwnd, int index, IntPtr new_long)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtrW(hwnd, index, new_long);
            else
                return SetWindowLongW(hwnd, index, new_long);
        }

        const int GWLP_EXSTYLE = -20;

        const int WS_EX_NOACTIVATE = 0x08000000;
        const int WS_EX_TOPMOST = 0x00000008;

        public IntPtr GetWindowHandle(IntPtr sdl_window)
        {
            SDL.SDL_SysWMinfo info = default;

            SDL.SDL_VERSION(out info.version);

            if (SDL.SDL_GetWindowWMInfo(sdl_window, ref info) != SDL.SDL_bool.SDL_TRUE)
                throw new Exception(SDL.SDL_GetError());

            return info.info.win.window;
        }

        public override void CustomizeOverlayWindow(IntPtr sdl_window)
        {
            var win32_window = GetWindowHandle(sdl_window);

            var old_exstyle = GetWindowLong(win32_window, GWLP_EXSTYLE);

            IntPtr new_exstyle = (IntPtr)((int)old_exstyle | WS_EX_NOACTIVATE | WS_EX_TOPMOST);

            SetWindowLong(win32_window, GWLP_EXSTYLE, new_exstyle);

            base.CustomizeOverlayWindow(sdl_window);
        }
    }
}
