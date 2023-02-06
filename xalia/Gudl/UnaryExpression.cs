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

        public override string ToString()
        {
            string opname;
            switch (Kind)
            {
                case GudlToken.Not:
                    opname = "not ";
                    break;
                default:
                    return base.ToString();
            }    
            return $"{opname}{Inner}";
        }
    }
}
