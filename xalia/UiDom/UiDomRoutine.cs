using System;
using System.Collections;
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
        public UiDomValue[] Arglist { get; }

        public UiDomRoutine(UiDomElement element, string name, UiDomValue[] arglist)
        {
            Element = element;
            Name = name;
            Arglist = arglist;
        }

        public UiDomRoutine() : this(null, null, null) { }
        public UiDomRoutine(UiDomElement element) : this(element, null, null) { }
        public UiDomRoutine(string name) : this(null, name, null) { }
        public UiDomRoutine(UiDomElement element, string name) : this(element, name, null) { }
        public UiDomRoutine(string name, UiDomValue[] arglist) : this(null, name, arglist) { }

        public abstract Task ProcessInputQueue(InputQueue queue);

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Element != null)
            {
                sb.Append(Element.ToString());
                sb.Append('.');
            }
            if (Name != null)
            {
                sb.Append(Name);
            }
            if (Arglist != null)
            {
                sb.Append('(');
                for (int i = 0; i < Arglist.Length; i++)
                {
                    sb.Append(Arglist[i].ToString());
                    if (i != Arglist.Length - 1)
                        sb.Append(", ");
                }
                sb.Append(')');
            }
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            // Assumes that any 2 routines on the same element, name, and arguments are identical.
            // We should maintain that constraint for debugging purposes anyway.
            if (obj is UiDomRoutine rou)
            {
                if (Element != rou.Element || Name != rou.Name)
                    return false;
                if ((Arglist is null) != (rou.Arglist is null))
                    return false;
                if (!(Arglist is null))
                {
                    if (Arglist.Length != rou.Arglist.Length)
                        return false;
                    for (int i = 0; i < Arglist.Length; i++)
                        if (!Arglist[i].Equals(rou.Arglist[i]))
                            return false;
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (Element, Name,
                Arglist != null ? 0 : StructuralComparisons.StructuralEqualityComparer.GetHashCode(Arglist)
                ).GetHashCode() ^ typeof(UiDomRoutine).GetHashCode();
        }

        public void Pulse()
        {
            var queue = new InputQueue();
            queue.Enqueue(new InputState(InputStateKind.Pulse));
            queue.Enqueue(new InputState(InputStateKind.Disconnected));
            Utils.RunTask(ProcessInputQueue(queue));
        }
    }
}
