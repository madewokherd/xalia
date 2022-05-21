using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            return base.Compare(other, out sign);
        }
    }
}
