using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xalia.Input;

namespace Xalia.UiDom
{
    public abstract class UiDomRoutine : UiDomValue
    {
        public UiDomElement Element { get; }
        public string Name { get; }

        public UiDomRoutine(UiDomElement element, string name)
        {
            Element = element;
            Name = name;
        }

        public UiDomRoutine() : this(null, null) { }
        public UiDomRoutine(UiDomElement element) : this(element, null) { }
        public UiDomRoutine(string name) : this(null, name) { }

        public abstract Task ProcessInputQueue(InputQueue queue);

        public override string ToString()
        {
            if (Name != null)
            {
                if (Element != null)
                {
                    return $"{Element}.{Name}";
                }
                return Name;
            }
            return base.ToString();
        }

        public override bool Equals(object obj)
        {
            // Assumes that any 2 routines on the same element with the same name are identical.
            // We should maintain that constraint for debugging purposes anyway.
            if (obj is UiDomRoutine rou)
            {
                return Element == rou.Element && Name == rou.Name;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (Element, Name).GetHashCode() ^ typeof(UiDomRoutine).GetHashCode();
        }
    }
}
