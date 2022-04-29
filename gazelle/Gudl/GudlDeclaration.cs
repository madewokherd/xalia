using System;

namespace Gazelle.Gudl
{
    public class GudlDeclaration : GudlStatement
    {
        public GudlDeclaration(GudlExpression property, GudlExpression value)
        {
            if (property is IdentifierExpression id)
                Property = id.Name;
            else if (property is StringExpression st)
                Property = st.Value;
            else
                throw new ArgumentException("property must be an IdentifierExpression or StringExpression");
            Value = value;
        }

        public string Property;
        public GudlExpression Value;
    }
}
