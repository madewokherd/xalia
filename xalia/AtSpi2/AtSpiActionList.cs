using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi2
{
    internal class AtSpiActionList : UiDomValue
    {
        public AtSpiElement Element { get; private set; }

        public AtSpiActionList(AtSpiElement element)
        {
            Element = element;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is AtSpiActionList l)
            {
                return l.Element.Equals(Element);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(AtSpiActionList).GetHashCode() ^ Element.GetHashCode();
        }

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
                        Element, $"spi_action.{id}", HandleAction);
                }
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression[] arglist, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 1)
                return UiDomUndefined.Instance;
            var expr = arglist[0];
            UiDomValue right = context.Evaluate(expr, root, depends_on);
            if (right is UiDomString st)
            {
                return EvaluateIdentifier(st.Value, root, depends_on);
            }
            if (right is UiDomInt i)
            {
                return new UiDomRoutineAsync(Element, "spi_action", new UiDomValue[] { right }, HandleApply);
            }
            return UiDomUndefined.Instance;
        }

        private static Task HandleApply(UiDomRoutineAsync obj)
        {
            var element = (AtSpiElement)obj.Element;
            var index = ((UiDomInt)obj.Arglist[0]).Value;
            return element.DoAction(index);
        }

        private static Task HandleAction(UiDomRoutineAsync obj)
        {
            string id = obj.Name.Substring(11);
            var element = (AtSpiElement)obj.Element;
            for (int i=0; i<element.Actions.Length; i++)
            {
                if (element.Actions[i] == id)
                    return element.DoAction(i);
            }
            return Task.CompletedTask;
        }
    }
}