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

        private UiDomObject _targetedElement;
        public UiDomObject TargetedElement
        {
            get
            {
                return _targetedElement;
            }
            set
            {
                if (_targetedElement != value)
                {
                    var previous = TargetedElement;
                    _targetedElement = value;

                    PropertyChanged("targeted_element");
                    if (!(_targetedElement is null))
                        _targetedElement.PropertyChanged("targeted");
                    if (!(previous is null))
                        previous.PropertyChanged("targeted");
                    Application.TargetChanged(previous);
                }
            }
        }

        internal void RaiseElementDeclarationsChangedEvent(UiDomObject element)
        {
            Application.ElementDeclarationsChanged(element);
        }

        internal void RaiseElementDiedEvent(UiDomObject element)
        {
            Application.ElementDied(element);
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "targeted_element":
                    depends_on.Add((this, new IdentifierExpression("targeted_element")));
                    if (TargetedElement is null)
                        return UiDomUndefined.Instance;
                    return TargetedElement;
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
