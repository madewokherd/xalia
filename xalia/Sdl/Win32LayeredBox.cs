#if WINDOWS
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static SDL2.SDL;
using static Xalia.Interop.Win32;

namespace Xalia.Sdl
{
    internal class Win32LayeredBox : OverlayBox
    {
        public Win32LayeredBox(Win32WindowingSystem windowingSystem) : base(windowingSystem)
        {
            WindowingSystem = windowingSystem;
            int style = WS_POPUP;
            // WS_EX_TRANSPARENT makes it possible to click theough the window when set with WS_EX_LAYERED
            int exstyle = WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
            GCHandle handle = GCHandle.Alloc(this, GCHandleType.Normal);
            try
            {
                CreateWindowExW(exstyle, WindowClass,
                    "Overlay Box", style, 0, 0, 5, 5, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    GCHandle.ToIntPtr(handle));
                if (_hwnd == IntPtr.Zero)
                    throw new Win32Exception();
            }
            finally
            {
                handle.Free();
            }
            // For some reason, window creation messes with the styles we passed in.
            SetWindowLong(_hwnd, GWL_EXSTYLE, (IntPtr)exstyle);
            SetWindowLong(_hwnd, GWL_STYLE, (IntPtr)style);
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }

        static IntPtr _windowClass;

        static WNDPROC _wndProc;

        static Dictionary<IntPtr, Win32LayeredBox> _hwnds = new Dictionary<IntPtr, Win32LayeredBox>();

        static IntPtr WindowClass
        {
            get
            {
                if (_windowClass == IntPtr.Zero)
                {
                    if (_wndProc == null)
                    {
                        _wndProc = new WNDPROC(StaticWindowProc);
                    }
                    var wndclass = new WNDCLASSEXW();
                    wndclass.cbSize = Marshal.SizeOf<WNDCLASSEXW>();
                    wndclass.lpfnWndProc = _wndProc;
                    wndclass.hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW);
                    wndclass.lpszClassName = "XaliaOverlayBox";
                    _windowClass = RegisterClassExW(ref wndclass);
                }
                return _windowClass;
            }
        }

        public Win32WindowingSystem WindowingSystem { get; }

        private static IntPtr StaticWindowProc(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam)
        {
            try
            {
                if (_hwnds.TryGetValue(hwnd, out var self))
                {
                    return self.WindowProc(msg, wparam, lparam);
                }
                if (msg == WM_NCCREATE)
                {
                    var createStruct = Marshal.PtrToStructure<CREATESTRUCTW>(lparam);
                    var creating = (Win32LayeredBox)GCHandle.FromIntPtr(createStruct.lpCreateParams).Target;
                    creating._hwnd = hwnd;
                    _hwnds.Add(hwnd, creating);
                    return (IntPtr)1;
                }
            }
            catch (Exception e)
            {
                Utils.OnError(e);
            }
            return DefWindowProcW(hwnd, msg, wparam, lparam);
        }

        private IntPtr WindowProc(int msg, IntPtr wparam, IntPtr lparam)
        {
            switch (msg)
            {
                case WM_DESTROY:
                    _windowDestroyed = true;
                    Dispose();
                    return IntPtr.Zero;
                case WM_MOUSEACTIVATE:
                    // For some reason, WS_EX_NOACTIVATE doesn't affect this on Wine.
                    return (IntPtr)MA_NOACTIVATE;
            }
            return DefWindowProcW(_hwnd, msg, wparam, lparam);
        }

        IntPtr _hwnd;
        bool _windowDestroyed;

        protected override void Dispose(bool disposing)
        {
            if (_hwnd != IntPtr.Zero)
            {
                _hwnds.Remove(_hwnd);
                if (!_windowDestroyed)
                    DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }

        protected override void Update(UpdateFlags flags)
        {
            if ((flags & UpdateFlags.Visible) == 0)
            {
                HideHwnd();
            }
            else if ((flags & UpdateFlags.Show) != 0)
            {
                UpdateWindowRegion();
                UpdateWindowPosition();
                ShowHwnd();
            }
            else if ((flags & UpdateFlags.SizeChanged|UpdateFlags.EffectiveThicknessChanged) != 0)
            {
                HideHwnd();
                UpdateWindowRegion();
                UpdateWindowPosition();
                ShowHwnd();
            }
            else
            {
                if ((flags & UpdateFlags.PositionChanged) != 0)
                    UpdateWindowPosition();
                if ((flags & UpdateFlags.ColorChanged) != 0)
                    UpdateWindowRegion();
            }
        }

        private void ShowHwnd()
        {
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_SHOWWINDOW|SWP_NOACTIVATE|SWP_NOMOVE|SWP_NOSIZE|SWP_NOZORDER);
        }

