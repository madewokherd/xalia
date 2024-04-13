using System.Text;

namespace Xalia.Gudl
{
    internal class ApplyExpression : GudlExpression
    {
        public ApplyExpression(GudlExpression left, GudlExpression[] arglist)
        {
            Left = left;
            Arglist = arglist;
        }

        public GudlExpression Left { get; }
        public GudlExpression[] Arglist { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is ApplyExpression apply)
            {
                if (Arglist.Length != apply.Arglist.Length)
                    return false;
                if (!Left.Equals(apply.Left))
                    return false;
                for (int i = 0; i < Arglist.Length; i++)
                {
                    if (!Arglist[i].Equals(apply.Arglist[i]))
                        return false;
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int result = typeof(ApplyExpression).GetHashCode() ^ Left.GetHashCode();
            foreach (var arg in Arglist)
            {
                result = (result, arg).GetHashCode();
            }
            return result;
        }

        internal override string ToString(out GudlPrecedence precedence)
        {
            var result = new StringBuilder();
            var left_str = Left.ToString(out var left_precedence);
            if (left_precedence < GudlPrecedence.Apply)
                result.Append("(");
            result.Append(left_str);
            if (left_precedence < GudlPrecedence.Apply)
                result.Append(")");
            result.Append("(");
            for (int i=0; i < Arglist.Length; i++)
            {
                result.Append(Arglist[i]);
                if (i < Arglist.Length - 1)
                    result.Append(", ");
            }
            result.Append(")");
            precedence = GudlPrecedence.Apply;
            return result.ToString();
        }
    }
}
