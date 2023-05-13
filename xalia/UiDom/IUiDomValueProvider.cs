using System.Threading.Tasks;

namespace Xalia.UiDom
{
    internal interface IUiDomValueProvider : IUiDomProvider
    {
        Task<double> GetMinimumIncrementAsync(UiDomElement element);

        Task<bool> OffsetValueAsync(UiDomElement element, double offset);
    }
}
