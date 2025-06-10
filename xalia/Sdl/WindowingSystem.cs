using System;
using System.Threading.Tasks;

using static SDL3.SDL;

namespace Xalia.Sdl
{
    internal abstract class WindowingSystem
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
#if WINDOWS
                case "windows":
                    return new Win32WindowingSystem();
#endif
                default:
                    throw new PlatformNotSupportedException($"Video driver {driver} not supported");
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

        public abstract OverlayBox CreateOverlayBox();

        public virtual float GetDpi(int x, int y)
        {
            return 96;
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
