using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Xalia.Gudl;

namespace Xalia.UiDom
{
    public abstract class UiDomRoot : UiDomObject
    {
        public UiDomRoot(GudlStatement[] rules, IUiDomApplication application)
        {
            Rules = GudlSelector.Flatten(rules).AsReadOnly();
            Application = application;
            Application.RootElementCreated(this);
        }

        public IReadOnlyCollection<(GudlExpression, GudlDeclaration[])> Rules { get; }

        public IUiDomApplication Application { get; }

        internal void RaiseElementDeclarationsChangedEvent(UiDomObject element)
        {
            Application.ElementDeclarationsChanged(element);
        }

        internal void RaiseElementDiedEvent(UiDomObject element)
        {
            Application.ElementDied(element);
        }
    }
}
