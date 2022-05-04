using Gazelle.Gudl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Gazelle.UiDom
{
    public class UiDomUndefined : UiDomValue
    {
        private UiDomUndefined()
        {

        }

        public static UiDomUndefined Instance = new UiDomUndefined();

        public override string ToString()
        {
            return "undefined";
        }

        public override bool ToBool()
        {
            return false;
        }
    }
}
