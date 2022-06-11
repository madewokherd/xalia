using Xalia.Gudl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Xalia.UiDom
{
    public enum UiDomRelationshipKind
    {
        ThisOrAncestor,
        ThisOrDescendent,
        Child,
        Parent,
        LastChild,
        NextSibling,
        PreviousSibling
    }

    public class UiDomRelationship : UiDomValue
    {
        public UiDomObject Owner { get; }
        public UiDomRelationshipKind Kind { get; }

        static UiDomRelationship()
        {
            Names = new Dictionary<string, UiDomRelationshipKind>();
            Names["this_or_ancestor_matches"] = UiDomRelationshipKind.ThisOrAncestor;
            Names["this_or_descendent_matches"] = UiDomRelationshipKind.ThisOrDescendent;
            Names["child_matches"] = UiDomRelationshipKind.Child;
            Names["parent_matches"] = UiDomRelationshipKind.Parent;
            Names["last_child_matches"] = UiDomRelationshipKind.LastChild;
            Names["next_sibling_matches"] = UiDomRelationshipKind.NextSibling;
            Names["previous_sibling_matches"] = UiDomRelationshipKind.PreviousSibling;
        }

        public UiDomRelationship(UiDomObject owner, UiDomRelationshipKind kind)
        {
            Owner = owner;
            Kind = kind;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomRelationship rel)
                return Owner.Equals(rel.Owner) && Kind == rel.Kind;
            return false;
        }

        public override int GetHashCode()
        {
            return (Owner, Kind).GetHashCode() ^ typeof(UiDomRelationship).GetHashCode();
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

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression expr, UiDomRoot root, [In, Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            depends_on.Add((Owner, new BinaryExpression(
                new IdentifierExpression(Name),
                expr,
                GudlToken.LParen)));
            return Owner.EvaluateRelationship(Kind, expr);
        }

        protected override UiDomValue EvaluateDot(UiDomValue context, GudlExpression expr, UiDomRoot root, [In, Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            return EvaluateApply(context, expr, root, depends_on);
        }
    }
}
