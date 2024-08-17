using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
                return Names[0] == en.Names[0];
            return false;
        }

        public override int GetHashCode()
        {
            return Names[0].GetHashCode() ^ typeof(UiDomEnum).GetHashCode();
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
            return $"<enum '{Names[0]}'>";
        }
    }
}