        private void UpdateWindowPosition()
        {
            SetWindowPos(_hwnd, HWND_TOPMOST, X - EffectiveThickness, Y - EffectiveThickness,
                0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private void DrawBox(IntPtr renderer)
        {
            float dpi_ul = WindowingSystem.GetDpi(X, Y);
            float dpi_br = WindowingSystem.GetDpi(X + Width, Y + Height);
            int pixel_width = (int)Math.Round(Math.Max(dpi_ul, dpi_br) / 96.0);

            SDL_SetRenderDrawBlendMode(renderer, SDL_BlendMode.SDL_BLENDMODE_NONE);

            // outer pixel border
            SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);

            SDL_RenderClear(renderer);

            // colored border
            SDL_SetRenderDrawColor(renderer, Color.r, Color.g, Color.b, Color.a);

            SDL_Rect rc;

            rc.x = pixel_width;
            rc.y = pixel_width;
            rc.w = Width + EffectiveThickness * 2 - pixel_width * 2;
            rc.h = Height + EffectiveThickness * 2 - pixel_width * 2;

            SDL_RenderFillRect(renderer, ref rc);

            // inner pixel border
            SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);

            rc.x = EffectiveThickness - pixel_width;
            rc.y = EffectiveThickness - pixel_width;
            rc.w = Width + pixel_width * 2;
            rc.h = Height + pixel_width * 2;

            SDL_RenderFillRect(renderer, ref rc);

            // hole
            SDL_SetRenderDrawColor(renderer, 0, 0, 0, 0);

            rc.x = EffectiveThickness;
            rc.y = EffectiveThickness;
            rc.w = Width;
            rc.h = Height;

            SDL_RenderFillRect(renderer, ref rc);

            SDL_RenderPresent(renderer);
        }

        private void UpdateWindowRegion()
        {
            int width = Width + EffectiveThickness * 2;
            int height = Height + EffectiveThickness * 2;
            var bmih = new BITMAPINFOHEADER();
            bmih.biSize = Marshal.SizeOf<BITMAPINFOHEADER>();
            bmih.biWidth = width;
            bmih.biHeight = height;
            bmih.biPlanes = 1;
            bmih.biBitCount = 32;
            bmih.biCompression = BI_RGB;
            IntPtr dib = CreateDIBSection(IntPtr.Zero, ref bmih, DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero)
                throw new Win32Exception();
            try
            {
                // create a surface to render to the dib
                IntPtr surface = SDL_CreateRGBSurfaceWithFormatFrom(bits, width, height, 32, width * 4, SDL_PIXELFORMAT_ARGB8888);
                if (surface == IntPtr.Zero)
                    throw new Exception(SDL_GetError());
                try
                {
                    IntPtr renderer = SDL_CreateSoftwareRenderer(surface);
                    if (renderer == IntPtr.Zero)
                        throw new Exception(SDL_GetError());

                    DrawBox(renderer);
                }
                finally
                {
                    SDL_FreeSurface(surface);
                }

                //select DIB into an HDC
                IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero)
                    throw new Win32Exception();
                try
                {
                    SelectObject(hdc, dib);

                    SIZE size = new SIZE();
                    size.cx = width;
                    size.cy = height;

                    POINT srcpos = new POINT();
                    srcpos.x = 0;
                    srcpos.y = 0;

                    var blend = new BLENDFUNCTION();
                    blend.BlendOp = AC_SRC_OVER;
                    blend.BlendFlags = 0;
                    blend.SourceConstantAlpha = 255;
                    blend.AlphaFormat = AC_SRC_ALPHA;

                    UpdateLayeredWindow(_hwnd, IntPtr.Zero, IntPtr.Zero, ref size, hdc, ref srcpos,
                        0, ref blend, ULW_ALPHA);
                }
                finally
                {
                    DeleteDC(hdc);
                }
            }
            finally
            {
                DeleteObject(dib);
            }
        }

        private void HideHwnd()
        {
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_HIDEWINDOW|SWP_NOACTIVATE|SWP_NOMOVE|SWP_NOSIZE|SWP_NOZORDER);
        }
    }
}
#endif
