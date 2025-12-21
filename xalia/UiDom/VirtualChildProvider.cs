using System.Collections.Generic;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    internal class VirtualChildProvider : UiDomProviderBase
    {
        public VirtualChildProvider(int index)
        {
            Index = index;
        }

        public int Index { get; }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (identifier == "virtual_child_index")
                return new UiDomInt(Index);
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }
    }
}
