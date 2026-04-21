using System.Collections.Generic;
using System.Runtime.InteropServices;

using Xalia.Gudl;

namespace Xalia.UiDom
{
    public interface IReleaseChildren : IUiDomProvider
    {
        // Release any children that no longer belong to this element.
        // Alternatively, if this can't be completed without blocking, child may be removed with no checking.
        void ReleaseChildren(UiDomElement child);
    }
}
