using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    public abstract class UiDomElement : UiDomValue
    {
        public string DebugId { get; }

        public List<UiDomElement> Children { get; } = new List<UiDomElement> ();

        public UiDomElement Parent { get; private set; }

        public bool IsAlive { get; private set; }

        public UiDomRoot Root { get; }

        public IReadOnlyCollection<string> Declarations => _activeDeclarations.Keys;

        public List<IUiDomProvider> Providers { get; private set; } = new List<IUiDomProvider>();

        private Dictionary<string, (GudlDeclaration, UiDomValue)> _activeDeclarations = new Dictionary<string, (GudlDeclaration, UiDomValue)>();

        private Dictionary<string, UiDomValue> _assignedProperties = new Dictionary<string, UiDomValue>();

        private Dictionary<GudlExpression, LinkedList<PropertyChangeNotifier>> _propertyChangeNotifiers = new Dictionary<GudlExpression, LinkedList<PropertyChangeNotifier>>();

        private Dictionary<(UiDomElement, GudlExpression), IDisposable> _dependencyPropertyChangeNotifiers = new Dictionary<(UiDomElement, GudlExpression), IDisposable>();

        bool _updatingRules;

        private Dictionary<GudlExpression, UiDomRelationshipWatcher> _relationshipWatchers = new Dictionary<GudlExpression, UiDomRelationshipWatcher>();
        private bool disposing;

        private Dictionary<GudlExpression, bool> polling_properties = new Dictionary<GudlExpression, bool>();
        private Dictionary<GudlExpression, CancellationTokenSource> polling_refresh_tokens = new Dictionary<GudlExpression, CancellationTokenSource>();

        private LinkedList<string[]> tracked_property_lists = new LinkedList<string[]>();
        private bool updating_tracked_properties;
        private Dictionary<string, UiDomValue> tracked_property_values = new Dictionary<string, UiDomValue>();
        private Dictionary<(UiDomElement, GudlExpression), IDisposable> tracked_property_notifiers = new Dictionary<(UiDomElement, GudlExpression), IDisposable>();

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
                    foreach (var provider in Providers)
                    {
                        provider.NotifyElementRemoved(this);
                    }
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
                    foreach (var token in polling_refresh_tokens)
                    {
                        if (!(token.Value is null))
                            token.Value.Cancel(); 
                    }
                    polling_refresh_tokens.Clear();
                    polling_properties.Clear();
                    _updatingRules = false;
                    while (Children.Count != 0)
                    {
                        RemoveChild(Children.Count - 1);
                    }
                    Root?.RaiseElementDiedEvent(this);
                }
            }
        }

        public UiDomElement(string debug_id, UiDomRoot root)
        {
            DebugId = debug_id;
            Root = root;
        }

        internal UiDomElement()
        {
            if (this is UiDomRoot root)
            {
                Root = root;
                DebugId = "root";
                SetAlive(true);
            }
            else
                throw new InvalidOperationException("UiDomObject constructor with no arguments can only be used by UiDomRoot");
        }

        protected void AddChild(int index, UiDomElement child)
        {
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"Child {child} added to {this} at index {index}");
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
            if (MatchesDebugCondition() || child.MatchesDebugCondition())
                Utils.DebugWriteLine($"Child {child} removed from {this}");
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
                    if (MatchesDebugCondition())
                        Utils.DebugWriteLine($"{this}.{propName} assigned: {propValue}");
                    _assignedProperties.Remove(propName);
                    PropertyChanged(new IdentifierExpression(propName));
                    return;
                }
            }

            if (!_assignedProperties.TryGetValue(propName, out var oldValue) || !oldValue.Equals(propValue))
            {
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.{propName} assigned: {propValue}");
                _assignedProperties[propName] = propValue;
                PropertyChanged(propName);
                return;
            }
        }

        public UiDomValue GetDeclaration(string property)
        {
            if (_activeDeclarations.TryGetValue(property, out var decl) && !(decl.Item2 is UiDomUndefined))
                return decl.Item2;
            if (_assignedProperties.TryGetValue(property, out var result) && !(result is UiDomUndefined))
                return result;
            return UiDomUndefined.Instance;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            UiDomValue value;
            foreach (var provider in Providers)
            {
                value = provider.EvaluateIdentifier(this, id, depends_on);
                if (!(value is UiDomUndefined))
                    return value;
            }
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
                    return new UiDomMethod(this, "assign", AssignFn);
                case "index_in_parent":
                    if (!(Parent is null))
                    {
                        depends_on.Add((Parent, new IdentifierExpression("children")));
                        return new UiDomInt(Parent.Children.IndexOf(this));
                    }
                    return UiDomUndefined.Instance;
                case "child_at_index":
                    return new UiDomMethod(this, "child_at_index", ChildAtIndexFn);
                case "child_count":
                    depends_on.Add((this, new IdentifierExpression("children")));
                    return new UiDomInt(Children.Count);
                case "repeat_action":
                    return UiDomRepeatAction.GetMethod();
                case "do_action":
                    return UiDomDoAction.Instance;
                case "map_directions":
                    return new UiDomMethod("map_directions", UiDomMapDirections.ApplyFn);
                case "adjust_scrollbars":
                    return new UiDomMethod("adjust_scrollbars", AdjustScrollbarsMethod);
                case "adjust_value":
                    return new UiDomMethod("adjust_value", AdjustValueMethod);
                case "radial_deadzone":
                    return new UiDomMethod("radial_deadzone", UiDomRadialDeadzone.ApplyFn);
            }
            var result = root.Application.EvaluateIdentifierHook(this, id, depends_on);
            if (!(result is null))
            {
                return result;
            }
            depends_on.Add((this, new IdentifierExpression(id)));
            if (_activeDeclarations.TryGetValue(id, out var decl) && !(decl.Item2 is UiDomUndefined))
                return decl.Item2;
            if (_assignedProperties.TryGetValue(id, out result) && !(result is UiDomUndefined))
                return result;
            foreach (var provider in Providers)
            {
                value = provider.EvaluateIdentifierLate(this, id, depends_on);
                if (!(value is UiDomUndefined))
                    return value;
            }
            return UiDomUndefined.Instance;
        }

        private UiDomValue AdjustValueMethod(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length < 1)
                return UiDomUndefined.Instance;
            if (!Evaluate(arglist[0], depends_on).TryToDouble(out var offset))
                return UiDomUndefined.Instance;
            return new UiDomRoutineAsync(this, "adjust_value", new UiDomValue[] {new UiDomDouble(offset)}, AdjustValueRoutine);
        }

        private static async Task AdjustValueRoutine(UiDomRoutineAsync obj)
        {
            await obj.Element.OffsetValue(((UiDomDouble)obj.Arglist[0]).Value);
        }

        private UiDomValue ChildAtIndexFn(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 1)
                return UiDomUndefined.Instance;
            var expr = arglist[0];
            UiDomValue right = context.Evaluate(expr, root, depends_on);
            if (right is UiDomInt i)
            {
                depends_on.Add((this, new IdentifierExpression("children")));
                if (i.Value >= 0 && i.Value < Children.Count)
                {
                    return Children[i.Value];
                }
            }
            return UiDomUndefined.Instance;
        }

        private UiDomValue AssignFn(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 2)
                return UiDomUndefined.Instance;

            var name = context.Evaluate(arglist[0], root, depends_on);

            if (!(name is UiDomString st))
            {
                return UiDomUndefined.Instance;
            }

            var value = context.Evaluate(arglist[1], root, depends_on);

            UiDomValue[] values = new UiDomValue[] { name, value };

            return new UiDomRoutineSync(this, "assign", values, DoAssign);
        }

        private static void DoAssign(UiDomRoutineSync obj)
        {
            obj.Element.AssignProperty(((UiDomString)obj.Arglist[0]).Value, obj.Arglist[1]);
        }

        public virtual async Task<(bool, int, int)> GetClickablePoint()
        {
            int x, y;
            if (GetDeclaration("target_x") is UiDomInt tx &&
                GetDeclaration("target_y") is UiDomInt ty &&
                GetDeclaration("target_width") is UiDomInt tw &&
                GetDeclaration("target_height") is UiDomInt th)
            {
                x = tx.Value + tw.Value / 2;
                y = ty.Value + th.Value / 2;
                return (true, x, y);
            }
            foreach (var provider in Providers)
            {
                var result = await provider.GetClickablePointAsync(this);
                if (result.Item1)
                    return result;
            }
            return (false, 0, 0);
        }

        static GudlExpression debugCondition;

        public bool MatchesDebugCondition()
        {
            if (debugCondition is null)
            {
                string conditionStr = Environment.GetEnvironmentVariable("XALIA_DEBUG");
                if (conditionStr is null)
                {
                    debugCondition = new IdentifierExpression("false");
                }
                else
                {
                    debugCondition = GudlParser.ParseExpression(conditionStr);
                }
            }
            return Evaluate(debugCondition, new HashSet<(UiDomElement, GudlExpression)>()).ToBool();
        }

        private void EvaluateRules()
        {
            _updatingRules = false;
            if (!IsAlive)
                return;
            var activeDeclarations = new Dictionary<string, (GudlDeclaration, UiDomValue)>();
            bool stop = false;
            var depends_on = new HashSet<(UiDomElement, GudlExpression)>();
            foreach ((GudlExpression expr, GudlDeclaration[] declarations) in Root.Rules)
            {
                bool any_new_declarations = false;
                foreach (var decl in declarations)
                {
                    if (decl.Property == "stop" || !activeDeclarations.ContainsKey(decl.Property))
                    {
                        any_new_declarations = true;
                        break;
                    }
                }
                if (!any_new_declarations)
                    continue;

                if (!(expr is null))
                {
                    UiDomValue condition = Evaluate(expr, Root, depends_on);

                    if (!condition.ToBool())
                        continue;
                }

                foreach (var decl in declarations)
                {
                    if (activeDeclarations.ContainsKey(decl.Property) && decl.Property != "stop")
                    {
                        continue;
                    }

                    UiDomValue value = Evaluate(decl.Value, depends_on);

                    if (decl.Property == "stop" && value.ToBool())
                        stop = true;

                    activeDeclarations[decl.Property] = (decl, value);
                }

                if (stop)
                    break;
            }

            DeclarationsChanged(activeDeclarations, depends_on);

            Root?.RaiseElementDeclarationsChangedEvent(this);

            if (MatchesDebugCondition())
            {
                Utils.DebugWriteLine($"properties for {DebugId}:");
                DumpProperties();
            }
        }

        protected virtual void DumpProperties()
        {
            foreach (var provider in Providers)
            {
                provider.DumpProperties(this);
            }
            if (!(Parent is null))
            {
                Utils.DebugWriteLine($"  parent: {Parent.DebugId}");
                Utils.DebugWriteLine($"  index_in_parent: {Parent.Children.IndexOf(this)}");
            }
            for (int i = 0; i < Children.Count; i++)
            {
                Utils.DebugWriteLine($"  child_at_index({i}): {Children[i].DebugId}");
            }
            foreach (var kvp in _relationshipWatchers)
            {
                if (!(kvp.Value.Value is UiDomUndefined))
                {
                    Utils.DebugWriteLine($"  {kvp.Key}: {kvp.Value.Value}");
                }
            }
            Root.Application.DumpElementProperties(this);
            foreach (var kvp in _activeDeclarations)
            {
                if (!(kvp.Value.Item2 is UiDomUndefined))
                {
                    Utils.DebugWriteLine($"  {kvp.Key}: {kvp.Value.Item2} [{kvp.Value.Item1.Position}]");
                }
            }
            foreach (var kvp in _assignedProperties)
            {
                if (!(kvp.Value is UiDomUndefined))
                {
                    Utils.DebugWriteLine($"  {kvp.Key}: {kvp.Value} [assigned]");
                }
            }
        }

        private void DeclarationsChanged(Dictionary<string, (GudlDeclaration, UiDomValue)> all_declarations,
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
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"queued rule evaluation for {this} because {element}.{property} changed");
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
            foreach (var provider in Providers)
            {
                if (provider.WatchProperty(this, expression))
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
            foreach (var provider in Providers)
            {
                if (provider.UnwatchProperty(this, expression))
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

        protected internal void PropertyChanged(string identifier, object val)
        {
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.{identifier}: {val}");
            PropertyChanged(identifier);
        }

        protected internal void PropertyChanged(GudlExpression property)
        {
            HashSet<GudlExpression> properties = new HashSet<GudlExpression> { property };
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

        private static UiDomValue AdjustScrollbarsMethod(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length >= 2)
            {
                var hscroll = context.Evaluate(arglist[0], root, depends_on) as UiDomElement;
                var vscroll = context.Evaluate(arglist[1], root, depends_on) as UiDomElement;

                if (hscroll is null && vscroll is null)
                    return UiDomUndefined.Instance;

                return new UiDomAdjustScrollbars(hscroll, vscroll);
            }
            return UiDomUndefined.Instance;
        }

        public virtual Task<double> GetMinimumIncrement()
        {
            return Task.FromResult(25.0);
        }

        public virtual Task OffsetValue(double ofs)
        {
            return Task.CompletedTask;
        }

        public void PollProperty(GudlExpression expression, Func<Task> refresh_function, int default_interval)
        {
            if (!polling_properties.TryGetValue(expression, out var polling) || !polling)
            {
                polling_properties[expression] = true;
                Utils.RunTask(DoPollProperty(expression, refresh_function, default_interval));
            }
        }

        private async Task DoPollProperty(GudlExpression expression, Func<Task> refresh_function, int default_interval)
        {
            if (!polling_properties.TryGetValue(expression, out bool polling) || !polling)
                return;

            await refresh_function();

            if (!polling_properties.TryGetValue(expression, out polling) || !polling)
                return;

            var token = new CancellationTokenSource();

            polling_refresh_tokens[expression] = token;

            try
            {
                await Task.Delay(default_interval);
            }
            catch (TaskCanceledException)
            {
                polling_refresh_tokens[expression] = null;
                return;
            }

            polling_refresh_tokens[expression] = null;
            Utils.RunTask(DoPollProperty(expression, refresh_function, default_interval));
        }

        public void EndPollProperty(GudlExpression expression)
        {
            if (polling_properties.TryGetValue(expression, out var polling) && polling)
            {
                polling_properties[expression] = false;
                if (polling_refresh_tokens.TryGetValue(expression, out var token) && !(token is null))
                {
                    token.Cancel();
                    polling_refresh_tokens[expression] = null;
                }
            }
        }

        private void OnTrackedDependencyChanged(UiDomElement element, GudlExpression property)
        {
            if (!updating_tracked_properties)
            {
                updating_tracked_properties = true;
                Utils.RunIdle(UpdateTrackedProperties);
            }
        }

        protected void RegisterTrackedProperties(string[] properties)
        {
            tracked_property_lists.AddLast(properties);
            if (!updating_tracked_properties)
            {
                updating_tracked_properties = true;
                Utils.RunIdle(UpdateTrackedProperties);
            }
        }

        private void UpdateTrackedProperties()
        {
            updating_tracked_properties = false;

            Dictionary<string, UiDomValue> new_property_values = new Dictionary<string, UiDomValue>();
            HashSet<(UiDomElement, GudlExpression)> depends_on = new HashSet<(UiDomElement, GudlExpression)>();

            // Evaluate all tracked properties
            foreach (var proplist in tracked_property_lists)
            {
                foreach (var propname in proplist)
                {
                    if (new_property_values.ContainsKey(propname))
                        continue;
                    var propvalue = EvaluateIdentifier(propname, Root, depends_on);
                    new_property_values[propname] = propvalue;
                }
            }

            // Update dependency notifiers
            var updated_dependency_notifiers = new Dictionary<(UiDomElement, GudlExpression), IDisposable>();
            foreach (var dep in depends_on)
            {
                if (tracked_property_notifiers.TryGetValue(dep, out var notifier))
                {
                    updated_dependency_notifiers[dep] = notifier;
                    tracked_property_notifiers.Remove(dep);
                }
                else
                {
                    updated_dependency_notifiers.Add(dep,
                        dep.Item1.NotifyPropertyChanged(dep.Item2, OnTrackedDependencyChanged));
                }
            }
            foreach (var notifier in tracked_property_notifiers.Values)
            {
                notifier.Dispose();
            }
            tracked_property_notifiers = updated_dependency_notifiers;

            // Notify subclass of updates
            var old_values = tracked_property_values;
            tracked_property_values = new_property_values;
            foreach (var kvp in new_property_values)
            {
                if (!old_values.TryGetValue(kvp.Key, out var old_value) || !kvp.Value.Equals(old_value))
                {
                    TrackedPropertyChanged(kvp.Key, kvp.Value);
                }
            }
        }

        protected virtual void TrackedPropertyChanged(string name, UiDomValue new_value)
        {
            foreach (var provider in Providers)
            {
                provider.TrackedPropertyChanged(this, name, new_value);
            }
        }

        public void AddProvider(IUiDomProvider provider, int index)
        {
            Providers.Insert(index, provider);
            if (!_updatingRules)
            {
                _updatingRules = true;
                Utils.RunIdle(EvaluateRules);
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"queued rule evaluation for {this} because {provider} was added");
            }
        }

        public void AddProvider(IUiDomProvider provider)
        {
            AddProvider(provider, Providers.Count);
        }
    }
}
