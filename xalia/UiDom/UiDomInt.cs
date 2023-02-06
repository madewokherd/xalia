namespace Xalia.UiDom
{
    internal class UiDomInt : UiDomValue
    {
        public UiDomInt(int value)
        {
            Value = value;
        }

        public int Value { get; }

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
            return Value ^ typeof(UiDomInt).GetHashCode();
        }

        public override bool ToBool()
        {
            return Value != 0;
        }

        public override bool TryToDouble(out double val)
        {
            val = Value;
            return true;
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
                if (Value == d.Value)
                    sign = 0;
                else if (Value < d.Value)
                    sign = -1;
                else
                    sign = 1;
                return true;
            }
            return base.Compare(other, out sign);
        }

        public override bool ValueEquals(UiDomValue other)
        {
            if (Equals(other))
                return true;
            if (other is UiDomDouble d)
                return Value == d.Value;
            return false;
        }
    }
}
