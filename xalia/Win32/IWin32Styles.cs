using System.Collections.Generic;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal interface IWin32Styles : IUiDomProvider
    {
        // Adds the names of class-specific styles to names
        void GetStyleNames(int style, List<string> names);
    }
}
