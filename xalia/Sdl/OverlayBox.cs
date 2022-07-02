using System;
using System.Collections.Generic;

using static SDL2.SDL;

namespace Xalia.Sdl
{
    internal class OverlayBox : IDisposable
    {
        internal OverlayBox(WindowingSystem windowingSystem)
        {
            SdlSynchronizationContext.Instance.AssertMainThread();

            _window = SDL_CreateShapedWindow("overlay box", 0, 0, 10, 10,
                SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP |
                SDL_WindowFlags.SDL_WINDOW_BORDERLESS |
                SDL_WindowFlags.SDL_WINDOW_TOOLTIP |
                SDL_WindowFlags.SDL_WINDOW_HIDDEN);

            if (_window == IntPtr.Zero)
                throw new Exception(SDL_GetError());

            windowingSystem.CustomizeOverlayWindow(this, _window);

            _renderer = SDL_CreateRenderer(_window, -1, 0);

            this.windowingSystem = windowingSystem;
            this.windowingSystem.BoxCreated(this);
        }

        private int _x, _y, _width, _height;

        internal bool HideWhenResizing { get; set; }

        internal void OnWindowEvent(SDL_WindowEvent window)
        {
            if (window.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_EXPOSED)
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

        private SDL_Color _color;
        public SDL_Color Color
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
            var color = new SDL_Color();
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
            if (HideWhenResizing && was_shown && (update_region || updated_thickness))
                Hide();
            if (update_position || updated_thickness)
                UpdatePosition();
            if (update_region || updated_thickness)
                UpdateRegion();
            if (HideWhenResizing && was_shown && (update_region || updated_thickness))
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
        internal IntPtr _renderer;
        private readonly WindowingSystem windowingSystem;

        private void UpdatePosition()
        {
            SdlSynchronizationContext.Instance.AssertMainThread();
            SDL_SetWindowPosition(_window, _x - _effective_thickness, _y - _effective_thickness);
        }

        private void UpdateRegion()
        {
            SdlSynchronizationContext.Instance.AssertMainThread();
            var window_width = _width + _effective_thickness * 2;
            var window_height = _height + _effective_thickness * 2;
            SDL_SetWindowSize(_window, window_width, window_height);

            var surface = SDL_CreateRGBSurfaceWithFormat(0, window_width, window_height, 16,
                SDL_PIXELFORMAT_BGRA5551);

            try
            {
                var surface_renderer = SDL_CreateSoftwareRenderer(surface);

                try
                {
                    SDL_SetRenderDrawColor(surface_renderer, 0, 0, 0, 0);

                    SDL_RenderClear(surface_renderer);

                    SDL_SetRenderDrawColor(surface_renderer, 0, 0, 0, 255);

                    var rect = new SDL_Rect();
                    rect.x = 0;
                    rect.y = 0;
                    rect.w = window_width;
                    rect.h = window_height;

                    SDL_RenderFillRect(surface_renderer, ref rect);

                    SDL_SetRenderDrawColor(surface_renderer, 0, 0, 0, 0);

                    // FIXME: Why is the 1 pixel adjustment needed?
                    rect.x = _effective_thickness - 1;
                    rect.y = _effective_thickness - 1;
                    rect.w = _width + 1;
                    rect.h = _height + 1;

                    SDL_RenderFillRect(surface_renderer, ref rect);
                }
                finally
                {
                    SDL_DestroyRenderer(surface_renderer);
                }

                var mode = new SDL_WindowShapeMode();
                mode.mode = WindowShapeMode.ShapeModeDefault;

                SDL_SetWindowShape(_window, surface, ref mode);
            }
            finally
            {
                SDL_FreeSurface(surface);
            }
        }


        public void Show()
        {
            SdlSynchronizationContext.Instance.AssertMainThread();
            Shown = true;
            SDL_ShowWindow(_window);
        }

        public void Hide()
        {
            SdlSynchronizationContext.Instance.AssertMainThread();
            Shown = false;
            SDL_HideWindow(_window);
        }

        public void Dispose()
        {
            windowingSystem.BoxDestroyed(this);
            SDL_DestroyWindow(_window);
            _window = IntPtr.Zero;
            SDL_DestroyRenderer(_renderer);
            _renderer = IntPtr.Zero;
        }

        public void Redraw()
        {
            float dpi_ul = windowingSystem.GetDpi(_x, _y);
            float dpi_br = windowingSystem.GetDpi(_x + _width, _y + _height);
            int pixel_width = (int)Math.Round(Math.Max(dpi_ul, dpi_br) / 96.0);

            SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 0);

            SDL_RenderClear(_renderer);

            SDL_SetRenderDrawColor(_renderer, _color.r, _color.g, _color.b, _color.a);

            SDL_Rect rc;

            rc.x = pixel_width;
            rc.y = pixel_width;
            rc.w = Width + _effective_thickness * 2 - pixel_width * 2;
            rc.h = Height + _effective_thickness * 2 - pixel_width * 2;

            SDL_RenderFillRect(_renderer, ref rc);

            SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 0);

            rc.x = _effective_thickness - pixel_width;
            rc.y = _effective_thickness - pixel_width;
            rc.w = Width + pixel_width * 2;
            rc.h = Height + pixel_width * 2;

            SDL_RenderFillRect(_renderer, ref rc);

            SDL_RenderPresent(_renderer);
        }
    }
}
