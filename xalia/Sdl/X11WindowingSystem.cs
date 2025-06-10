using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using static SDL3.SDL;

using static Xalia.Interop.X11;

namespace Xalia.Sdl
{
    internal class X11WindowingSystem : XdgWindowingSystem
    {
        internal IntPtr display;
        private IntPtr root_window;

        private float dpi;
        private bool dpi_checked;

        private bool xtest_supported;

        private static SDL_X11EventHook x11hook;

        public X11WindowingSystem()
        {
            IntPtr window = SDL_CreateWindow("dummy window", 1, 1, SDL_WindowFlags.SDL_WINDOW_HIDDEN);

            if (window == IntPtr.Zero)
                throw new Exception(SDL_GetError());

            display = SDL_GetPointerProperty(SDL_GetWindowProperties(window), SDL_PROP_WINDOW_X11_DISPLAY_POINTER, IntPtr.Zero);

            SDL_DestroyWindow(window);

            root_window = XDefaultRootWindow(display);

            try
            {
                int _ev = 0, _er = 0, _maj = 0, _min = 0;
                var supported = XTestQueryExtension(display, ref _ev, ref _er, ref _maj, ref _min);

                xtest_supported = (supported != False);
            }
            catch (DllNotFoundException)
            {
                // no libXtst
            }

            EnableInputMask(root_window, PropertyChangeMask);
            
            x11hook = new SDL_X11EventHook(OnX11Event);

            SDL_SetX11EventHook(x11hook, IntPtr.Zero);
        }

        private SDLBool OnX11Event(IntPtr userdata, XEvent xev)
        {
            if (xev.type == PropertyNotify && xev.xproperty.window == root_window &&
                xev.xproperty.atom == XA_RESOURCE_MANAGER)
            {
                dpi_checked = false;
            }

            return true;
        }

        private void EnableInputMask(IntPtr window, IntPtr mask)
        {
            XWindowAttributes window_attributes = default;
            XGetWindowAttributes(display, window, ref window_attributes);

            IntPtr prev_event_mask = window_attributes.your_event_mask;

            if (((long)prev_event_mask & (long)mask) != (long)mask)
            {
                XSelectInput(display, window, (IntPtr)((long)prev_event_mask | (long)mask));
            }
        }

        private string GetStringProperty(IntPtr window, IntPtr property)
        {
            string result;
            int length = 1024;

            while (true)
            {
                var ret = XGetWindowProperty(display, window, property, IntPtr.Zero, (IntPtr)length,
                    False, XA_STRING, out var actual_type, out var actual_format, out var nitems, out var bytes_after,
                    out var prop);
                if (ret != Success || actual_type != XA_STRING || actual_format != 8)
                {
                    return null;
                }

                if (bytes_after != UIntPtr.Zero)
                {
                    length += ((int)bytes_after + 3) / 4;
                    continue;
                }

                result = Marshal.PtrToStringAnsi(prop);
                XFree(prop);
                break;
            }

            return result;
        }

        internal string GetAtSpiBusAddress()
        {
            var XA_AT_SPI_BUS = XInternAtom(display, "AT_SPI_BUS", True);

            if (XA_AT_SPI_BUS != IntPtr.Zero)
            {
                return GetStringProperty(root_window, XA_AT_SPI_BUS);
            }

            return null;
        }

        private void CheckDpi()
        {
            dpi_checked = true;

            string resources = GetStringProperty(root_window, XA_RESOURCE_MANAGER);

            foreach (var line in resources.Split('\n'))
            {
                if (line.StartsWith("Xft.dpi:\t"))
                {
                    if (int.TryParse(line.Substring(9), NumberStyles.Integer, CultureInfo.InvariantCulture, out int dpi_int))
                    {
                        dpi = dpi_int;
                        return;
                    }
                }
            }

            dpi = 0;
        }

        public override float GetDpi(int x, int y)
        {
            if (!dpi_checked)
            {
                CheckDpi();
            }

            if (dpi != 0.0)
                return dpi;

            return 96;
        }

        public override bool CanSendKeys => xtest_supported || base.CanSendKeys;

        public override async Task SendKey(int keysym)
        {
            if (xtest_supported)
            {
                int keycode = XKeysymToKeycode(display, new IntPtr(keysym)).ToInt32();
                if (keycode == 0)
                {
                    Utils.DebugWriteLine($"WARNING: Failed looking up X keycode for keysym {keysym}");
                    return;
                }
                //TODO: check XkbGetSlowKeysDelay
                XTestFakeKeyEvent(display, keycode, True, IntPtr.Zero);
                XTestFakeKeyEvent(display, keycode, False, IntPtr.Zero);
                return;
            }
            await base.SendKey(keysym);
        }

        public override Task SendMouseMotion(int x, int y)
        {
            if (xtest_supported)
            {
                XTestFakeMotionEvent(display, 0, x, y, IntPtr.Zero);
                return Task.CompletedTask;
            }
            return base.SendMouseMotion(x, y);
        }

        public override Task SendMouseButton(MouseButton button, bool is_press)
        {
            if (xtest_supported)
            {
                XTestFakeButtonEvent(display, (int)button, is_press ? 1 : 0, IntPtr.Zero);
                return Task.CompletedTask;
            }
            return base.SendMouseButton(button, is_press);
        }

        public override OverlayBox CreateOverlayBox()
        {
            return new XShapeBox(this);
        }
    }
}
