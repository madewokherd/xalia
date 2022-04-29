namespace Gazelle.Gudl
{
    public class StringExpression : GudlExpression
    {
        public StringExpression(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}
