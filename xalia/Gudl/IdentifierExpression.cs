namespace Xalia.Gudl
{
    public class IdentifierExpression : GudlExpression
    {
        public IdentifierExpression(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is IdentifierExpression id)
                return Name == id.Name;
            return false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ typeof(IdentifierExpression).GetHashCode();
        }

        internal override string ToString(out GudlPrecedence precedence)
        {
            precedence = GudlPrecedence.Atom;
            return Name.ToString();
        }
    }
}
