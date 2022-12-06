using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    internal class UiDomAssign : UiDomValue
    {
        public UiDomAssign(UiDomElement element)
        {
            Element = element;
        }

        public UiDomElement Element { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomAssign assign)
            {
                return assign.Element.Equals(Element);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(UiDomAssign).GetHashCode() ^ Element.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Element}.assign";
        }

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 2)
                return UiDomUndefined.Instance;

            var name = context.Evaluate(arglist[0], root, depends_on);

            if (!(name is UiDomString st))
            {
                return UiDomUndefined.Instance;
            }

            var value = context.Evaluate(arglist[1], root, depends_on);

            return new UiDomAssignRoutine(Element, st.Value, value);
        }
    }
}
