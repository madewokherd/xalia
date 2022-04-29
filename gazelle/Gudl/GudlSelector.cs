using System;

namespace Gazelle.Gudl
{
    public class GudlSelector : GudlStatement
    {
        public GudlSelector(GudlExpression kind, GudlExpression condition, GudlStatement[] statements, GudlSelector @else)
        {
            if (kind is IdentifierExpression id)
                Kind = id.Name;
            else if (kind is StringExpression st)
                Kind = st.Value;
            else
                throw new ArgumentException("kind must be an IdentifierExpression or StringExpression");
            Condition = condition;
            Statements = statements;
            Else = @else;
        }

        public string Kind;
        public GudlExpression Condition;
        public GudlStatement[] Statements;
        public GudlSelector Else;
    }
}
