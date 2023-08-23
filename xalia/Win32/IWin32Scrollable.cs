using Xalia.UiDom;

namespace Xalia.Win32
{
    internal interface IWin32Scrollable : IUiDomProvider
    {
        IUiDomProvider GetScrollBarProvider(NonclientScrollProvider nonclient);
    }
}
