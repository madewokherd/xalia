using System;
using System.Collections.Generic;
using SDL2;

namespace Gazelle.Sdl
{
    internal class OverlayBox : IDisposable
    {
        internal OverlayBox(WindowingSystem windowingSystem)
        {
            SdlSynchronizationContext.Instance.AssertMainThread();

            _window = SDL.SDL_CreateShapedWindow("overlay box", 0, 0, 10, 10,
                SDL.SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP |
                SDL.SDL_WindowFlags.SDL_WINDOW_BORDERLESS |
                SDL.SDL_WindowFlags.SDL_WINDOW_TOOLTIP |
                SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN);

            if (_window == IntPtr.Zero)
                throw new Exception(SDL.SDL_GetError());

            this.windowingSystem = windowingSystem;
            this.windowingSystem.BoxCreated(this);
        }

        private int _x, _y, _width, _height;

        internal void OnWindowEvent(SDL.SDL_WindowEvent window)
        {
            if (window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_EXPOSED)
                Redraw();
        }

        public int X
        {
            get => _x;
            set
            {
                SdlSynchronizationContext.Instance.AssertMainThread();
                if (_x != value)
                {
                    _x = value;
                    UpdatePosition();
                }
            }
        }
        public int Y
        {
            get => _y;
            set
            {
                SdlSynchronizationContext.Instance.AssertMainThread();
                if (_y != value)
                {
                    _y = value;
                    UpdatePosition();
                }
            }
        }
        public int Width
        {
            get => _width;
            set
            {
                if (_width != value)
                {
                    SdlSynchronizationContext.Instance.AssertMainThread();
                    _width = value;
                    UpdateRegion();
                }
            }
        }
        public int Height
        {
            get => _height;
            set
            {
                if (_height != value)
                {
                    SdlSynchronizationContext.Instance.AssertMainThread();
                    _height = value;
                    UpdateRegion();
                }
            }
        }

        private SDL.SDL_Color _color;
        public SDL.SDL_Color Color
        {
            get => _color;
            set
            {
                if (!_color.Equals(value))
                {
                    _color = value;
                    Redraw();
                }
            }
        }

        public void SetColor(byte r, byte g, byte b, byte a)
        {
            var color = new SDL.SDL_Color();
            color.r = r;
            color.g = g;
            color.b = b;
            color.a = a;
            Color = color;
        }

        private int _thickness = 5;
        public int Thickness
        {
            get => _thickness;
            set
            {
                if (_thickness != value)
                {
                    _thickness = value;
                    UpdatePosition();
                    UpdateRegion();
                }
            }
        }

        internal IntPtr _window;
        private readonly WindowingSystem windowingSystem;

        private void UpdatePosition()
        {
            SDL.SDL_SetWindowPosition(_window, _x - _thickness, _y - _thickness);
        }

        private void UpdateRegion()
        {
            var window_width = _width + _thickness * 2;
            var window_height = _height + _thickness * 2;
            SDL.SDL_SetWindowSize(_window, window_width, window_height);

            var surface = SDL.SDL_CreateRGBSurfaceWithFormat(0, window_width, window_height, 16,
                SDL.SDL_PIXELFORMAT_BGRA5551);

            try
            {
                var surface_renderer = SDL.SDL_CreateSoftwareRenderer(surface);

                try
                {
                    SDL.SDL_SetRenderDrawColor(surface_renderer, 0, 0, 0, 0);

                    SDL.SDL_RenderClear(surface_renderer);

                    SDL.SDL_SetRenderDrawColor(surface_renderer, 0, 0, 0, 255);

                    var rect = new SDL.SDL_Rect();
                    rect.x = 0;
                    rect.y = 0;
                    rect.w = window_width;
                    rect.h = window_height;

                    SDL.SDL_RenderFillRect(surface_renderer, ref rect);

                    SDL.SDL_SetRenderDrawColor(surface_renderer, 0, 0, 0, 0);

                    rect.x = _thickness;
                    rect.y = _thickness;
                    rect.w = _width;
                    rect.h = _height;

                    SDL.SDL_RenderFillRect(surface_renderer, ref rect);
                }
                finally
                {
                    SDL.SDL_DestroyRenderer(surface_renderer);
                }

                var mode = new SDL.SDL_WindowShapeMode();
                mode.mode = SDL.WindowShapeMode.ShapeModeDefault;

                SDL.SDL_SetWindowShape(_window, surface, ref mode);
            }
            finally
            {
                SDL.SDL_FreeSurface(surface);
            }
        }


        public void Show()
        {
            SDL.SDL_ShowWindow(_window);
        }

        public void Hide()
        {
            SDL.SDL_HideWindow(_window);
        }

        public void Dispose()
        {
            windowingSystem.BoxDestroyed(this);
            SDL.SDL_DestroyWindow(_window);
            _window = IntPtr.Zero;
        }

        public void Redraw()
        {
            IntPtr renderer = SDL.SDL_CreateRenderer(_window, -1, 0);

            try
            {
                SDL.SDL_SetRenderDrawColor(renderer, _color.r, _color.g, _color.b, _color.a);

                SDL.SDL_RenderClear(renderer);

                SDL.SDL_RenderPresent(renderer);
            }
            finally
            {
                SDL.SDL_DestroyRenderer(renderer);
            }
        }
    }
}
