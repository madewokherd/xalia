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

        void ElementDeclarationsChanged(UiDomObject element);

        void ElementDied(UiDomObject element);

        void TargetChanged(UiDomObject previous_target);

        UiDomValue EvaluateIdentifierHook(UiDomObject element, string id, [In, Out] HashSet<(UiDomObject, GudlExpression)> depends_on);
    }
}
