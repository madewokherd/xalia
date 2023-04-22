using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    public interface IUiDomProvider
    {
        void NotifyElementRemoved(UiDomElement element);

        // returns True if handled
        bool WatchProperty(UiDomElement element, GudlExpression expression);

        bool UnwatchProperty(UiDomElement element, GudlExpression expression);

        UiDomValue EvaluateIdentifier(UiDomElement element, string identifier,
            HashSet<(UiDomElement, GudlExpression)> depends_on);

        UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier,
            HashSet<(UiDomElement, GudlExpression)> depends_on);

        void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value);

        void DumpProperties(UiDomElement element);

        Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element);

        string[] GetTrackedProperties();
    }
}
