using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class Win32ItemRects : UiDomValue
    {
        private RECT[] Rects;

        public Win32ItemRects(RECT[] rects)
        {
            Rects = rects;
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            result.Append("win32_item_rects[");
            bool first = true;
            foreach (var rect in Rects)
            {
                if (!first)
                    result.Append("; ");
                result.Append($"({rect.left},{rect.top}-{rect.right},{rect.bottom})");
                first = true;
            }
            result.Append(']');
            return result.ToString();
        }

        public override int GetHashCode()
        {
            return typeof(Win32ItemRects).GetHashCode() ^
                StructuralComparisons.StructuralEqualityComparer.GetHashCode(Rects); ;
        }

        public override bool Equals(object obj)
        {
            if (obj is Win32ItemRects other)
            {
                if (Rects.Length != other.Rects.Length)
                    return false;
                for (int i = 0; i < Rects.Length; i++)
                {
                    if (!Rects[i].Equals(other.Rects[i]))
                        return false;
                }
                return true;
            }
            return false;
        }

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 1)
                return UiDomUndefined.Instance;

            var arg = context.Evaluate(arglist[0], root, depends_on);

            int index;
            if (arg.TryToInt(out int i))
            {
                index = i;
            }
            else
                return UiDomUndefined.Instance;

            if (0 < index && index < Rects.Length)
            {
                return new Win32Rect(Rects[index]);
            }

            return base.EvaluateApply(context, arglist, root, depends_on);
        }
    }
}