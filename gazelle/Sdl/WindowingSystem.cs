using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SDL2;

namespace Gazelle.Sdl
{
    internal class WindowingSystem
    {
        public WindowingSystem()
        {
            SdlSynchronizationContext.Instance.SdlEvent += OnSdlEvent;
        }

        internal Dictionary<uint, object> _windows = new Dictionary<uint, object>();

        private void OnSdlEvent(object sender, SdlSynchronizationContext.SdlEventArgs e)
        {
            switch (e.SdlEvent.type)
            {
                case SDL2.SDL.SDL_EventType.SDL_WINDOWEVENT:
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
    }
}
