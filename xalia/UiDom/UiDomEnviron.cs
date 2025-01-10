using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.UiDom
{
    internal class UiDomEnviron : UiDomValue
    {
        private UiDomEnviron()
        {
        }

        public static readonly UiDomEnviron Instance = new UiDomEnviron();

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return typeof(UiDomEnviron).GetHashCode();
        }

        public override string ToString()
        {
            return "environ";
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (Utils.TryGetEnvironmentVariable(id, out string result))
                return new UiDomString(result);
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 1)
                return UiDomUndefined.Instance;
            var expr = arglist[0];
            UiDomValue right = context.Evaluate(expr, root, depends_on);
            if (right is UiDomString st)
            {
                return EvaluateIdentifier(st.Value, root, depends_on);
            }
            return UiDomUndefined.Instance;
        }
    }
}
