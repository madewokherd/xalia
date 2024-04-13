namespace Xalia.Gudl
{
    public abstract class GudlExpression
    {
        internal abstract string ToString(out GudlPrecedence precedence);

        public override string ToString()
        {
            return ToString(out GudlPrecedence _precedence);
        }
    }
}
