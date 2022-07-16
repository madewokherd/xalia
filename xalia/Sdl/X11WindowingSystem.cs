using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static SDL2.SDL;

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

        public X11WindowingSystem()
        {
            IntPtr window = SDL_CreateWindow("dummy window", 0, 0, 1, 1, SDL_WindowFlags.SDL_WINDOW_HIDDEN);

            if (window == IntPtr.Zero)
                throw new Exception(SDL_GetError());

            SDL_SysWMinfo info = default;

            SDL_VERSION(out info.version);

            if (SDL_GetWindowWMInfo(window, ref info) != SDL_bool.SDL_TRUE)
                throw new Exception(SDL_GetError());

            SDL_DestroyWindow(window);

            display = info.info.x11.display;

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
            
            ((SdlSynchronizationContext)SynchronizationContext.Current).SdlEvent += OnSdlEvent;

            SDL_EventState(SDL_EventType.SDL_SYSWMEVENT, SDL_ENABLE);
        }

        private void OnSdlEvent(object sender, SdlSynchronizationContext.SdlEventArgs e)
        {
            if (e.SdlEvent.type == SDL_EventType.SDL_SYSWMEVENT)
            {
                var syswm = Marshal.PtrToStructure<SDL_SysWMmsg_X11>(e.SdlEvent.syswm.msg);

                if (syswm.xev.type == PropertyNotify && syswm.xev.xproperty.window == root_window &&
                    syswm.xev.xproperty.atom == XA_RESOURCE_MANAGER)
                {
                    dpi_checked = false;
                }
            }
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
                    if (int.TryParse(line.Substring(9), out int dpi_int))
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

            return base.GetDpi(x, y);
        }

        public override bool CanSendKeys => xtest_supported || base.CanSendKeys;

        public override Task SendKey(string key)
        {
            if (xtest_supported)
            {
                return SendKey(XKeyCodes.GetKeySym(key));
            }
            return base.SendKey(key);
        }

        public override async Task SendKey(int keysym)
        {
            if (xtest_supported)
            {
                int keycode = XKeysymToKeycode(display, new IntPtr(keysym)).ToInt32();
                if (keycode == 0)
                {
                    Console.WriteLine($"WARNING: Failed looking up X keycode for keysym {keysym}");
                    return;
                }
                //TODO: check XkbGetSlowKeysDelay
                XTestFakeKeyEvent(display, keycode, True, IntPtr.Zero);
                XTestFakeKeyEvent(display, keycode, False, IntPtr.Zero);
                return;
            }
            await base.SendKey(keysym);
        }

        public override void CustomizeOverlayWindow(OverlayBox box, IntPtr sdl_window)
        {
            // For some reason, this reduces flickering on X11
            box.HideWhenResizing = true;

            base.CustomizeOverlayWindow(box, sdl_window);
        }
    }
}
