using System.Numerics;

namespace Xalia.Gudl
{
    internal class IntegerExpression : GudlExpression
    {
        public IntegerExpression(BigInteger value)
        {
            Value = value;
        }

        public IntegerExpression(int value) : this(new BigInteger(value)) { }

        public BigInteger Value { get; }

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
