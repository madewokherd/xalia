using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gazelle.Winforms
{
    internal class WindowingSystem
    {
        public virtual OverlayBox CreateOverlayBox()
        {
            return new OverlayBox();
        }
    }
}
