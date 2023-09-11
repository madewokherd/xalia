using System;
using System.Numerics;

namespace Xalia.UiDom
{
    internal class UiDomInt : UiDomValue
    {
        public UiDomInt(BigInteger value)
        {
            Value = value;
        }

        public UiDomInt(int value) : this(new BigInteger(value)) { }

        public BigInteger Value { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomInt i)
                return Value == i.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ typeof(UiDomInt).GetHashCode();
        }

        public override bool ToBool()
        {
            return Value != 0;
        }

        public override bool TryToDouble(out double val)
        {
            try
            {
                val = (double)Value;
                return true;
            }
            catch (OverflowException)
            {
                val = 0.0;
                return false;
            }
        }

        public override bool TryToInt(out int val)
        {
            try
            {
                val = (int)Value;
                return true;
            }
            catch (OverflowException)
            {
                val = 0;
                return false;
            }
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public override bool Compare(UiDomValue other, out int sign)
        {
            if (other is UiDomInt i)
            {
                if (Value == i.Value)
                    sign = 0;
                else if (Value < i.Value)
                    sign = -1;
                else
                    sign = 1;
                return true;
            }
            if (other is UiDomDouble d)
            {
                // mixed comparison is complicated so don't duplicate it here
                var res = d.Compare(this, out sign);
                sign = -sign;
                return res;
            }
            return base.Compare(other, out sign);
        }

        public override bool ValueEquals(UiDomValue other)
        {
            if (Equals(other))
                return true;
            if (other is UiDomDouble d)
                // mixed comparison is complicated so don't duplicate it here
                return d.Equals(this);
            return false;
        }
    }
}
