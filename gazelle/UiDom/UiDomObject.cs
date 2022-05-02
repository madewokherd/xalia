using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

        private Dictionary<string, UiDomValue> _activeDeclarations = new Dictionary<string, UiDomValue>();

        private Dictionary<GudlExpression, LinkedList<PropertyChangeNotifier>> _propertyChangeNotifiers = new Dictionary<GudlExpression, LinkedList<PropertyChangeNotifier>>();

        private Dictionary<(UiDomObject, GudlExpression), IDisposable> _dependencyPropertyChangeNotifiers = new Dictionary<(UiDomObject, GudlExpression), IDisposable>();

        bool _updatingRules;

        protected virtual void SetAlive(bool value)
        {
            if (IsAlive != value)
            {
                IsAlive = value;
                if (value)
                {
                    _updatingRules = true;
                    Utils.RunIdle(EvaluateRules); // This could infinitely recurse for badly-coded rules if we did it immediately
                }
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

        public UiDomValue GetDeclaration(string property)
        {
            if (_activeDeclarations.TryGetValue(property, out var result))
                return result;
            return UiDomUndefined.Instance;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            depends_on.Add((this, new IdentifierExpression(id)));
            if (_activeDeclarations.TryGetValue(id, out var result))
                return result;
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        private void EvaluateRules()
        {
            _updatingRules = false;
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
                Console.WriteLine($"  depends on: {dep.Item1}.{dep.Item2}");
            }

            DeclarationsChanged(activeDeclarations, depends_on);
        }

        protected virtual void DeclarationsChanged(Dictionary<string, UiDomValue> all_declarations,
            HashSet<(UiDomObject, GudlExpression)> dependencies)
        {
            HashSet<GudlExpression> changed = new HashSet<GudlExpression>();

            foreach (var kvp in _activeDeclarations)
            {
                if (!all_declarations.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                    changed.Add(new IdentifierExpression(kvp.Key));
            }

            foreach (var key in all_declarations.Keys)
            {
                if (!_activeDeclarations.ContainsKey(key))
                    changed.Add(new IdentifierExpression(key));
            }

            _activeDeclarations = all_declarations;

            var updated_dependency_notifiers = new Dictionary<(UiDomObject, GudlExpression), IDisposable>();

            foreach (var dep in dependencies)
            {
                if (_dependencyPropertyChangeNotifiers.TryGetValue(dep, out var notifier))
                {
                    updated_dependency_notifiers[dep] = notifier;
                    _dependencyPropertyChangeNotifiers.Remove(dep);
                }
                else
                {
                    updated_dependency_notifiers.Add(dep,
                        dep.Item1.NotifyPropertyChanged(dep.Item2, OnDependencyPropertyChanged));
                }
            }
            foreach (var notifier in _dependencyPropertyChangeNotifiers.Values)
            {
                notifier.Dispose();
            }
            _dependencyPropertyChangeNotifiers = updated_dependency_notifiers;

            PropertiesChanged(changed);
        }

        private void OnDependencyPropertyChanged(UiDomObject obj, GudlExpression property)
        {
            if (!_updatingRules)
            {
                _updatingRules = true;
                Utils.RunIdle(EvaluateRules);
#if DEBUG
                Console.WriteLine($"queued rule evaluation for {this} because {obj}.{property} changed");
#endif
            }
        }

        public delegate void PropertyChangeHandler(UiDomObject obj, GudlExpression property);

        private class PropertyChangeNotifier : IDisposable
        {
            public PropertyChangeNotifier(UiDomObject obj, GudlExpression expression, PropertyChangeHandler handler)
            {
                Object = obj;
                Expression = expression;
                Handler = handler;
                Object.AddPropertyChangeNotifier(this);
            }

            public readonly UiDomObject Object;
            public readonly GudlExpression Expression;
            public readonly PropertyChangeHandler Handler;
            bool Disposed;

            public void Dispose()
            {
                if (!Disposed)
                {
                    Object.RemovePropertyChangeNotifier(this);
                }
                Disposed = true;
            }
        }

        private void RemovePropertyChangeNotifier(PropertyChangeNotifier propertyChangeNotifier)
        {
            var expr = propertyChangeNotifier.Expression;
            var notifiers = _propertyChangeNotifiers[expr];

            if (notifiers.Count == 1)
            {
                _propertyChangeNotifiers.Remove(expr);
                UnwatchProperty(expr);
                return;
            }

            notifiers.Remove(propertyChangeNotifier);
        }

        private void AddPropertyChangeNotifier(PropertyChangeNotifier propertyChangeNotifier)
        {
            var expr = propertyChangeNotifier.Expression;
            if (_propertyChangeNotifiers.TryGetValue(expr, out var notifiers))
            {
                notifiers.AddLast(propertyChangeNotifier);
            }
            else
            {
                _propertyChangeNotifiers.Add(expr,
                    new LinkedList<PropertyChangeNotifier>(new PropertyChangeNotifier[] { propertyChangeNotifier }));

                WatchProperty(expr);
            }
        }

        public IDisposable NotifyPropertyChanged(GudlExpression expression, PropertyChangeHandler handler)
        {
            return new PropertyChangeNotifier(this, expression, handler);
        }

        protected virtual void WatchProperty(GudlExpression expression)
        {

        }

        protected virtual void UnwatchProperty(GudlExpression expression)
        {

        }

        protected virtual void PropertiesChanged(HashSet<GudlExpression> changed_properties)
        {
            foreach (var prop in changed_properties)
            {
                if (_propertyChangeNotifiers.TryGetValue(prop, out var notifiers))
                {
                    foreach (var notifier in notifiers)
                    {
                        notifier.Handler(this, prop);
                    }
                }
            }
        }
    }
}
