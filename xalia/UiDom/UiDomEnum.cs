using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    internal class UiDomEnum : UiDomValue
    {
        public UiDomEnum(string[] names)
        {
            Names = names;
        }

        public string[] Names { get; }

        public override bool Equals(object obj)
        {
            if (obj is UiDomEnum en)
            {
                if (Names.Length != en.Names.Length)
                    return false;
                foreach (string name in Names)
                {
                    if (!en.Names.Contains(name))
                        return false;
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            var result = typeof(UiDomEnum).GetHashCode();
            foreach (string name in Names)
                result ^= name.GetHashCode();
            return result;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "name":
                    return new UiDomString(Names[0]);
            }
            return UiDomBoolean.FromBool(Names.Contains(id));
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("enum(\"");
            sb.Append(Names[0]);
            sb.Append('"');
            for (int i = 1; i < Names.Length; i++ )
            {
                sb.Append(", \"");
                sb.Append(Names[i]);
                sb.Append('"');
            }
            sb.Append(')');
            return sb.ToString();
        }
    }
}
