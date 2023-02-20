using System;
using static SDL2.SDL;
#if WINDOWS
using static Xalia.Interop.Win32;
#endif

namespace Xalia.Sdl
{
    internal class SplitOverlayBox : OverlayBox
    {
        private WindowingSystem windowingSystem;

        private struct window_info
        {
            public IntPtr window;
            public uint windowID;
            public IntPtr renderer;
        }

        private window_info[] windows; // left, down, up, right

        public SplitOverlayBox(WindowingSystem windowingSystem) : base(windowingSystem)
        {
            SdlSynchronizationContext.Instance.AssertMainThread();

            this.windowingSystem = windowingSystem;
            SdlSynchronizationContext.Instance.SdlEvent += OnSdlEvent;

            CreateWindows();
        }

        private void CreateWindows()
        {
            windows = new window_info[4];
            for (int i = 0; i < 4; i++)
            {
                windows[i].window = SDL_CreateWindow("Overlay box", 0, 0, 10, 10,
                    SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP | SDL_WindowFlags.SDL_WINDOW_BORDERLESS |
                    SDL_WindowFlags.SDL_WINDOW_TOOLTIP | SDL_WindowFlags.SDL_WINDOW_HIDDEN);

                if (windows[i].window == IntPtr.Zero)
                    throw new Exception(SDL_GetError());

                windows[i].windowID = SDL_GetWindowID(windows[i].window);

                windows[i].renderer = SDL_CreateRenderer(windows[i].window, -1, 0);

                if (windows[i].renderer == IntPtr.Zero)
                    throw new Exception(SDL_GetError());

#if WINDOWS
                if (windowingSystem is Win32WindowingSystem)
                {
                    var win32_window = GetSdlWindowHwnd(windows[i].window);

                    var old_exstyle = GetWindowLong(win32_window, GWL_EXSTYLE);

                    IntPtr new_exstyle = (IntPtr)((int)old_exstyle | WS_EX_NOACTIVATE);

                    SetWindowLong(win32_window, GWL_EXSTYLE, new_exstyle);
                }
#endif
            }
        }

        private void OnSdlEvent(object sender, SdlSynchronizationContext.SdlEventArgs e)
        {
            switch (e.SdlEvent.type)
            {
                case SDL_EventType.SDL_WINDOWEVENT:
                    {
                        var windowEvent = e.SdlEvent.window;
                        if (windowEvent.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_EXPOSED ||
                            windowEvent.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_SHOWN)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                if (windowEvent.windowID == windows[i].windowID)
                                {
                                    Redraw(i);
                                    break;
                                }
                            }
                        }
                    }
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            SdlSynchronizationContext.Instance.SdlEvent -= OnSdlEvent;
            for (int i=0; i<4; i++)
            {
                if (windows[i].renderer != IntPtr.Zero)
                {
                    SDL_DestroyRenderer(windows[i].renderer);
                    windows[i].renderer = IntPtr.Zero;
                }
                if (windows[i].window != IntPtr.Zero)
                {
                    SDL_DestroyWindow(windows[i].window);
                    windows[i].window = IntPtr.Zero;
                }
            }
        }

        protected override void Update(UpdateFlags flags)
        {
            if ((flags & UpdateFlags.Visible) == 0)
            {
                HideWindow();
            }
            else if ((flags & UpdateFlags.Show) != 0)
            {
                UpdateWindowPlacement();
                ShowWindow();
            }
            else
            {
                if ((flags & UpdateFlags.PositionChanged | UpdateFlags.EffectiveThicknessChanged | UpdateFlags.SizeChanged) != 0)
                    UpdateWindowPlacement();
                if ((flags & UpdateFlags.ColorChanged) != 0)
                    Redraw();
            }
        }

        private void Redraw()
        {
            for (int i = 0; i < 4; i++)
                Redraw(i);
        }

        private void Redraw(int index)
        {
            float dpi_ul = windowingSystem.GetDpi(X, Y);
            float dpi_br = windowingSystem.GetDpi(X + Width, Y + Height);
            int pixel_width = (int)Math.Round(Math.Max(dpi_ul, dpi_br) / 96.0);

            var info = windows[index];

            SDL_SetRenderDrawColor(info.renderer, 0, 0, 0, 0);

            SDL_RenderClear(info.renderer);

            SDL_SetRenderDrawColor(info.renderer, Color.r, Color.g, Color.b, Color.a);

            SDL_Rect rc;

            switch (index)
            {
                case 0:
                case 3:
                    // left or right
                    rc.x = pixel_width;
                    rc.y = 0;
                    rc.w = EffectiveThickness - pixel_width * 2;
                    rc.h = Height;
                    SDL_RenderFillRect(info.renderer, ref rc);

                    break;
                case 1:
                    // bottom
                    rc.x = pixel_width;
                    rc.y = pixel_width;
                    rc.w = Width + EffectiveThickness * 2 - pixel_width * 2;
                    rc.h = EffectiveThickness - pixel_width * 2;
                    SDL_RenderFillRect(info.renderer, ref rc);

                    rc.y = 0;
                    rc.w = EffectiveThickness - pixel_width * 2;
                    rc.h = pixel_width;
                    SDL_RenderFillRect(info.renderer, ref rc);

                    rc.x = EffectiveThickness + Width + pixel_width;
                    SDL_RenderFillRect(info.renderer, ref rc);

                    break;
                case 2:
                    // top
                    rc.x = pixel_width;
                    rc.y = pixel_width;
                    rc.w = Width + EffectiveThickness * 2 - pixel_width * 2;
                    rc.h = EffectiveThickness - pixel_width * 2;
                    SDL_RenderFillRect(info.renderer, ref rc);

                    rc.y = EffectiveThickness - pixel_width;
                    rc.w = EffectiveThickness - pixel_width * 2;
                    rc.h = pixel_width;
                    SDL_RenderFillRect(info.renderer, ref rc);

                    rc.x = EffectiveThickness + Width + pixel_width;
                    SDL_RenderFillRect(info.renderer, ref rc);
                    break;
            }

            SDL_RenderPresent(info.renderer);
        }

        private void UpdateWindowPlacement()
        {
            // top
            UpdateWindowPlacement(2, X - EffectiveThickness, Y - EffectiveThickness,
                Width + EffectiveThickness * 2, EffectiveThickness);
            // left
            UpdateWindowPlacement(0, X - EffectiveThickness, Y,
                EffectiveThickness, Height);
            // right
            UpdateWindowPlacement(3, X + Width, Y,
                EffectiveThickness, Height);
            // bottom
            UpdateWindowPlacement(1, X - EffectiveThickness, Y + Height,
                Width + EffectiveThickness * 2, EffectiveThickness);
        }

        private void UpdateWindowPlacement(int index, int x, int y, int width, int height)
        {
            IntPtr window = windows[index].window;

            SDL_GetWindowSize(window, out int old_width, out int old_height);

            bool resize_first = Math.Max(old_width, old_height) > Math.Max(width, height);

            if (resize_first)
                SDL_SetWindowSize(window, width, height);

            SDL_SetWindowPosition(window, x, y);

            if (!resize_first)
                SDL_SetWindowSize(window, width, height);

#if WINDOWS
            if (windowingSystem is Win32WindowingSystem)
            {
                var win32_window = GetSdlWindowHwnd(window);

                SetWindowPos(win32_window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
            }
#endif
        }

        private void HideWindow()
        {
            for (int i = 0; i < 4; i++)
                SDL_HideWindow(windows[i].window);
        }

        private void ShowWindow()
        {
            for (int i = 0; i < 4; i++)
                SDL_ShowWindow(windows[i].window);
        }
    }
}
