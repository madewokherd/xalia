using System;

using static SDL3.SDL;

namespace Xalia.Sdl
{
    internal abstract class OverlayBox : IDisposable
    {
        public OverlayBox(WindowingSystem windowingSystem)
        {
            WindowingSystem = windowingSystem;
        }

        private WindowingSystem WindowingSystem { get; }

        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        [Flags]
        public enum UpdateFlags
        {
            Visible = 0x1,
            Show = 0x2,
            SizeChanged = 0x4,
            PositionChanged = 0x8,
            ThicknessChanged = 0x10,
            EffectiveThicknessChanged = 0x20,
            ColorChanged = 0x40,
        }

        protected abstract void Update(UpdateFlags flags);

        private void NotifyUpdate(UpdateFlags flags)
        {
            int new_effective_thickness = (int)Math.Round(_thickness * WindowingSystem.GetDpi(_x, _y) / 96);
            if (new_effective_thickness != EffectiveThickness)
            {
                EffectiveThickness = new_effective_thickness;
                flags |= UpdateFlags.EffectiveThicknessChanged;
            }
            if (Visible)
                flags |= UpdateFlags.Visible;
            Update(flags);
        }

        private bool _visible;

        public virtual bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    NotifyUpdate(_visible ? UpdateFlags.Show : default);
                }
            }
        }

        public void Show()
        {
            Visible = true;
        }

        public void Hide()
        {
            Visible = false;
        }

        private int _x, _y, _width, _height;

        public int X
        {
            get => _x;
            set
            {
                SdlSynchronizationContext.Instance.AssertMainThread();
                if (_x != value)
                {
                    _x = value;
                    NotifyUpdate(UpdateFlags.PositionChanged);
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
                    NotifyUpdate(UpdateFlags.PositionChanged);
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
                    NotifyUpdate(UpdateFlags.SizeChanged);
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
                    NotifyUpdate(UpdateFlags.SizeChanged);
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
                    NotifyUpdate(UpdateFlags.ColorChanged);
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

            UpdateFlags flags = default;
            if (x != _x || y != _y)
                flags |= UpdateFlags.PositionChanged;
            if (width != _width || height != _height)
                flags |= UpdateFlags.SizeChanged;

            _x = x;
            _y = y;
            _width = width;
            _height = height;

            if (flags != 0)
                NotifyUpdate(flags);
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
                    NotifyUpdate(UpdateFlags.ThicknessChanged);
                }
            }
        }

        public int EffectiveThickness { get; private set; } = 5;
    }
}
