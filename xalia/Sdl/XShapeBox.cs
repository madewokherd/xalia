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

            UpdateWindowRegion();
        }

        private IntPtr ColorFromSdl(SDL_Color color)
        {
            long result = color.a << 24 | color.b << 16 | color.g << 8 | color.r;

            return unchecked((IntPtr)result);
        }

        private void UpdateWindowRegion()
        {
            float dpi_ul = WindowingSystem.GetDpi(X, Y);
            float dpi_br = WindowingSystem.GetDpi(X + Width, Y + Height);
            int pixel_width = (int)Math.Round(Math.Max(dpi_ul, dpi_br) / 96.0);

            int width = Width + EffectiveThickness * 2;
            int height = Height + EffectiveThickness * 2;

            XResizeWindow(Display, window, width, height);

            XGCValues values = default;

            values.function = GXcopy;
            values.fill_style = FillSolid;
            values.foreground = unchecked((IntPtr)0xff000000);

            IntPtr gc = XCreateGC(Display, window, (IntPtr)(GCFunction|GCFillStyle|GCForeground), ref values);

            XFillRectangle(Display, window, gc, 0, 0, width, height);

            values.foreground = ColorFromSdl(Color);

            XChangeGC(Display, gc, (IntPtr)GCForeground, ref values);

            XFillRectangle(Display, window, gc, pixel_width, pixel_width,
                Width + EffectiveThickness * 2 - pixel_width * 2,
                Height + EffectiveThickness * 2 - pixel_width * 2);

            values.foreground = unchecked((IntPtr)0xff000000);

            XChangeGC(Display, gc, (IntPtr)GCForeground, ref values);

            XFillRectangle(Display, window, gc,
                EffectiveThickness - pixel_width, EffectiveThickness - pixel_width,
                Width + pixel_width * 2,
                Height + pixel_width * 2);

            XFreeGC(Display, gc);

            // Make our window transparent to pointer input
            XShapeCombineRectangles(Display, window, ShapeInput, 0, 0, new XRectangle[] { }, 0, ShapeSet, Unsorted);
        }
    }
}
