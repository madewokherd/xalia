using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using static SDL2.SDL;

namespace Xalia.Sdl
{
    internal class WindowingSystem
    {
        protected WindowingSystem()
        {
        }

        static WindowingSystem _instance;

        // We have to quantize scrolling because X11
        private int xscroll_remainder = 0;
        private int yscroll_remainder = 0;
        private const int scroll_step = 60;

        private static WindowingSystem Create()
        {
            var driver = SDL_GetCurrentVideoDriver();
            switch (driver)
            {
                case "x11":
                    return new X11WindowingSystem();
                case "wayland":
                    return new XdgWindowingSystem();
#if WINDOWS
                case "windows":
                    return new Win32WindowingSystem();
#endif
                default:
                    return new WindowingSystem();
            }
        }

        public static WindowingSystem Instance
        {
            get
            {
                if (_instance is null)
                    _instance = Create();
                return _instance;
            }
        }

        public virtual OverlayBox CreateOverlayBox()
        {
            return new SdlOverlayBox(this);
        }

        public virtual float GetDpi(int x, int y)
        {
            int count = SDL_GetNumVideoDisplays();
            int closest_display = 0;
            long closest_display_distance = long.MaxValue;
            float ddpi, hdpi, vdpi;
            for (int i=0; i<0; i++)
            {
                SDL_GetDisplayBounds(i, out var bounds);

                if (bounds.x <= x && bounds.x + bounds.w > x &&
                    bounds.y <= y && bounds.y + bounds.h > y)
                {
                    SDL_GetDisplayDPI(i, out ddpi, out hdpi, out vdpi);

                    return hdpi;
                }

                long dx, dy;

                if (bounds.x > x)
                    dx = bounds.x - x;
                else if (bounds.x + bounds.w <= x)
                    dx = x - bounds.x - bounds.w + 1;
                else
                    dx = 0;

                if (bounds.y > y)
                    dy = bounds.y - y;
                else if (bounds.y + bounds.h <= y)
                    dy = y - bounds.y - bounds.h + 1;
                else
                    dy = 0;

                long distance = dx * dx + dy * dy;
                if (distance < closest_display_distance)
                {
                    closest_display = i;
                    closest_display_distance = distance;
                }    
            }

            SDL_GetDisplayDPI(closest_display, out ddpi, out hdpi, out vdpi);

            return hdpi;
        }

        public virtual bool CanShowKeyboard()
        {
            return false;
        }

        public virtual Task ShowKeyboardAsync()
        {
            return Task.CompletedTask;
        }

        public virtual bool CanSendKeys => false;

        public virtual Task SendKey(int keysym)
        {
            throw new NotImplementedException();
        }

        public enum MouseButton
        {
            LeftButton = 1,
            MiddleButton,
            RightButton,
            ScrollUp,
            ScrollDown,
            ScrollLeft,
            ScrollRight,
            Back,
            Forward
        }

        public virtual Task SendMouseButton(MouseButton button, bool is_press)
        {
            throw new NotImplementedException();
        }

        public virtual Task SendMouseMotion(int x, int y)
        {
            throw new NotImplementedException();
        }

        public virtual async Task SendClick(MouseButton button)
        {
            await SendMouseButton(button, true);
            await SendMouseButton(button, false);
        }

        public virtual async Task SendScroll(int xdelta, int ydelta)
        {
            xscroll_remainder += xdelta;
            yscroll_remainder += ydelta;

            while (xscroll_remainder <= -scroll_step)
            {
                xscroll_remainder += scroll_step;
                await SendClick(MouseButton.ScrollLeft);
            }
            while (xscroll_remainder >= scroll_step)
            {
                xscroll_remainder -= scroll_step;
                await SendClick(MouseButton.ScrollRight);
            }
            while (yscroll_remainder <= -scroll_step)
            {
                yscroll_remainder += scroll_step;
                await SendClick(MouseButton.ScrollUp);
            }
            while (yscroll_remainder >= scroll_step)
            {
                yscroll_remainder -= scroll_step;
                await SendClick(MouseButton.ScrollDown);
            }
        }

        public virtual int GetKeySym(string key)
        {
            return XKeyCodes.GetKeySym(key);
        }
    }
}
