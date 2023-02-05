using Superpower.Model;
using System;

namespace Xalia.Gudl
{
    public class GudlDeclaration : GudlStatement
    {
        public GudlDeclaration(GudlExpression property, GudlExpression value, Superpower.Model.Position position)
        {
            if (property is IdentifierExpression id)
                Property = id.Name;
            else if (property is StringExpression st)
                Property = st.Value;
            else
                throw new ArgumentException("property must be an IdentifierExpression or StringExpression");
            Value = value;
            Position = position;
        }

        public string Property;
        public GudlExpression Value;
        public Position Position;
    }
}
