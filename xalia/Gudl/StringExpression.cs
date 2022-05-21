namespace Xalia.Gudl
{
    public class StringExpression : GudlExpression
    {
        public StringExpression(string value)
        {
            Value = value;
        }

        public string Value { get; }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is StringExpression st)
                return Value == st.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ typeof(StringExpression).GetHashCode();
        }

        public override string ToString()
        {
            // FIXME: add escapes if necessary
            return $"\"{Value}\"";
        }
    }
}
