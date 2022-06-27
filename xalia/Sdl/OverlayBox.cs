using System;
using System.Collections.Generic;
using SDL2;

namespace Xalia.Sdl
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

            windowingSystem.CustomizeOverlayWindow(_window);

            this.windowingSystem = windowingSystem;
            this.windowingSystem.BoxCreated(this);
        }

        private int _x, _y, _width, _height;

        internal void OnWindowEvent(SDL.SDL_WindowEvent window)
        {
            if (window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_EXPOSED)
                Redraw();
        }

        public bool Shown { get; private set; }

        public int X
        {
            get => _x;
            set
            {
                SdlSynchronizationContext.Instance.AssertMainThread();
                if (_x != value)
                {
                    _x = value;
                    if (UpdateEffectiveThickness())
                        UpdateRegion();
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
                    if (UpdateEffectiveThickness())
                        UpdateRegion();
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
                    if (UpdateEffectiveThickness())
                        UpdatePosition();
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
                    if (UpdateEffectiveThickness())
                        UpdatePosition();
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

        public void SetBounds(int x, int y, int width, int height)
        {
            SdlSynchronizationContext.Instance.AssertMainThread();
            bool update_position = x != _x || y != _y;
            bool update_region = width != _width || height != _height;
            bool was_shown = Shown;
            _x = x;
            _y = y;
            _width = width;
            _height = height;
            // FIXME: For some reason the window flickers black when the size/region is updated,
            // but it doesn't flicker when initially shown. Ideally we should have two
            // windows that we flip between, to prevent flickering on and off as well.
            // That's likely to be required when we start animating the target box.
            bool updated_thickness = UpdateEffectiveThickness();
            if (was_shown && (update_region || updated_thickness))
                Hide();
            if (update_position || updated_thickness)
                UpdatePosition();
            if (update_region || updated_thickness)
                UpdateRegion();
            if (was_shown && (update_region || updated_thickness))
                Show();
        }

        private int _effective_thickness = 5;

        private bool UpdateEffectiveThickness()
        {
            float dpi_ul = windowingSystem.GetDpi(_x, _y);
            float dpi_br = windowingSystem.GetDpi(_x + _width, _y + _height);
            int new_thickness = (int)Math.Round(Math.Max(dpi_ul, dpi_br) * _thickness / 96.0);

            if (_effective_thickness != new_thickness)
            {
                _effective_thickness = new_thickness;
                return true;
            }
            return false;
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
                    if (UpdateEffectiveThickness())
                    {
                        UpdatePosition();
                        UpdateRegion();
                    }
                }
            }
        }

        internal IntPtr _window;
        private readonly WindowingSystem windowingSystem;

        private void UpdatePosition()
        {
            SdlSynchronizationContext.Instance.AssertMainThread();
            SDL.SDL_SetWindowPosition(_window, _x - _effective_thickness, _y - _effective_thickness);
        }

        private void UpdateRegion()
        {
            SdlSynchronizationContext.Instance.AssertMainThread();
            var window_width = _width + _effective_thickness * 2;
            var window_height = _height + _effective_thickness * 2;
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

                    rect.x = _effective_thickness;
                    rect.y = _effective_thickness;
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
            SdlSynchronizationContext.Instance.AssertMainThread();
            Shown = true;
            SDL.SDL_ShowWindow(_window);
        }

        public void Hide()
        {
            SdlSynchronizationContext.Instance.AssertMainThread();
            Shown = false;
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
