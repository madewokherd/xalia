using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gazelle.Gudl;

namespace Gazelle.UiDom
{
    public abstract class UiDomObject : UiDomValue
    {
        public abstract string DebugId { get; }

        public List<UiDomObject> Children { get; } = new List<UiDomObject> ();

        public UiDomObject Parent { get; private set; }

        public bool IsAlive { get; private set; }

        public UiDomRoot Root { get; }

        protected virtual void SetAlive(bool value)
        {
            if (IsAlive != value)
            {
                IsAlive = value;
                if (value)
                    Utils.RunIdle(EvaluateRules); // This could infinitely recurse for badly-coded rules if we did it immediately
            }
        }

        public UiDomObject(UiDomRoot root)
        {
            Root = root;
        }

        internal UiDomObject()
        {
            if (this is UiDomRoot root)
            {
                Root = root;
                SetAlive(true);
            }
            else
                throw new InvalidOperationException("UiDomObject constructor with no arguments can only be used by UiDomRoot");
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

        public override string ToString()
        {
            return DebugId;
        }

        public UiDomValue Evaluate(GudlExpression expr, HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            return Evaluate(expr, Root, depends_on);
        }

        private void EvaluateRules()
        {
            var activeDeclarations = new Dictionary<string, UiDomValue>();
            bool stop = false;
            var depends_on = new HashSet<(UiDomObject, GudlExpression)>();
#if DEBUG
            Console.WriteLine($"rule evaluation for {this}");
#endif
            foreach ((GudlExpression expr, GudlDeclaration[] declaraions) in Root.Rules)
            {
                if (!(expr is null))
                {
                    UiDomValue condition = Evaluate(expr, Root, depends_on);

                    if (!condition.ToBool())
                        continue;
#if DEBUG
                    Console.WriteLine($"  matched condition {expr}");
#endif
                }
#if DEBUG
                else
                {
                    Console.WriteLine("  applying unconditional declarations");
                }
#endif

                foreach (var decl in declaraions)
                {
                    if (activeDeclarations.ContainsKey(decl.Property) && decl.Property != "stop")
                    {
                        continue;
                    }

                    UiDomValue value = Evaluate(decl.Value, depends_on);

#if DEBUG
                    Console.WriteLine($"  {decl.Property}: {value}");
#endif

                    if (decl.Property == "stop" && value.ToBool())
                        stop = true;

                    activeDeclarations[decl.Property] = value;
                }

                if (stop)
                    break;
            }

            foreach(var dep in depends_on)
            {
                Console.WriteLine($"  depends on: {dep}");
            }

            DeclarationsChanged(activeDeclarations, depends_on);
        }

        protected virtual void DeclarationsChanged(Dictionary<string, UiDomValue> all_declarations,
            HashSet<(UiDomObject, GudlExpression)> dependencies)
        {
            // TODO: Store declarations

            // TODO: Call PropertiesChanged for anything that changed

            // TODO: Add "notifier" for dependencies that queues EvaluateRules
        }

        protected virtual void PropertiesChanged(HashSet<GudlExpression> changed_properties)
        {
            // TODO: Call any notifiers that care about the changed properties
        }
    }
}
