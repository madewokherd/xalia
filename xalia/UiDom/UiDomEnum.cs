using Xalia.Gudl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            return UiDomBoolean.FromBool(Names.Contains(id));
        }

        public override string ToString()
        {
            return $"<enum '{Names[0]}'>";
        }
    }
}
