using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    internal class UiDomIsRelationship : UiDomValue
    {
        public UiDomIsRelationship(UiDomElement element, IsRelationshipType relationship_type)
        {
            Element = element;
            RelationshipType = relationship_type;
        }

        public UiDomElement Element { get; }
        public IsRelationshipType RelationshipType { get; }

        public enum IsRelationshipType
        {
            Child,
            Parent,
            Ancestor,
            Descendent,
            Sibling
        }

        public override string ToString()
        {
            switch (RelationshipType)
            {
                case IsRelationshipType.Child:
                    return "is_child_of";
                case IsRelationshipType.Parent:
                    return "is_parent_of";
                case IsRelationshipType.Ancestor:
                    return "is_ancestor_of";
                case IsRelationshipType.Descendent:
                    return "is_descendent_of";
                case IsRelationshipType.Sibling:
                    return "is_sibling_of";
            }
            return base.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is UiDomIsRelationship rel)
            {
                return rel.Element.Equals(Element) && rel.RelationshipType == RelationshipType;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (Element, RelationshipType).GetHashCode() ^ typeof(UiDomIsRelationship).GetHashCode();
        }

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression expr, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            var right_value = context.Evaluate(expr, root, depends_on);
            if (right_value is UiDomElement other && !(right_value is null))
            {
                switch (RelationshipType)
                {
                    case IsRelationshipType.Child:
                        return UiDomBoolean.FromBool(other.Parent.Equals(Element));
                    case IsRelationshipType.Parent:
                        return UiDomBoolean.FromBool(Element.Parent.Equals(other));
                    case IsRelationshipType.Ancestor:
                        while (!(other.Parent is null))
                        {
                            if (other.Parent.Equals(Element))
                                return UiDomBoolean.True;
                            other = other.Parent;
                        }
                        return UiDomBoolean.False;
                    case IsRelationshipType.Descendent:
                        {
                            var element = Element;
                            while (!(element.Parent is null))
                            {
                                if (element.Parent.Equals(other))
                                    return UiDomBoolean.True;
                                element = element.Parent;
                            }
                            return UiDomBoolean.False;
                        }
                    case IsRelationshipType.Sibling:
                        {
                            if (Element.Parent is null || other.Parent is null)
                            {
                                // root or defunct element
                                return UiDomBoolean.FromBool(Element.Equals(other));
                            }
                            else
                            {
                                return UiDomBoolean.FromBool(Element.Parent.Equals(other.Parent));
                            }
                        }
                }
            }
            return UiDomUndefined.Instance;
        }
    }
}
