using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using SDL2;

using static Xalia.Interop.Win32;

namespace Xalia.Sdl
{
    internal class Win32WindowingSystem : WindowingSystem
    {
        public override void CustomizeOverlayWindow(OverlayBox box, IntPtr sdl_window)
        {
            var win32_window = GetSdlWindowHwnd(sdl_window);

            var old_exstyle = GetWindowLong(win32_window, GWL_EXSTYLE);

            IntPtr new_exstyle = (IntPtr)((int)old_exstyle | WS_EX_NOACTIVATE);

            SetWindowLong(win32_window, GWL_EXSTYLE, new_exstyle);

            SetWindowPos(win32_window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);

            box.HideWhenResizing = true;

            box.Win32ShapeWorkaround = true;

            base.CustomizeOverlayWindow(box, sdl_window);
        }

        public override bool CanShowKeyboard()
        {
            return true;
        }

        public override Task ShowKeyboardAsync()
        {
            var invocation = new UIHostNoLaunch() as ITipInvocation;

            invocation.Toggle(GetDesktopWindow());

            return Task.CompletedTask;
        }
    }
}
