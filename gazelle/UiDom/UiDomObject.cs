using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gazelle.UiDom
{
    internal abstract class UiDomObject
    {
        internal abstract string DebugId { get; }

        protected void AddChild(int index, UiDomObject child)
        {
#if DEBUG
            Console.WriteLine("Child {0} added to {1} at index {2}", child.DebugId, DebugId, index);
#endif
        }

        protected void RemoveChild(UiDomObject child)
        {
#if DEBUG
            Console.WriteLine("Child {0} removed from {1}", child.DebugId, DebugId);
#endif
        }
    }
}
