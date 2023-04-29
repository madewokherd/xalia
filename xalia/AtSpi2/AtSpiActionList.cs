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
        public AtSpiActionList(ActionProvider provider)
        {
            Provider = provider;
        }

        public ActionProvider Provider { get; }

        public UiDomElement Element => Provider.Element;

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
            return $"{Element}.spi_action [{string.Join(",",Provider.Actions)}]";
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            for (int i=0; i < Provider.Actions.Length; i++)
            {
                if (Provider.Actions[i] == id)
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
            var element = obj.Element;
            var index = ((UiDomInt)obj.Arglist[0]).Value;
            return element.ProviderByType<ActionProvider>().DoAction(index);
        }

        private static Task HandleAction(UiDomRoutineAsync obj)
        {
            string id = obj.Name.Substring(11);
            var element = obj.Element;
            var provider = element.ProviderByType<ActionProvider>();
            for (int i=0; i<provider.Actions.Length; i++)
            {
                if (provider.Actions[i] == id)
                    return provider.DoAction(i);
            }
            return Task.CompletedTask;
        }
    }
}