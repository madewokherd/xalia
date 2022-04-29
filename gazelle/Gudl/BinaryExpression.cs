namespace Gazelle.Gudl
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
    }
}
