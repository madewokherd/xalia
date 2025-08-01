using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    internal class UiDomMethod : UiDomValue
    {
        public UiDomValue Value { get; }
        public UiDomElement Element { get => Value as UiDomElement; }
        public string Name { get; }
        public ApplyFn ApplyFunction { get; }

        public delegate UiDomValue ApplyFn(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on);

        public UiDomMethod(UiDomValue value, string name, ApplyFn apply_function)
        {
            Value = value;
            Name = name;
            ApplyFunction = apply_function;
        }

        public UiDomMethod(string name, ApplyFn apply_function) : this(null, name, apply_function)
        {
        }

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return ApplyFunction(this, context, arglist, root, depends_on);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomMethod m)
            {
                return m.Value == Value && m.Name == Name;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(UiDomMethod).GetHashCode() ^ (Value, Name).GetHashCode();
        }

        public override string ToString()
        {
            if (Value is null)
                return Name;
            return $"{Value}.{Name}";
        }
    }
}
