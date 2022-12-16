using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    internal class UiDomMethod : UiDomValue
    {
        public UiDomElement Element { get; }
        public string Name { get; }
        public ApplyFn ApplyFunction { get; }

        public delegate UiDomValue ApplyFn(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on);

        public UiDomMethod(UiDomElement element, string name, ApplyFn apply_function)
        {
            Element = element;
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
                return m.Element == Element && m.Name == Name;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(UiDomMethod).GetHashCode() ^ (Element, Name).GetHashCode();
        }

        public override string ToString()
        {
            if (Element is null)
                return Name;
            return $"{Element}.{Name}";
        }
    }
}
