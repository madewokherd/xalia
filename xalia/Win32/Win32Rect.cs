using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class Win32Rect : UiDomValue
    {
        private RECT Rect;

        public Win32Rect(RECT rect)
        {
            Rect = rect;
        }

        public override string ToString()
        {
            return $"({Rect.left},{Rect.top}-{Rect.right},{Rect.bottom})";
        }

        public override int GetHashCode()
        {
            return typeof(Win32Rect).GetHashCode() ^ (Rect.left, Rect.top, Rect.right, Rect.bottom).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is Win32Rect other)
            {
                return Rect.Equals(other.Rect);
            }
            return false;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "left":
                case "x":
                    return new UiDomInt(Rect.left);
                case "top":
                case "y":
                    return new UiDomInt(Rect.top);
                case "width":
                    return new UiDomInt(Rect.right - Rect.left);
                case "height":
                    return new UiDomInt(Rect.bottom - Rect.top);
                case "right":
                    return new UiDomInt(Rect.right);
                case "bottom":
                    return new UiDomInt(Rect.bottom);
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}