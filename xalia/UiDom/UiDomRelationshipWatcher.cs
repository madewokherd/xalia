using Xalia.Gudl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xalia.UiDom
{
    internal class UiDomRelationshipWatcher : IDisposable
    {
        public UiDomRelationshipWatcher(UiDomElement element, UiDomRelationshipKind kind, GudlExpression expr)
        {
            Element = element;
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

        public UiDomElement Element { get; }
        public UiDomRelationshipKind Kind { get; }
        public GudlExpression Expression { get; }

        public UiDomValue Value { get; private set; }

        public GudlExpression AsProperty { get; }

        private bool updating;

        private bool disposed;

        private Dictionary<(UiDomElement, GudlExpression), IDisposable> dependencies = new Dictionary<(UiDomElement, GudlExpression), IDisposable>();

        UiDomValue CalculateValue(HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (Kind)
            {
                case UiDomRelationshipKind.ThisOrAncestor:
                    {
                        UiDomValue this_result = Element.Evaluate(Expression, depends_on);
                        if (this_result.ToBool())
                            return Element;
                        if (!(Element.Parent is null))
                        {
                            return Element.Parent.Evaluate(
                                AsProperty,
                                depends_on);
                        }
                        return UiDomUndefined.Instance;
                    }
                case UiDomRelationshipKind.ThisOrDescendent:
                    {
                        UiDomValue this_result = Element.Evaluate(Expression, depends_on);
                        if (this_result.ToBool())
                            return Element;
                        depends_on.Add((Element, new IdentifierExpression("children")));
                        UiDomValue match = null;
                        foreach (var child in Element.Children)
                        {
                            var value = child.Evaluate(AsProperty, depends_on);
                            if (match == null && value.ToBool())
                            {
                                match = value;
                            }
                            // Continue evaluating other children to add dependencies
                        }
                        if (match != null)
                            return match;
                        return UiDomUndefined.Instance;
                    }
                case UiDomRelationshipKind.Child:
                    {
                        depends_on.Add((Element, new IdentifierExpression("children")));
                        UiDomValue match = null;
                        foreach (var child in Element.Children)
                        {
                            var value = child.Evaluate(Expression, depends_on);
                            if (match == null && value.ToBool())
                            {
                                match = child;
                            }
                            // Continue evaluating other children to add dependencies
                        }
                        if (match != null)
                            return match;
                        return UiDomUndefined.Instance;
                    }
                case UiDomRelationshipKind.LastChild:
                    {
                        depends_on.Add((Element, new IdentifierExpression("children")));
                        UiDomValue match = null;
                        foreach (var child in Element.Children)
                        {
                            if (child.Evaluate(Expression, depends_on).ToBool())
                                match = child;
                        }
                        if (match != null)
                            return match;
                        return UiDomUndefined.Instance;
                    }
                case UiDomRelationshipKind.NextSibling:
                    {
                        if (Element.Parent is null)
                            return UiDomUndefined.Instance;
                        depends_on.Add((Element.Parent, new IdentifierExpression("children")));
                        int idx = Element.Parent.Children.IndexOf(Element);
                        for (idx = idx + 1; idx < Element.Parent.Children.Count; idx++)
                        {
                            var child = Element.Parent.Children[idx];
                            if (child.Evaluate(Expression, depends_on).ToBool())
                                return child;
                        }    
                        return UiDomUndefined.Instance;
                    }
                case UiDomRelationshipKind.PreviousSibling:
                    {
                        if (Element.Parent is null)
                            return UiDomUndefined.Instance;
                        depends_on.Add((Element.Parent, new IdentifierExpression("children")));
                        int idx = Element.Parent.Children.IndexOf(Element);
                        for (idx = idx - 1; idx >= 0; idx--)
                        {
                            var child = Element.Parent.Children[idx];
                            if (child.Evaluate(Expression, depends_on).ToBool())
                                return child;
                        }
                        return UiDomUndefined.Instance;
                    }
                case UiDomRelationshipKind.Parent:
                    {
                        if (Element.Parent is null)
                            return UiDomUndefined.Instance;
                        if (Element.Parent.Evaluate(Expression, depends_on).ToBool())
                            return Element.Parent;
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
            var new_depends_on = new HashSet<(UiDomElement, GudlExpression)>();
            var new_value = CalculateValue(new_depends_on);

            if (!new_value.Equals(Value))
            {
#if DEBUG
                Console.WriteLine($"{Element}.{AsProperty}: {new_value}");
#endif
                Value = new_value;
                Element.RelationshipValueChanged(this);
            }

            var new_dependencies = new Dictionary<(UiDomElement, GudlExpression), IDisposable>();
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
#if DEBUG
                    Console.WriteLine($"{Element}.{AsProperty} depends on: {dependency.Item1}.{dependency.Item2}");
#endif
                }
            }
#if DEBUG
            foreach (var old_dep in dependencies.Keys)
            {
                Console.WriteLine($"{Element}.{AsProperty} no longer depends on: {old_dep.Item1}.{old_dep.Item2}");
            }
#endif
            foreach (var old_notifier in dependencies.Values)
            {
                old_notifier.Dispose();
            }
            dependencies = new_dependencies;
        }

        private void DependencyChanged(UiDomElement element, GudlExpression property)
        {
            if (!updating)
            {
#if DEBUG
                Console.WriteLine($"queued evaluation of {Element}.{AsProperty} because {element}.{property} changed");
#endif
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
