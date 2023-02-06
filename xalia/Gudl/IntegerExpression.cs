namespace Xalia.Gudl
{
    internal class IntegerExpression : GudlExpression
    {
        public IntegerExpression(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is IntegerExpression i)
                return Value == i.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ typeof(IntegerExpression).GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
