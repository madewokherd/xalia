using System;

using static SDL3.SDL;
using static Xalia.Interop.X11;

namespace Xalia.Sdl
{
    internal class XShapeBox : OverlayBox
    {
        public XShapeBox(X11WindowingSystem windowingSystem) : base(windowingSystem) {
            WindowingSystem = windowingSystem;

            int _event_basep = 0, _error_basep = 0;
            if (XShapeQueryExtension(Display, ref _event_basep, ref _error_basep) == 0)
            {
                throw new PlatformNotSupportedException("X shape extension not supported");
            }

            window = XCreateSimpleWindow(Display, XDefaultRootWindow(Display),
                15, 15, 15, 15, // xywh
                0, // boder_width
                IntPtr.Zero, // border
                IntPtr.Zero); // background
            
            XSetWindowAttributes attributes = default;
            attributes.override_redirect = 1;

            XChangeWindowAttributes(Display, window, new IntPtr(CWOverrideRedirect), ref attributes);
        }

        private X11WindowingSystem WindowingSystem { get; }

        private IntPtr Display => WindowingSystem.display;

        private IntPtr window;

        protected override void Dispose(bool disposing)
        {
            if (disposing && window != IntPtr.Zero)
            {
                XDestroyWindow(Display, window);
                window = IntPtr.Zero;
            }
        }

        protected override void Update(UpdateFlags flags)
        {
            if ((flags & UpdateFlags.Visible) == 0)
            {
                XUnmapWindow(Display, window);
            }
            if ((flags & UpdateFlags.Show) != 0)
            {
                XMapWindow(Display, window);
            }
        }
    }
}
