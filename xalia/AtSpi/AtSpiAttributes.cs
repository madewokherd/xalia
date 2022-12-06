using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi
{
    internal class AtSpiAttributes : UiDomValue
    {
        public AtSpiAttributes(AtSpiElement element)
        {
            Element = element;
        }

        public AtSpiElement Element { get; }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (Element.Attributes.TryGetValue(id, out var result))
            {
                return new UiDomString(result);
            }
            if (id.Contains("_") && Element.Attributes.TryGetValue(id.Replace("_","-"), out result))
            {
                return new UiDomString(result);
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is AtSpiAttributes attr)
                return Element.Equals(attr.Element);
            return false;
        }

        public override int GetHashCode()
        {
            return Element.GetHashCode() ^ typeof(AtSpiAttributes).GetHashCode();
        }

        public override string ToString()
        {
            return $"{Element}.spi_attributes";
        }
    }
}
