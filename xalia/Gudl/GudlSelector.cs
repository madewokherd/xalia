using System;
using System.Collections.Generic;

namespace Xalia.Gudl
{
    public class GudlSelector : GudlStatement
    {
        static GudlSelector()
        {
            relationship_conditions = new Dictionary<string, string>();
            relationship_conditions["parent"] = "child_matches";
            relationship_conditions["ancestor"] = "desendent_matches";
            relationship_conditions["child"] = "parent_matches";
            relationship_conditions["desdendent"] = "ancestor_matches";
            relationship_conditions["previous_sibling"] = "next_sibling_matches";
            relationship_conditions["next_sibling"] = "previous_sibling_matches";
        }

        public GudlSelector(GudlExpression kind, GudlExpression condition, GudlStatement[] statements)
        {
            if (kind is IdentifierExpression id)
                Kind = id.Name;
            else if (kind is StringExpression st)
                Kind = st.Value;
            else
                throw new ArgumentException("kind must be an IdentifierExpression or StringExpression");
            Condition = condition;
            Statements = statements;
        }

        static Dictionary<string, string> relationship_conditions;

        public string Kind;
        public GudlExpression Condition;
        public GudlStatement[] Statements;

        private GudlExpression And(GudlExpression left, GudlExpression right)
        {
            if (left is null)
                return right;
            else if (right is null)
                return left;
            else
                return new BinaryExpression(left, right, GudlToken.And);
        }

        private void FlattenRecursive(GudlExpression parent_condition, List<(GudlExpression, GudlDeclaration[])> items)
        {
            GudlExpression condition = Condition;
            if (!(Kind is null) && Kind != "if")
            {
                if (relationship_conditions.ContainsKey(Kind))
                {
                    condition = And(
                        new BinaryExpression(new IdentifierExpression(relationship_conditions[Kind]),
                            parent_condition ?? new IdentifierExpression("true"),
                            GudlToken.LParen),
                        condition);
                }
                else
                {
                    condition = And(
                        And(new IdentifierExpression(Kind), parent_condition),
                        condition);
                }
            }
            else
            {
                condition = And(parent_condition, condition);
            }

            List<GudlDeclaration> declarations = new List<GudlDeclaration>();

            foreach (var statement in Statements)
            {
                if (statement is GudlDeclaration declaration)
                {
                    declarations.Add(declaration);
                }
                else
                {
                    if (declarations.Count != 0)
                        items.Add((condition, declarations.ToArray()));
                    declarations.Clear();
                    GudlSelector selector = (GudlSelector)statement;
                    selector.FlattenRecursive(condition, items);
                }
            }

            if (declarations.Count != 0)
                items.Add((condition, declarations.ToArray()));
        }

        public List<(GudlExpression, GudlDeclaration[])> Flatten()
        {
            var result = new List<(GudlExpression, GudlDeclaration[])>();
            FlattenRecursive(null, result);
            return result;
        }

        public static List<(GudlExpression, GudlDeclaration[])> Flatten(GudlStatement[] statements)
        {
            var items = new List<(GudlExpression, GudlDeclaration[])>();

            List<GudlDeclaration> declarations = new List<GudlDeclaration>();

            foreach (var statement in statements)
            {
                if (statement is GudlDeclaration declaration)
                {
                    declarations.Add(declaration);
                }
                else
                {
                    if (declarations.Count != 0)
                        items.Add((null, declarations.ToArray()));
                    declarations.Clear();
                    GudlSelector selector = (GudlSelector)statement;
                    selector.FlattenRecursive(null, items);
                }
            }

            if (declarations.Count != 0)
                items.Add((null, declarations.ToArray()));

            return items;
        }
    }
}
