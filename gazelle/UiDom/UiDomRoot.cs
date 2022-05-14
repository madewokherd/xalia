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

        public event EventHandler<UiDomObject> ElementDeclarationsChangedEvent;

        public event EventHandler<UiDomObject> ElementDiedEvent;

        internal void RaiseElementDeclarationsChangedEvent(UiDomObject element)
        {
            EventHandler<UiDomObject> elementDeclarationsChangedEvent = ElementDeclarationsChangedEvent;

            if (elementDeclarationsChangedEvent != null)
            {
                elementDeclarationsChangedEvent(this, element);
            }
        }

        internal void RaiseElementDiedEvent(UiDomObject element)
        {
            EventHandler<UiDomObject> elementDiedEvent = ElementDiedEvent;

            if (elementDiedEvent != null)
            {
                elementDiedEvent(this, element);
            }
        }
    }
}
