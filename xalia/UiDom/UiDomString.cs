using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

using Xalia.Gudl;

namespace Xalia.UiDom
{
    public class UiDomString : UiDomValue
    {
        public UiDomString(string value)
        {
            Value = value;
        }

        public override string ToString()
        {
            // FIXME: Escape string if necessary
            return $"\"{Value}\"";
        }
        public string Value { get; }

        public override bool ToBool()
        {
            return Value != string.Empty;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomString st)
                return Value == st.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ typeof(UiDomString).GetHashCode();
        }

        public override bool Compare(UiDomValue other, out int sign)
        {
            if (other is UiDomString s)
            {
                sign = string.CompareOrdinal(Value, s.Value);
                return true;
            }
            return base.Compare(other, out sign);
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root,
            [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "startswith":
                    return new UiDomMethod(this, "startswith", StartsWithMethod);
                case "endswith":
                    return new UiDomMethod(this, "endswith", EndsWithMethod);
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        private static UiDomValue StartsWithMethod(UiDomMethod method, UiDomValue context, GudlExpression[] arglist,
            UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            var s = method.Value as UiDomString;

            foreach (var arg in arglist)
            {
                var s2 = context.Evaluate(arg, root, depends_on) as UiDomString;

                if (!(s2 is null) && s.Value.StartsWith(s2.Value, false, CultureInfo.InvariantCulture))
                    return UiDomBoolean.True;
            }

            return UiDomBoolean.False;
        }

        private static UiDomValue EndsWithMethod(UiDomMethod method, UiDomValue context, GudlExpression[] arglist,
            UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            var s = method.Value as UiDomString;

            foreach (var arg in arglist)
            {
                var s2 = context.Evaluate(arg, root, depends_on) as UiDomString;

                if (!(s2 is null) && s.Value.EndsWith(s2.Value, false, CultureInfo.InvariantCulture))
                    return UiDomBoolean.True;
            }

            return UiDomBoolean.False;
        }
    }
}
