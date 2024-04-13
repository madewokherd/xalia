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

        public override string ToString()
        {
            string opname;
            switch (Kind)
            {
                case GudlToken.And:
                    opname = " and ";
                    break;
                case GudlToken.Dot:
                    opname = ".";
                    break;
                case GudlToken.Equal:
                    opname = " == ";
                    break;
                case GudlToken.NotEqual:
                    opname = " != ";
                    break;
                case GudlToken.Lt:
                    opname = " < ";
                    break;
                case GudlToken.Gt:
                    opname = " > ";
                    break;
                case GudlToken.Lte:
                    opname = " <= ";
                    break;
                case GudlToken.Gte:
                    opname = " >= ";
                    break;
                case GudlToken.Or:
                    opname = " or ";
                    break;
                case GudlToken.Mult:
                    opname = " * ";
                    break;
                case GudlToken.IDiv:
                    opname = " ~/ ";
                    break;
                case GudlToken.Modulo:
                    opname = " ~ ";
                    break;
                case GudlToken.Div:
                    opname = " / ";
                    break;
                case GudlToken.Plus:
                    opname = " + ";
                    break;
                case GudlToken.Minus:
                    opname = " - ";
                    break;
                default:
                    return base.ToString();
            }
            return $"({Left}{opname}{Right})";
        }
    }
}
