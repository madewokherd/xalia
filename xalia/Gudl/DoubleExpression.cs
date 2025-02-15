﻿using System.Globalization;

namespace Xalia.Gudl
{
    internal class DoubleExpression : GudlExpression
    {
        public DoubleExpression(double value)
        {
            Value = value;
        }

        public double Value { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is DoubleExpression d)
            {
                return Value == d.Value;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ typeof(DoubleExpression).GetHashCode();
        }

        internal override string ToString(out GudlPrecedence precedence)
        {
            precedence = GudlPrecedence.Atom;
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
