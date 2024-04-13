namespace Xalia.Gudl
{
    public class BinaryExpression : GudlExpression
    {
        public BinaryExpression(GudlExpression left, GudlExpression right, GudlToken op)
        {
            Left = left;
            Right = right;
            Kind = op;
        }

        public GudlExpression Left { get; }
        public GudlExpression Right { get; }
        public GudlToken Kind { get; }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is BinaryExpression bin)
                return Kind == bin.Kind && Left.Equals(bin.Left) && Right.Equals(bin.Right);
            return false;
        }

        public override int GetHashCode()
        {
            return (Left, Right, Kind).GetHashCode() ^ typeof(BinaryExpression).GetHashCode();
        }

        internal override string ToString(out GudlPrecedence precedence)
        {
            string opname;
            switch (Kind)
            {
                case GudlToken.And:
                    opname = " and ";
                    precedence = GudlPrecedence.And;
                    break;
                case GudlToken.Dot:
                    opname = ".";
                    precedence = GudlPrecedence.Dot;
                    break;
                case GudlToken.Equal:
                    opname = " == ";
                    precedence = GudlPrecedence.Inequality;
                    break;
                case GudlToken.NotEqual:
                    opname = " != ";
                    precedence = GudlPrecedence.Inequality;
                    break;
                case GudlToken.Lt:
                    opname = " < ";
                    precedence = GudlPrecedence.Inequality;
                    break;
                case GudlToken.Gt:
                    opname = " > ";
                    precedence = GudlPrecedence.Inequality;
                    break;
                case GudlToken.Lte:
                    opname = " <= ";
                    precedence = GudlPrecedence.Inequality;
                    break;
                case GudlToken.Gte:
                    opname = " >= ";
                    precedence = GudlPrecedence.Inequality;
                    break;
                case GudlToken.Or:
                    opname = " or ";
                    precedence = GudlPrecedence.Or;
                    break;
                case GudlToken.Mult:
                    opname = " * ";
                    precedence = GudlPrecedence.Product;
                    break;
                case GudlToken.IDiv:
                    opname = " ~/ ";
                    precedence = GudlPrecedence.Product;
                    break;
                case GudlToken.Modulo:
                    opname = " ~ ";
                    precedence = GudlPrecedence.Product;
                    break;
                case GudlToken.Div:
                    opname = " / ";
                    precedence = GudlPrecedence.Product;
                    break;
                case GudlToken.Plus:
                    opname = " + ";
                    precedence = GudlPrecedence.Sum;
                    break;
                case GudlToken.Minus:
                    opname = " - ";
                    precedence = GudlPrecedence.Sum;
                    break;
                default:
                    precedence = GudlPrecedence.Atom;
                    return base.ToString();
            }
            var left_str = Left.ToString(out GudlPrecedence left_precedence);
            if (left_precedence < precedence)
                left_str = $"({left_str})";
            var right_str = Right.ToString(out GudlPrecedence right_precedence);
            if (right_precedence <= precedence)
                right_str = $"({right_str})";
            return $"{left_str}{opname}{right_str}";
        }
    }
}
