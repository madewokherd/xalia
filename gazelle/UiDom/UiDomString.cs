using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gazelle.UiDom
{
    public class UiDomString : UiDomValue
    {
        public UiDomString(string value)
        {
            Value = value;
        }

        public override string ToString()
        {
            // FIXME: Escape string if necessary
            return $"\"{Value}\"";
        }
        public string Value { get; }

        public override bool ToBool()
        {
            return Value != string.Empty;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomString st)
                return Value == st.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ typeof(UiDomString).GetHashCode();
        }

        public override bool Compare(UiDomValue other, out int sign)
        {
            if (other is UiDomString s)
            {
                sign = string.CompareOrdinal(Value, s.Value);
                return true;
            }
            return base.Compare(other, out sign);
        }
    }
}
