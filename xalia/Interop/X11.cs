using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using static SDL2.SDL;

namespace Xalia.Interop
{
    internal static class X11
    {
        const string X11_LIB = "X11";
        const string XTEST_LIB = "Xtst";

        public static IntPtr XA_RESOURCE_MANAGER => (IntPtr)23;
        public static IntPtr XA_STRING => (IntPtr)31;


        [StructLayout(LayoutKind.Sequential)]
        public struct XWindowAttributes
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
        public struct XPropertyEvent
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
        public struct SDL_SysWMmsg_X11
        {
            public SDL_version version;
            public SDL_SYSWM_TYPE subsystem;
            public XEvent xev;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XEventPad
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
        public struct XEvent
        {
            [FieldOffset(0)]
            public int type;
            [FieldOffset(0)]
            public XPropertyEvent xproperty;
            [FieldOffset(0)]
            XEventPad pad;
        }

        public const int Success = 0;
        public const int True = 1;
        public const int False = 0;

        public const int PropertyNotify = 28;

        public static IntPtr PropertyChangeMask => (IntPtr)(1 << 22);

        [DllImport(X11_LIB)]
        public extern static IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(X11_LIB)]
        public extern static int XGetWindowAttributes(IntPtr display, IntPtr window, ref XWindowAttributes window_attributes_return);

        [DllImport(X11_LIB)]
        public extern static int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
            IntPtr long_offset, IntPtr long_length, int delete, IntPtr req_type, out IntPtr actual_type_return,
            out int actual_format_return, out UIntPtr nitems_return, out UIntPtr bytes_after_return,
            out IntPtr prop_return);

        [DllImport(X11_LIB)]
        public extern static int XFree(IntPtr data);

        [DllImport(X11_LIB, CharSet = CharSet.Ansi)]
        public extern static IntPtr XInternAtom(IntPtr display, string atom_name, int only_if_exists);

        [DllImport(X11_LIB)]
        public extern static int XSelectInput(IntPtr display, IntPtr window, IntPtr event_mask);

        [DllImport(X11_LIB)]
        public extern static IntPtr XKeysymToKeycode(IntPtr display, IntPtr keysym);

        [DllImport(XTEST_LIB)]
        public extern static int XTestQueryExtension(IntPtr display, ref int event_basep, ref int error_basep, ref int majorp, ref int minorp);

        [DllImport(XTEST_LIB)]
        public extern static int XTestFakeKeyEvent(IntPtr display, int keycode, int is_press, IntPtr delay);
    }
}
