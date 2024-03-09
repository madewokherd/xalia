using Xalia.UiDom;

namespace Xalia.Win32
{
    internal interface IWin32Container : IUiDomProvider
    {
        void MsaaChildCreated(int ChildId);
        void MsaaChildDestroyed(int ChildId);
        void MsaaChildrenReordered();

        UiDomElement GetMsaaChild(int ChildId);
    }
}