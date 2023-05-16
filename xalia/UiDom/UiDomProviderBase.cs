using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    internal class UiDomProviderBase : IUiDomProvider
    {
        public virtual void DumpProperties(UiDomElement element)
        {
        }

        public virtual UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return UiDomUndefined.Instance;
        }

        public virtual UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return UiDomUndefined.Instance;
        }

        public virtual Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public virtual string[] GetTrackedProperties()
        {
            return null;
        }

        public virtual void NotifyElementRemoved(UiDomElement element)
        {
        }

        public virtual void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
        }

        public virtual bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public virtual bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }
    }
}
