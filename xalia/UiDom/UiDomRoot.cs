using System.Collections.Generic;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    public class UiDomRoot : UiDomElement
    {
        public UiDomRoot(GudlStatement[] rules, IUiDomApplication application)
        {
            Rules = GudlSelector.Flatten(rules).AsReadOnly();
            Application = application;
            Application.RootElementCreated(this);
            AddGlobalProvider(new VirtualChildrenProvider());
        }

        public IReadOnlyCollection<(GudlExpression, GudlDeclaration[])> Rules { get; }

        public IUiDomApplication Application { get; }

        public List<IUiDomProvider> GlobalProviders { get; private set; } = new List<IUiDomProvider>();

        internal void RaiseElementDeclarationsChangedEvent(UiDomElement element)
        {
            Application.ElementDeclarationsChanged(element);
        }

        internal void RaiseElementDiedEvent(UiDomElement element)
        {
            Application.ElementDied(element);
        }

        public void AddGlobalProvider(IUiDomProvider provider, int index)
        {
            GlobalProviders.Insert(index, provider);
            AddedGlobalProvider(provider);
        }

        public void AddGlobalProvider(IUiDomProvider provider)
        {
            AddGlobalProvider(provider, GlobalProviders.Count);
        }
    }
}
