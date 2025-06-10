using System;

using static SDL3.SDL;
using static Xalia.Interop.X11;

namespace Xalia.Sdl
{
    internal class XShapeBox : OverlayBox
    {
        public XShapeBox(X11WindowingSystem windowingSystem) : base(windowingSystem) {
            WindowingSystem = windowingSystem;

            int _event_basep = 0, _error_basep = 0;
            if (XShapeQueryExtension(Display, ref _event_basep, ref _error_basep) == 0)
            {
                throw new PlatformNotSupportedException("X shape extension not supported");
            }
        }

        private X11WindowingSystem WindowingSystem { get; }

        private IntPtr Display => WindowingSystem.display;

        protected override void Dispose(bool disposing)
        {
            throw new NotImplementedException ();
        }

        protected override void Update(UpdateFlags flags)
        {
            throw new NotImplementedException ();
        }
    }
}
