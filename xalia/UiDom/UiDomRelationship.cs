using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    public enum UiDomRelationshipKind
    {
        ThisOrAncestor,
        ThisOrDescendent,
        Ancestor,
        Descendent,
        Child,
        Parent,
        LastChild,
        NextSibling,
        PreviousSibling
    }

    public class UiDomRelationship : UiDomValue
    {
        public UiDomElement Element { get; }
        public UiDomRelationshipKind Kind { get; }

        static UiDomRelationship()
        {
            Names = new Dictionary<string, UiDomRelationshipKind>();
            Names["this_or_ancestor_matches"] = UiDomRelationshipKind.ThisOrAncestor;
            Names["this_or_descendent_matches"] = UiDomRelationshipKind.ThisOrDescendent;
            Names["ancestor_matches"] = UiDomRelationshipKind.Ancestor;
            Names["descendent_matches"] = UiDomRelationshipKind.Descendent;
            Names["child_matches"] = UiDomRelationshipKind.Child;
            Names["parent_matches"] = UiDomRelationshipKind.Parent;
            Names["last_child_matches"] = UiDomRelationshipKind.LastChild;
            Names["next_sibling_matches"] = UiDomRelationshipKind.NextSibling;
            Names["previous_sibling_matches"] = UiDomRelationshipKind.PreviousSibling;
        }

        public UiDomRelationship(UiDomElement element, UiDomRelationshipKind kind)
        {
            Element = element;
            Kind = kind;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomRelationship rel)
                return Element.Equals(rel.Element) && Kind == rel.Kind;
            return false;
        }

        public override int GetHashCode()
        {
            return (Element, Kind).GetHashCode() ^ typeof(UiDomRelationship).GetHashCode();
        }

        public static Dictionary<string, UiDomRelationshipKind> Names;

        public static string NameFromKind(UiDomRelationshipKind kind)
        {
            switch (kind)
            {
                case UiDomRelationshipKind.ThisOrAncestor:
                    return "this_or_ancestor_matches";
                case UiDomRelationshipKind.ThisOrDescendent:
                    return "this_or_descendent_matches";
                case UiDomRelationshipKind.Ancestor:
                    return "ancestor_matches";
                case UiDomRelationshipKind.Descendent:
                    return "descendent_matches";
                case UiDomRelationshipKind.Child:
                    return "child_matches";
                case UiDomRelationshipKind.Parent:
                    return "parent_matches";
                case UiDomRelationshipKind.LastChild:
                    return "last_child_matches";
                case UiDomRelationshipKind.NextSibling:
                    return "next_sibling_matches";
                case UiDomRelationshipKind.PreviousSibling:
                    return "previous_sibling_matches";
                default:
                    return "unknown";
            }
        }

        public string Name => NameFromKind(Kind);

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 1)
                return UiDomUndefined.Instance;
            var expr = arglist[0];
            depends_on.Add((Element, new ApplyExpression(
                new IdentifierExpression(Name),
                new GudlExpression[] { expr })));
            return Element.EvaluateRelationship(Kind, expr);
        }

        protected override UiDomValue EvaluateDot(UiDomValue context, GudlExpression expr, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return EvaluateApply(context, new GudlExpression[] { expr }, root, depends_on);
        }

        public override string ToString()
        {
            return $"{Element}.{Name}";
        }
    }
}
