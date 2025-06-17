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

            if ((flags & UpdateFlags.PositionChanged) != 0)
                UpdateWindowPosition();

            if ((flags & (UpdateFlags.SizeChanged|UpdateFlags.ThicknessChanged|UpdateFlags.EffectiveThicknessChanged)) != 0)
                UpdateWindowRegion();

            if ((flags & (UpdateFlags.SizeChanged|UpdateFlags.ThicknessChanged|UpdateFlags.EffectiveThicknessChanged|UpdateFlags.ColorChanged)) != 0)
                Redraw();

            if ((flags & UpdateFlags.Show) != 0)
            {
                XMapWindow(Display, window);
            }
        }

        private void UpdateWindowPosition()
        {
            XMoveWindow(Display, window, X - EffectiveThickness, Y - EffectiveThickness);
        }

        private IntPtr ColorFromSdl(SDL_Color color)
        {
            long result = color.a << 24 | color.b << 16 | color.g << 8 | color.r;

            return unchecked((IntPtr)result);
        }

        private void Redraw()
        {
            float dpi_ul = WindowingSystem.GetDpi(X, Y);
            float dpi_br = WindowingSystem.GetDpi(X + Width, Y + Height);
            int pixel_width = (int)Math.Round(Math.Max(dpi_ul, dpi_br) / 96.0);

            int width = Width + EffectiveThickness * 2;
            int height = Height + EffectiveThickness * 2;

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
        }

        private void UpdateWindowRegion()
        {
            float dpi_ul = WindowingSystem.GetDpi(X, Y);
            float dpi_br = WindowingSystem.GetDpi(X + Width, Y + Height);
            int pixel_width = (int)Math.Round(Math.Max(dpi_ul, dpi_br) / 96.0);

            int width = Width + EffectiveThickness * 2;
            int height = Height + EffectiveThickness * 2;

            XResizeWindow(Display, window, width, height);

            // Make our window transparent to pointer input
            XShapeCombineRectangles(Display, window, ShapeInput, 0, 0, new XRectangle[] { }, 0, ShapeSet, Unsorted);

            XRectangle[] bounding_shape = new XRectangle[4];

            // top
            bounding_shape[0].width = (short)width;
            bounding_shape[0].height = (short)EffectiveThickness;

            // left
            bounding_shape[1].width = (short)EffectiveThickness;
            bounding_shape[1].height = (short)height;

            // right
            bounding_shape[2].x = (short)(width - EffectiveThickness);
            bounding_shape[2].width = (short)EffectiveThickness;
            bounding_shape[2].height = (short)height;

            // bottom
            bounding_shape[3].y = (short)(height - EffectiveThickness);
            bounding_shape[3].width = (short)width;
            bounding_shape[3].height = (short)EffectiveThickness;

            XShapeCombineRectangles(Display, window, ShapeBounding, 0, 0, bounding_shape, 4, ShapeSet, Unsorted);
        }
    }
}
