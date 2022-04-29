namespace Gazelle.Gudl
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
    }
}
