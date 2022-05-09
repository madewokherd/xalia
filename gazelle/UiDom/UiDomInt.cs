using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gazelle.UiDom
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
    }
}
