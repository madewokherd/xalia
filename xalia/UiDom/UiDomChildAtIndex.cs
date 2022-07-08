using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    internal class UiDomChildAtIndex : UiDomValue
    {
        public UiDomChildAtIndex(UiDomElement element)
        {
            Element = element;
        }

        public UiDomElement Element { get; }

        public override bool Equals(object obj)
        {
            if (obj is UiDomChildAtIndex ch)
            {
                return ch.Element == Element;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Element.GetHashCode() ^ typeof(UiDomChildAtIndex).GetHashCode();
        }

        public override string ToString()
        {
            return $"{Element}.child_at_index";
        }

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression expr, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            UiDomValue right = context.Evaluate(expr, root, depends_on);
            if (right is UiDomInt i)
            {
                depends_on.Add((Element, new IdentifierExpression("children")));
                if (i.Value >= 0 && i.Value < Element.Children.Count)
                {
                    return Element.Children[i.Value];
                }
            }
            return UiDomUndefined.Instance;
        }
    }
}
