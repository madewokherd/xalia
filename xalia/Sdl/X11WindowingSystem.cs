using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SDL2;

namespace Xalia.Sdl
{
    internal class X11WindowingSystem : XdgWindowingSystem
    {
        internal IntPtr display;
        private IntPtr root_window;

        private float dpi;
        private bool dpi_checked;

        private bool xtest_supported;

        const string X11_LIB = "X11";
        const string XTEST_LIB = "Xtst";

        private static IntPtr XA_RESOURCE_MANAGER => (IntPtr)23;
        private static IntPtr XA_STRING => (IntPtr)31;

        public X11WindowingSystem()
        {
            IntPtr window = SDL.SDL_CreateWindow("dummy window", 0, 0, 1, 1, SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN);

            if (window == IntPtr.Zero)
                throw new Exception(SDL.SDL_GetError());

            SDL.SDL_SysWMinfo info = default;

            SDL.SDL_VERSION(out info.version);

            if (SDL.SDL_GetWindowWMInfo(window, ref info) != SDL.SDL_bool.SDL_TRUE)
                throw new Exception(SDL.SDL_GetError());

            SDL.SDL_DestroyWindow(window);

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

            Console.WriteLine($"Xtest: {xtest_supported}");

            EnableInputMask(root_window, PropertyChangeMask);
            
            ((SdlSynchronizationContext)SynchronizationContext.Current).SdlEvent += OnSdlEvent;

            SDL.SDL_EventState(SDL.SDL_EventType.SDL_SYSWMEVENT, SDL.SDL_ENABLE);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XWindowAttributes
        {
            public int x;
            public int y;
            public int width;
            public int height;
            public int border_width;
            public int depth;
            public IntPtr visual;
            public IntPtr root;
            public int _class;
            public int bit_gravity;
            public int win_gravity;
            public int backing_store;
            public UIntPtr backing_planes;
            public UIntPtr backing_pixel;
            public int save_under;
            public IntPtr colormap;
            public int map_installed;
            public int map_state;
            public IntPtr all_event_masks;
            public IntPtr your_event_mask;
            public IntPtr do_not_propogate_mask;
            public int override_redirect;
            public IntPtr screen;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XPropertyEvent
        {
            public int type;
            public UIntPtr serial;
            public int send_event; // Bool
            public IntPtr display; // Display*
            public IntPtr window; // Window
            public IntPtr atom; // Atom
            public IntPtr time; // Time
            public int state;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SDL_SysWMmsg_X11
        {
            public SDL.SDL_version version;
            public SDL.SDL_SYSWM_TYPE subsystem;
            public XEvent xev;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XEventPad
        {
            IntPtr pad0;
            IntPtr pad1;
            IntPtr pad2;
            IntPtr pad3;
            IntPtr pad4;
            IntPtr pad5;
            IntPtr pad6;
            IntPtr pad7;
            IntPtr pad8;
            IntPtr pad9;
            IntPtr pad10;
            IntPtr pad11;
            IntPtr pad12;
            IntPtr pad13;
            IntPtr pad14;
            IntPtr pad15;
            IntPtr pad16;
            IntPtr pad17;
            IntPtr pad18;
            IntPtr pad19;
            IntPtr pad20;
            IntPtr pad21;
            IntPtr pad22;
            IntPtr pad23;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct XEvent
        {
            [FieldOffset(0)]
            public int type;
            [FieldOffset(0)]
            public XPropertyEvent xproperty;
            [FieldOffset(0)]
            XEventPad pad;
        }

        const int Success = 0;
        const int True = 1;
        const int False = 0;

        const int PropertyNotify = 28;

        static IntPtr PropertyChangeMask => (IntPtr)(1 << 22);

        [DllImport(X11_LIB)]
        private extern static IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(X11_LIB)]
        private extern static int XGetWindowAttributes(IntPtr display, IntPtr window, ref XWindowAttributes window_attributes_return);

        [DllImport(X11_LIB)]
        private extern static int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
            IntPtr long_offset, IntPtr long_length, int delete, IntPtr req_type, out IntPtr actual_type_return,
            out int actual_format_return, out UIntPtr nitems_return, out UIntPtr bytes_after_return,
            out IntPtr prop_return);

        [DllImport(X11_LIB)]
        private extern static int XFree(IntPtr data);

        [DllImport(X11_LIB, CharSet = CharSet.Ansi)]
        private extern static IntPtr XInternAtom(IntPtr display, string atom_name, int only_if_exists);

        [DllImport(X11_LIB)]
        private extern static int XSelectInput(IntPtr display, IntPtr window, IntPtr event_mask);

        [DllImport(X11_LIB)]
        private extern static IntPtr XKeysymToKeycode(IntPtr display, IntPtr keysym);

        [DllImport(XTEST_LIB)]
        private extern static int XTestQueryExtension(IntPtr display, ref int event_basep, ref int error_basep, ref int majorp, ref int minorp);

        [DllImport(XTEST_LIB)]
        private extern static int XTestFakeKeyEvent(IntPtr display, int keycode, int is_press, IntPtr delay);

        private void OnSdlEvent(object sender, SdlSynchronizationContext.SdlEventArgs e)
        {
            if (e.SdlEvent.type == SDL.SDL_EventType.SDL_SYSWMEVENT)
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

        private void CheckDpi()
        {
            dpi_checked = true;
            int length = 1024;

            string resources;

            while (true)
            {
                var result = XGetWindowProperty(display, root_window, XA_RESOURCE_MANAGER, IntPtr.Zero, (IntPtr)length,
                    False, XA_STRING, out var actual_type, out var actual_format, out var nitems, out var bytes_after,
                    out var prop);
                if (result != Success || actual_type != XA_STRING || actual_format != 8)
                {
                    dpi = 0;
                    return;
                }

                if (bytes_after != UIntPtr.Zero)
                {
                    length += ((int)bytes_after + 3)/4;
                    continue;
                }

                resources = Marshal.PtrToStringAnsi(prop);
                XFree(prop);
                break;
            }

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
    }
}
