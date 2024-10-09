using System;
using System.Globalization;
using System.Numerics;

namespace Xalia.UiDom
{
    internal class UiDomDouble : UiDomValue
    {
        public UiDomDouble(double value)
        {
            Value = value;
        }

        public double Value { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomDouble d)
                return Value == d.Value;
            return base.Equals(obj);
        }

        public override bool Compare(UiDomValue other, out int sign)
        {
            if (other is UiDomDouble d)
            {
                if (Value == d.Value)
                    sign = 0;
                else if (Value < d.Value)
                    sign = -1;
                else
                    sign = 1;
                return true;
            }
            if (other is UiDomInt i)
            {
                var tr = Math.Floor(Value);
                BigInteger bi;
                try
                {
                    bi = new BigInteger(tr);
                }
                catch (OverflowException)
                {
                    if (double.IsPositiveInfinity(tr))
                    {
                        sign = 1;
                        return true;
                    }
                    if (double.IsNegativeInfinity(tr))
                    {
                        sign = -1;
                        return true;
                    }
                    // else NaN
                    sign = 0;
                    return false;
                }

                if (bi == i.Value)
                {
                    // Value floors to i.Value, therefore Value >= i.Value
                    if (Value != tr)
                        sign = 1;
                    else
                        sign = 0;
                }
                else if (bi < i.Value)
                    sign = -1;
                else
                    sign = 1;
                return true;
            }
            return base.Compare(other, out sign);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ typeof(UiDomDouble).GetHashCode();
        }

        public override bool ToBool()
        {
            return Value != 0.0;
        }

        public override bool TryToInt(out int val)
        {
            try
            {
                val = (int)Math.Round(Value);
                return true;
            }
            catch (OverflowException)
            {
                val = 0;
                return false;
            }
        }

        public override bool TryToDouble(out double val)
        {
            val = Value;
            return true;
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }

        public override bool ValueEquals(UiDomValue other)
        {
            if (Equals(other))
                return true;
            if (other is UiDomInt i)
            {
                var tr = Math.Floor(Value);
                if (tr != Value)
                    return false;
                BigInteger bi;
                try
                {
                    bi = new BigInteger(tr);
                }
                catch (OverflowException)
                {
                    // Infinity or NaN
                    return false;
                }

                return bi == i.Value;
            }
            return false;
        }
    }
}
