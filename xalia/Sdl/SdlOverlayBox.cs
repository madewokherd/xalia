using System;

using static SDL3.SDL;

#if WINDOWS
using static Xalia.Interop.Win32;
#endif

namespace Xalia.Sdl
{
    internal class SdlOverlayBox : OverlayBox
    {
        internal SdlOverlayBox(WindowingSystem windowingSystem) : base(windowingSystem)
        {
            SdlSynchronizationContext.Instance.AssertMainThread();

            this.windowingSystem = windowingSystem;
            SdlSynchronizationContext.Instance.SdlEvent += OnSdlEvent;

            CreateWindow();
        }

        internal IntPtr _parentWindow;
        internal IntPtr _window;
        internal uint _windowID;
        internal IntPtr _renderer;
        private readonly WindowingSystem windowingSystem;
        internal int _effectiveThickness;

        private void OnSdlEvent(object sender, SdlSynchronizationContext.SdlEventArgs e)
        {
            switch ((SDL_EventType)e.SdlEvent.type)
            {
                case SDL_EventType.SDL_EVENT_WINDOW_EXPOSED:
                case SDL_EventType.SDL_EVENT_WINDOW_SHOWN:
                    {
                        var windowEvent = e.SdlEvent.window;
                        if (windowEvent.windowID == _windowID)
                        {
                            Redraw();
                        }
                    }
                    break;
            }
        }

        private void CreateWindow()
        {
            _parentWindow = SDL_CreateWindow("parent", 1, 1, SDL_WindowFlags.SDL_WINDOW_HIDDEN);

            _window = SDL_CreatePopupWindow(_parentWindow,
                0,
                0,
                10,
                10,
                SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP |
                SDL_WindowFlags.SDL_WINDOW_BORDERLESS |
                SDL_WindowFlags.SDL_WINDOW_NOT_FOCUSABLE |
                SDL_WindowFlags.SDL_WINDOW_TOOLTIP |
                SDL_WindowFlags.SDL_WINDOW_HIDDEN);

            if (_window == IntPtr.Zero)
                throw new Exception(SDL_GetError());

            _windowID = SDL_GetWindowID(_window);

            _effectiveThickness = (int)Math.Round(Thickness * SDL_GetWindowDisplayScale(_window));

            _renderer = SDL_CreateRenderer(_window, null);

            if (_renderer == IntPtr.Zero)
                throw new Exception(SDL_GetError());

#if WINDOWS
            if (windowingSystem is Win32WindowingSystem)
            {
                var win32_window = GetSdlWindowHwnd(_window);

                var old_exstyle = GetWindowLong(win32_window, GWL_EXSTYLE);

                IntPtr new_exstyle = (IntPtr)((int)old_exstyle | WS_EX_NOACTIVATE);

                SetWindowLong(win32_window, GWL_EXSTYLE, new_exstyle);
            }
#endif
        }

        private void UpdateWindowRegion()
        {
            SdlSynchronizationContext.Instance.AssertMainThread();
            var window_width = Width + EffectiveThickness * 2;
            var window_height = Height + EffectiveThickness * 2;

            SDL_SetWindowSize(_window, window_width, window_height);

            var surface = SDL_CreateSurface(window_width, window_height,
                SDL_PixelFormat.SDL_PIXELFORMAT_BGRA5551);

            try
            {
                var surface_renderer = SDL_CreateSoftwareRenderer(surface);

                try
                {
                    SDL_SetRenderDrawColor(surface_renderer, 0, 0, 0, 255);

                    SDL_RenderClear(surface_renderer);

                    var rect = new SDL_FRect();

                    SDL_SetRenderDrawColor(surface_renderer, 0, 0, 0, 0);

#if WINDOWS
                    if (windowingSystem is Win32WindowingSystem)
                    {
                        rect.x = EffectiveThickness - 1;
                        rect.y = EffectiveThickness - 1;
                        rect.w = Width + 1;
                        rect.h = Height + 1;
                    }
                    else
                    {
#endif
                        rect.x = EffectiveThickness;
                        rect.y = EffectiveThickness;
                        rect.w = Width;
                        rect.h = Height;
#if WINDOWS
                }
#endif

                    SDL_RenderFillRect(surface_renderer, ref rect);
                }
                finally
                {
                    SDL_DestroyRenderer(surface_renderer);
                }

                SDL_SetWindowShape(_window, surface);
            }
            finally
            {
                SDL_DestroySurface(surface);
            }
        }

        private void ShowWindow()
        {
            SDL_ShowWindow(_window);
        }

        private void HideWindow()
        {
            SDL_HideWindow(_window);
        }

        private void DestroyWindow()
        {
            if (_window != IntPtr.Zero)
            {
                SDL_DestroyWindow(_window);
                _window = IntPtr.Zero;
                _windowID = 0;
            }
            if (_parentWindow != IntPtr.Zero)
            {
                SDL_DestroyWindow(_parentWindow);
                _parentWindow = IntPtr.Zero;
            }
            if (_renderer != IntPtr.Zero)
            {
                SDL_DestroyRenderer(_renderer);
            }
        }

        private void UpdateWindowPosition()
        {
            SDL_SetWindowPosition(_window, X - EffectiveThickness, Y - EffectiveThickness);

#if WINDOWS
            if (windowingSystem is Win32WindowingSystem)
            {
                var win32_window = GetSdlWindowHwnd(_window);

                SetWindowPos(win32_window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
            }
#endif
        }

        protected override void Update(UpdateFlags flags)
        {
            if ((flags & UpdateFlags.Visible) == 0)
            {
                HideWindow();
            }
            else if ((flags & UpdateFlags.Show) != 0)
            {
                UpdateWindowRegion();
                UpdateWindowPosition();
                ShowWindow();
            }
            else if ((flags & UpdateFlags.SizeChanged|UpdateFlags.EffectiveThicknessChanged) != 0)
            {
                HideWindow();
                UpdateWindowRegion();
                UpdateWindowPosition();
                ShowWindow();
            }
            else
            {
                if ((flags & UpdateFlags.PositionChanged) != 0)
                    UpdateWindowPosition();
                if ((flags & UpdateFlags.ColorChanged) != 0)
                    Redraw();
            }
        }

        public void Redraw()
        {
            float dpi_ul = windowingSystem.GetDpi(X, Y);
            float dpi_br = windowingSystem.GetDpi(X + Width, Y + Height);
            int pixel_width = (int)Math.Round(Math.Max(dpi_ul, dpi_br) / 96.0);

            SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 0);

            SDL_RenderClear(_renderer);

            SDL_SetRenderDrawColor(_renderer, Color.r, Color.g, Color.b, Color.a);

            SDL_FRect rc;

            rc.x = pixel_width;
            rc.y = pixel_width;
            rc.w = Width + EffectiveThickness * 2 - pixel_width * 2;
            rc.h = Height + EffectiveThickness * 2 - pixel_width * 2;

            SDL_RenderFillRect(_renderer, ref rc);

            SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 0);

            rc.x = EffectiveThickness - pixel_width;
            rc.y = EffectiveThickness - pixel_width;
            rc.w = Width + pixel_width * 2;
            rc.h = Height + pixel_width * 2;

            SDL_RenderFillRect(_renderer, ref rc);

            SDL_RenderPresent(_renderer);
        }

        protected override void Dispose(bool disposing)
        {
            DestroyWindow();
            SdlSynchronizationContext.Instance.SdlEvent -= OnSdlEvent;
        }
    }
}
