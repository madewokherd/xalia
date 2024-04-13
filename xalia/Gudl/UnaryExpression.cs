namespace Xalia.Gudl
{
    public class UnaryExpression : GudlExpression
    {
        public UnaryExpression(GudlExpression inner, GudlToken op)
        {
            Inner = inner;
            Kind = op;
        }

        public GudlExpression Inner { get; }
        public GudlToken Kind { get; }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UnaryExpression u)
                return Kind == u.Kind && Inner.Equals(u.Inner);
            return false;
        }

        public override int GetHashCode()
        {
            return (Inner, Kind).GetHashCode() ^ typeof(UnaryExpression).GetHashCode();
        }

        internal override string ToString(out GudlPrecedence precedence)
        {
            string opname;
            switch (Kind)
            {
                case GudlToken.Not:
                    opname = "not ";
                    precedence = GudlPrecedence.Not;
                    break;
                case GudlToken.Plus:
                    opname = "+";
                    precedence = GudlPrecedence.Sign;
                    break;
                case GudlToken.Minus:
                    opname = "-";
                    precedence = GudlPrecedence.Sign;
                    break;
                default:
                    precedence = GudlPrecedence.Atom;
                    return base.ToString();
            }
            var inner_str = Inner.ToString(out var inner_precedence);
            if (inner_precedence < precedence)
                return $"{opname}({inner_str})";
            return $"{opname}{inner_str}";
        }
    }
}
