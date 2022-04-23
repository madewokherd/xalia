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

        internal List<UiDomObject> Children { get; } = new List<UiDomObject> ();

        internal UiDomObject Parent { get; private set; }

        internal readonly bool IsRoot;

        internal bool IsAlive { get; private set; }

        protected virtual void SetAlive(bool value)
        {
            IsAlive = value;
            // TODO: begin rule processing
        }

        internal UiDomObject(bool is_root)
        {
            IsRoot = is_root;
            if (IsRoot)
            {
                SetAlive(true);
            }
        }

        internal UiDomObject() : this(false)
        {

        }

        protected void AddChild(int index, UiDomObject child)
        {
#if DEBUG
            Console.WriteLine("Child {0} added to {1} at index {2}", child.DebugId, DebugId, index);
#endif
            if (child.Parent != null)
                throw new InvalidOperationException(string.Format("Attempted to add child {0} to {1} but it already has a parent of {2}", child.DebugId, DebugId, child.Parent.DebugId));
            child.Parent = this;
            Children.Insert(index, child);
            child.SetAlive(true);
        }

        protected void RemoveChild(int index)
        {
            var child = Children[index];
#if DEBUG
            Console.WriteLine("Child {0} removed from {1}", child.DebugId, DebugId);
#endif
            Children.RemoveAt(index);
            child.Parent = null;
            child.SetAlive(false);
        }
    }
}
