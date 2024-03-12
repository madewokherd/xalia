using Xalia.UiDom;

namespace Xalia.Win32
{
    internal interface IWin32ScrollChange : IUiDomProvider
    {
        void MsaaScrolled(int which);
    }
}