using Gazelle.Gudl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gazelle.UiDom
{
    internal class UiDomRelationshipWatcher : IDisposable
    {
        public UiDomRelationshipWatcher(UiDomObject owner, UiDomRelationshipKind kind, GudlExpression expr)
        {
            Owner = owner;
            Kind = kind;
            Expression = expr;
            Value = UiDomUndefined.Instance;
            AsProperty = new BinaryExpression(
                new IdentifierExpression(UiDomRelationship.NameFromKind(Kind)),
                Expression,
                GudlToken.LParen);
            updating = true;
            Utils.RunIdle(Update);
        }

        public UiDomObject Owner { get; }
        public UiDomRelationshipKind Kind { get; }
        public GudlExpression Expression { get; }

        public UiDomValue Value { get; private set; }

        public GudlExpression AsProperty { get; }

        private bool updating;

        private bool disposed;

        private Dictionary<(UiDomObject, GudlExpression), IDisposable> dependencies = new Dictionary<(UiDomObject, GudlExpression), IDisposable>();

        UiDomValue CalculateValue(HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            switch (Kind)
            {
                case UiDomRelationshipKind.ThisOrAncestor:
                    {
                        UiDomValue this_result = Owner.Evaluate(Expression, depends_on);
                        if (this_result.ToBool())
                            return Owner;
                        if (!(Owner.Parent is null))
                        {
                            return Owner.Parent.Evaluate(
                                AsProperty,
                                depends_on);
                        }
                        return UiDomUndefined.Instance;
                    }
                default:
                    return UiDomUndefined.Instance;
            }
        }

        void Update()
        {
            if (disposed)
                return;
            updating = false;
            var new_depends_on = new HashSet<(UiDomObject, GudlExpression)>();
            var new_value = CalculateValue(new_depends_on);

            if (new_value != Value)
            {
#if DEBUG
                Console.WriteLine($"{Owner}.{AsProperty}: {new_value}");
#endif
                Value = new_value;
                Owner.RelationshipValueChanged(this);
            }

            var new_dependencies = new Dictionary<(UiDomObject, GudlExpression), IDisposable>();
            foreach (var dependency in new_depends_on)
            {
                if (dependencies.TryGetValue(dependency, out var existing_notifier))
                {
                    dependencies.Remove(dependency);
                    new_dependencies[dependency] = existing_notifier;
                }
                else
                {
                    new_dependencies[dependency] = dependency.Item1.NotifyPropertyChanged(
                        dependency.Item2, DependencyChanged);
                }
            }
            foreach (var old_notifier in dependencies.Values)
            {
                old_notifier.Dispose();
            }
            dependencies = new_dependencies;
        }

        private void DependencyChanged(UiDomObject obj, GudlExpression property)
        {
            if (!updating)
            {
                updating = true;
                Utils.RunIdle(Update);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                foreach (var old_notifier in dependencies.Values)
                {
                    old_notifier.Dispose();
                }
            }
        }
    }
}
