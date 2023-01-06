using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    internal class UiDomDoAction : UiDomValue
    {
        private UiDomDoAction()
        {
        }

        public static UiDomDoAction Instance { get; } = new UiDomDoAction();

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return new UiDomDoActionRoutine(id);
        }

        public override string ToString()
        {
            return "do_action";
        }
    }
}
