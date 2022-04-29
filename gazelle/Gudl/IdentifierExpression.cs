namespace Gazelle.Gudl
{
    public class IdentifierExpression : GudlExpression
    {
        public IdentifierExpression(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}
