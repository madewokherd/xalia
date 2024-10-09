using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi2
{
    internal class AtSpiAttributes : UiDomValue
    {
        public AtSpiAttributes(Dictionary<string,string> attributes) : base()
        {
            Attributes = attributes;
        }

        public Dictionary<string, string> Attributes { get; }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (Attributes.TryGetValue(id, out var result))
            {
                return new UiDomString(result);
            }
            if (id.Contains("_") && Attributes.TryGetValue(id.Replace("_","-"), out result))
            {
                return new UiDomString(result);
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is AtSpiAttributes a)
            {
                return Utils.DictionariesEqual(a.Attributes, Attributes);
            }
            return false;
        }

        public override int GetHashCode()
        {
            int result = Attributes.Count ^ typeof(AtSpiAttributes).GetHashCode();
            foreach (var kvp in Attributes)
            {
                result ^= kvp.Key.GetHashCode();
                result ^= kvp.Value.GetHashCode();
            }
            return result;
        }

        public override string ToString()
        {
            return $"spi_attributes [{Attributes.Count.ToString(CultureInfo.InvariantCulture)}]";
        }
    }
}
