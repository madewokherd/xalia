using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gazelle.Gudl;

namespace Gazelle.UiDom
{
    public abstract class UiDomRoot : UiDomObject
    {
        public UiDomRoot(GudlStatement[] rules)
        {
            Rules = GudlSelector.Flatten(rules).AsReadOnly();
        }

        public IReadOnlyCollection<(GudlExpression, GudlDeclaration[])> Rules { get; }
    }
}
