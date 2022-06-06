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
        Child
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
