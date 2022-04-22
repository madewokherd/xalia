using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gazelle.UiDom;

namespace Gazelle.AtSpi
{
    internal class AtSpiObject : UiDomObject
    {
        internal readonly string Path;
        internal override string DebugId => Path;

        internal AtSpiObject(string path)
        {
            Path = path;
        }
    }
}
