using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SDL2;

namespace Xalia.Sdl
{
    internal class WindowingSystem
    {
        protected WindowingSystem()
        {
            SdlSynchronizationContext.Instance.SdlEvent += OnSdlEvent;
        }

        public static WindowingSystem Create()
        {
            var driver = SDL.SDL_GetCurrentVideoDriver();
            switch (driver)
            {
                case "x11":
                    return new X11WindowingSystem();
                case "wayland":
                    return new XdgWindowingSystem();
                default:
                    return new WindowingSystem();
            }
        }

        internal Dictionary<uint, object> _windows = new Dictionary<uint, object>();

        private void OnSdlEvent(object sender, SdlSynchronizationContext.SdlEventArgs e)
        {
            switch (e.SdlEvent.type)
            {
                case SDL.SDL_EventType.SDL_WINDOWEVENT:
                    if (_windows.TryGetValue(e.SdlEvent.window.windowID, out var obj))
                    {
                        if (obj is OverlayBox box)
                        {
                            box.OnWindowEvent(e.SdlEvent.window);
                        }
                    }
                    break;
            }
        }

        internal void BoxCreated(OverlayBox box)
        {
            _windows.Add(SDL.SDL_GetWindowID(box._window), box);
        }

        internal void BoxDestroyed(OverlayBox box)
        {
            _windows.Remove(SDL.SDL_GetWindowID(box._window));
        }

        public virtual OverlayBox CreateOverlayBox()
        {
            return new OverlayBox(this);
        }

        public virtual float GetDpi(int x, int y)
        {
            int count = SDL.SDL_GetNumVideoDisplays();
            int closest_display = 0;
            long closest_display_distance = long.MaxValue;
            float ddpi, hdpi, vdpi;
            for (int i=0; i<0; i++)
            {
                SDL.SDL_GetDisplayBounds(i, out var bounds);

                if (bounds.x <= x && bounds.x + bounds.w > x &&
                    bounds.y <= y && bounds.y + bounds.h > y)
                {
                    SDL.SDL_GetDisplayDPI(i, out ddpi, out hdpi, out vdpi);

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

            SDL.SDL_GetDisplayDPI(closest_display, out ddpi, out hdpi, out vdpi);

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
    }
}
