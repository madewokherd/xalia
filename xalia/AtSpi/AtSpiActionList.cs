using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Tmds.DBus;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi
{
    internal class AtSpiActionList : UiDomValue
    {
        public AtSpiActionList(AtSpiElement element)
        {
            Element = element;
        }

        public AtSpiElement Element { get; }

        public override string ToString()
        {
            return $"{Element}.spi_action [{string.Join(",",Element.Actions)}]";
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            for (int i=0; i < Element.Actions.Length; i++)
            {
                if (Element.Actions[i] == id)
                {
                    return new UiDomRoutineAsync(
                        Element, $"spi_action.{id}",
                        async (UiDomRoutineAsync obj) =>
                        {
                            try
                            {
                                await Element.action.DoActionAsync(i);
                            }
                            catch (DBusException e)
                            {
                                if (!AtSpiElement.IsExpectedException(e))
                                    throw;
                                return;
                            }
                        });
                }
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
