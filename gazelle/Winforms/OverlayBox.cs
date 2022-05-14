using System;
using System.Drawing;
using System.Windows.Forms;

namespace Gazelle.Winforms
{
    internal class OverlayBox : IDisposable
    {
        private class OverlayBoxForm : Form
        {
            public OverlayBoxForm()
            {
                StartPosition = FormStartPosition.Manual;
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    var result = base.CreateParams;
                    result.Style |= unchecked((int)0x80000000); // WS_POPUP
                    result.Style &= ~0x00C00000; // WS_CAPTION
                    result.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                    result.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                    return result;
                }
            }
        }

        internal OverlayBox()
        {
        }

        private int _x, _y, _width, _height;
        public int X
        {
            get => _x;
            set
            {
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
                    _height = value;
                    UpdateRegion();
                }
            }
        }

        private Color _color;
        public Color Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    UpdateColor();
                }
            }
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

        private OverlayBoxForm _form;

        private void UpdateColor()
        {
            if (_form is null)
                return;
            _form.BackColor = Color;
            _form.Refresh();
        }

        private void UpdatePosition()
        {
            if (_form is null)
                return;
            _form.Location = new Point(_x - _thickness, _y - _thickness);
        }

        private void UpdateRegion()
        {
            if (_form is null)
                return;
            Size size = new Size(_width + _thickness * 2, _height + _thickness * 2);
            _form.Size = size;
            Region shape = new Region(new Rectangle(new Point(0,0), size));
            shape.Exclude(new Region(new Rectangle(
                new Point(_thickness, _thickness),
                new Size(_width, _height))));
            _form.Region = shape;
        }

        private void Realize()
        {
            if (_form is null)
            {
                _form = new OverlayBoxForm();
                UpdateColor();
                UpdatePosition();
                UpdateRegion();
            }
        }

        public void Show()
        {
            Realize();
            _form.Show();
        }

        public void Hide()
        {
            if (!(_form is null))
                _form.Hide();
        }

        public void Dispose()
        {
            if (!(_form is null))
            {
                _form.Dispose();
                _form = null;
            }
        }
    }
}
