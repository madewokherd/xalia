using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Xalia.Gudl;

namespace Xalia.UiDom
{
    public abstract class UiDomElement : UiDomValue
    {
        public abstract string DebugId { get; }

        public List<UiDomElement> Children { get; } = new List<UiDomElement> ();

        public UiDomElement Parent { get; private set; }

        public bool IsAlive { get; private set; }

        public UiDomRoot Root { get; }

        public IReadOnlyCollection<string> Declarations => _activeDeclarations.Keys;

        private Dictionary<string, UiDomValue> _activeDeclarations = new Dictionary<string, UiDomValue>();

        private Dictionary<string, UiDomValue> _assignedProperties = new Dictionary<string, UiDomValue>();

        private Dictionary<GudlExpression, LinkedList<PropertyChangeNotifier>> _propertyChangeNotifiers = new Dictionary<GudlExpression, LinkedList<PropertyChangeNotifier>>();

        private Dictionary<(UiDomElement, GudlExpression), IDisposable> _dependencyPropertyChangeNotifiers = new Dictionary<(UiDomElement, GudlExpression), IDisposable>();

        bool _updatingRules;

        private Dictionary<GudlExpression, UiDomRelationshipWatcher> _relationshipWatchers = new Dictionary<GudlExpression, UiDomRelationshipWatcher>();
        private bool disposing;

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
                else
                {
                    disposing = true;
                    foreach (var watcher in _relationshipWatchers.Values)
                    {
                        watcher.Dispose();
                    }
                    _relationshipWatchers.Clear();
                    foreach (var depNotifier in _dependencyPropertyChangeNotifiers.Values)
                    {
                        depNotifier.Dispose();
                    }
                    _dependencyPropertyChangeNotifiers.Clear();
                    _updatingRules = false;
                    while (Children.Count != 0)
                    {
                        RemoveChild(Children.Count - 1);
                    }
                    Root?.RaiseElementDiedEvent(this);
                }
            }
        }

        public UiDomElement(UiDomRoot root)
        {
            Root = root;
        }

        internal UiDomElement()
        {
            if (this is UiDomRoot root)
            {
                Root = root;
                SetAlive(true);
            }
            else
                throw new InvalidOperationException("UiDomObject constructor with no arguments can only be used by UiDomRoot");
        }

        protected void AddChild(int index, UiDomElement child)
        {
#if DEBUG
            Console.WriteLine("Child {0} added to {1} at index {2}", child.DebugId, DebugId, index);
#endif
            if (child.Parent != null)
                throw new InvalidOperationException(string.Format("Attempted to add child {0} to {1} but it already has a parent of {2}", child.DebugId, DebugId, child.Parent.DebugId));
            child.Parent = this;
            Children.Insert(index, child);
            child.SetAlive(true);
            PropertyChanged("children");
        }

        internal void RelationshipValueChanged(UiDomRelationshipWatcher watcher)
        {
            PropertyChanged(watcher.AsProperty);
        }

        internal UiDomValue EvaluateRelationship(UiDomRelationshipKind kind, GudlExpression expr)
        {
            if (_relationshipWatchers.TryGetValue(
                new ApplyExpression(
                    new IdentifierExpression(UiDomRelationship.NameFromKind(kind)),
                    new GudlExpression[] { expr }),
                out var watcher))
            {
                return watcher.Value;
            }
            return UiDomUndefined.Instance;
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
            if (IsAlive)
                PropertyChanged("children");
        }

        public override string ToString()
        {
            return DebugId;
        }

        public UiDomValue Evaluate(GudlExpression expr, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return Evaluate(expr, Root, depends_on);
        }

        public void AssignProperty(string propName, UiDomValue propValue)
        {
            if (propValue is UiDomUndefined)
            {
                if (_assignedProperties.ContainsKey(propName))
                {
#if DEBUG
                    Console.WriteLine($"{this}.{propName} assigned: {propValue}");
#endif
                    _assignedProperties.Remove(propName);
                    PropertyChanged(new IdentifierExpression(propName));
                    return;
                }
            }

            if (!_assignedProperties.TryGetValue(propName, out var oldValue) || !oldValue.Equals(propValue))
            {
#if DEBUG
                    Console.WriteLine($"{this}.{propName} assigned: {propValue}");
#endif
                _assignedProperties[propName] = propValue;
                PropertyChanged(propName);
                return;
            }
        }

        public UiDomValue GetDeclaration(string property)
        {
            if (_activeDeclarations.TryGetValue(property, out var result) && !(result is UiDomUndefined))
                return result;
            if (_assignedProperties.TryGetValue(property, out result) && !(result is UiDomUndefined))
                return result;
            return UiDomUndefined.Instance;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "this":
                    return this;
                case "element_identifier":
                    return new UiDomString(DebugId);
                case "this_or_ancestor_matches":
                    return new UiDomRelationship(this, UiDomRelationshipKind.ThisOrAncestor);
                case "this_or_descendent_matches":
                    return new UiDomRelationship(this, UiDomRelationshipKind.ThisOrDescendent);
                case "ancestor_matches":
                    return new UiDomRelationship(this, UiDomRelationshipKind.Ancestor);
                case "descendent_matches":
                    return new UiDomRelationship(this, UiDomRelationshipKind.Descendent);
                case "child_matches":
                case "first_child_matches":
                    return new UiDomRelationship(this, UiDomRelationshipKind.Child);
                case "parent_matches":
                    return new UiDomRelationship(this, UiDomRelationshipKind.Parent);
                case "last_child_matches":
                    return new UiDomRelationship(this, UiDomRelationshipKind.LastChild);
                case "sibling_matches":
                case "first_sibling_matches":
                    {
                        if (Parent is null)
                            return UiDomUndefined.Instance;
                        return new UiDomRelationship(Parent, UiDomRelationshipKind.Child);
                    }
                case "last_sibling_matches":
                    {
                        if (Parent is null)
                            return UiDomUndefined.Instance;
                        return new UiDomRelationship(Parent, UiDomRelationshipKind.LastChild);
                    }
                case "next_sibling_matches":
                    {
                        if (Parent is null)
                            return UiDomUndefined.Instance;
                        return new UiDomRelationship(this, UiDomRelationshipKind.NextSibling);
                    }
                case "previous_sibling_matches":
                    {
                        if (Parent is null)
                            return UiDomUndefined.Instance;
                        return new UiDomRelationship(this, UiDomRelationshipKind.PreviousSibling);
                    }
                case "first_child":
                    {
                        depends_on.Add((this, new IdentifierExpression("children")));
                        if (Children.Count == 0)
                            return UiDomUndefined.Instance;
                        return Children[0];
                    }
                case "last_child":
                    {
                        depends_on.Add((this, new IdentifierExpression("children")));
                        if (Children.Count == 0)
                            return UiDomUndefined.Instance;
                        return Children[Children.Count - 1];
                    }
                case "first_sibling":
                    {
                        if (Parent is null)
                            return UiDomUndefined.Instance;
                        depends_on.Add((Parent, new IdentifierExpression("children")));
                        return Parent.Children[0];
                    }
                case "last_sibling":
                    {
                        if (Parent is null)
                            return UiDomUndefined.Instance;
                        depends_on.Add((Parent, new IdentifierExpression("children")));
                        return Parent.Children[Parent.Children.Count - 1];
                    }
                case "next_sibling":
                    {
                        if (Parent is null)
                            return UiDomUndefined.Instance;
                        depends_on.Add((Parent, new IdentifierExpression("children")));
                        int idx = Parent.Children.IndexOf(this) + 1;
                        if (idx < Parent.Children.Count)
                            return Parent.Children[idx];
                        return UiDomUndefined.Instance;
                    }
                case "previous_sibling":
                    {
                        if (Parent is null)
                            return UiDomUndefined.Instance;
                        depends_on.Add((Parent, new IdentifierExpression("children")));
                        int idx = Parent.Children.IndexOf(this) - 1;
                        if (idx >= 0)
                            return Parent.Children[idx];
                        return UiDomUndefined.Instance;
                    }
                case "parent":
                    // We assume for now that this cannot change during an object's lifetime
                    return (UiDomValue)Parent ?? UiDomUndefined.Instance;
                case "is_child_of":
                    return new UiDomIsRelationship(this, UiDomIsRelationship.IsRelationshipType.Child);
                case "is_parent_of":
                    return new UiDomIsRelationship(this, UiDomIsRelationship.IsRelationshipType.Parent);
                case "is_ancestor_of":
                    return new UiDomIsRelationship(this, UiDomIsRelationship.IsRelationshipType.Ancestor);
                case "is_descendent_of":
                    return new UiDomIsRelationship(this, UiDomIsRelationship.IsRelationshipType.Descendent);
                case "is_sibling_of":
                    return new UiDomIsRelationship(this, UiDomIsRelationship.IsRelationshipType.Sibling);
                case "is_root":
                    return UiDomBoolean.FromBool(this is UiDomRoot);
                case "root":
                    return root;
                case "assign":
                    return new UiDomAssign(this);
                case "simulate_dpad":
                    return new SimulateDpad();
                case "index_in_parent":
                    if (!(Parent is null))
                    {
                        depends_on.Add((Parent, new IdentifierExpression("children")));
                        return new UiDomInt(Parent.Children.IndexOf(this));
                    }
                    return UiDomUndefined.Instance;
                case "child_at_index":
                    return new UiDomChildAtIndex(this);
            }
            var result = root.Application.EvaluateIdentifierHook(this, id, depends_on);
            if (!(result is null))
            {
                return result;
            }
            depends_on.Add((this, new IdentifierExpression(id)));
            if (_activeDeclarations.TryGetValue(id, out result) && !(result is UiDomUndefined))
                return result;
            if (_assignedProperties.TryGetValue(id, out result) && !(result is UiDomUndefined))
                return result;
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        public virtual Task<(bool, int, int)> GetClickablePoint()
        {
            int x, y;
            if (GetDeclaration("target_x") is UiDomInt tx &&
                GetDeclaration("target_y") is UiDomInt ty &&
                GetDeclaration("target_width") is UiDomInt tw &&
                GetDeclaration("target_height") is UiDomInt th)
            {
                x = tx.Value + tw.Value / 2;
                y = ty.Value + th.Value / 2;
                return Task.FromResult((true, x, y));
            }
            return Task.FromResult((false, 0, 0));
        }

        private void EvaluateRules()
        {
            _updatingRules = false;
            if (!IsAlive)
                return;
            var activeDeclarations = new Dictionary<string, UiDomValue>();
            bool stop = false;
            var depends_on = new HashSet<(UiDomElement, GudlExpression)>();
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

            DeclarationsChanged(activeDeclarations, depends_on);

            Root?.RaiseElementDeclarationsChangedEvent(this);
        }

        protected virtual void DeclarationsChanged(Dictionary<string, UiDomValue> all_declarations,
            HashSet<(UiDomElement, GudlExpression)> dependencies)
        {
            HashSet<GudlExpression> changed = new HashSet<GudlExpression>();

            if (disposing)
            {
                return;
            }

            foreach (var kvp in _activeDeclarations)
            {
                if (!all_declarations.TryGetValue(kvp.Key, out var value) || !value.Equals(kvp.Value))
                    changed.Add(new IdentifierExpression(kvp.Key));
            }

            foreach (var key in all_declarations.Keys)
            {
                if (!_activeDeclarations.ContainsKey(key))
                    changed.Add(new IdentifierExpression(key));
            }

            _activeDeclarations = all_declarations;

            var updated_dependency_notifiers = new Dictionary<(UiDomElement, GudlExpression), IDisposable>();

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

        private void OnDependencyPropertyChanged(UiDomElement element, GudlExpression property)
        {
            if (!_updatingRules)
            {
                _updatingRules = true;
                Utils.RunIdle(EvaluateRules);
#if DEBUG
                Console.WriteLine($"queued rule evaluation for {this} because {element}.{property} changed");
#endif
            }
        }

        public delegate void PropertyChangeHandler(UiDomElement element, GudlExpression property);

        private class PropertyChangeNotifier : IDisposable
        {
            public PropertyChangeNotifier(UiDomElement element, GudlExpression expression, PropertyChangeHandler handler)
            {
                Element = element;
                Expression = expression;
                Handler = handler;
                Element.AddPropertyChangeNotifier(this);
            }

            public readonly UiDomElement Element;
            public readonly GudlExpression Expression;
            public readonly PropertyChangeHandler Handler;
            bool Disposed;

            public void Dispose()
            {
                if (!Disposed)
                {
                    Element.RemovePropertyChangeNotifier(this);
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
            if (disposing)
            {
                return;
            }
            if (expression is ApplyExpression apply)
            {
                if (apply.Left is IdentifierExpression prop &&
                    apply.Arglist.Length == 1)
                {
                    if (UiDomRelationship.Names.TryGetValue(prop.Name, out var kind))
                    {
                        _relationshipWatchers.Add(expression,
                            new UiDomRelationshipWatcher(this, kind, apply.Arglist[0]));
                    }
                }
            }
        }

        protected virtual void UnwatchProperty(GudlExpression expression)
        {
            if (disposing)
            {
                return;
            }
            if (_relationshipWatchers.TryGetValue(expression, out var watcher))
            {
                watcher.Dispose();
                _relationshipWatchers.Remove(expression);
            }
        }

        protected internal void PropertyChanged(string identifier)
        {
            PropertyChanged(new IdentifierExpression(identifier));
        }

        protected internal void PropertyChanged(GudlExpression property)
        {
            HashSet<GudlExpression> properties = new HashSet<GudlExpression>();
            properties.Add(property);
            PropertiesChanged(properties);
        }

        protected virtual void PropertiesChanged(HashSet<GudlExpression> changed_properties)
        {
            if (!IsAlive)
                return;
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
