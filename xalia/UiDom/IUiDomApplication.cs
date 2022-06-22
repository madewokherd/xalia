using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Xalia.Gudl;

namespace Xalia.UiDom
{
    public interface IUiDomApplication
    {
        void RootElementCreated(UiDomRoot root);

        void ElementDeclarationsChanged(UiDomElement element);

        void ElementDied(UiDomElement element);

        UiDomValue EvaluateIdentifierHook(UiDomElement element, string id, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on);
    }
}
