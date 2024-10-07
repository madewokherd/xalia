using System.Threading.Tasks;

namespace Xalia.UiDom
{
    internal interface IUiDomScrollToProvider : IUiDomProvider
    {
        Task<bool> ScrollToAsync();
    }
}
