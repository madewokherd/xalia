using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi
{
    internal class AtSpiState : UiDomValue
    {
        static readonly string[] state_names =
        {
            "invalid",
            "active",
            "armed",
            "busy",
            "checked",
            "collapsed",
            "defunct",
            "editable",
            "enabled",
            "expandable",
            "expanded",
            "focusable",
            "focused",
            "has_tooltip",
            "horizontal",
            "iconified",
            "modal",
            "multi_line",
            "multiselectable",
            "opaque",
            "pressed",
            "resizable",
            "selectable",
            "selected",
            "sensitive",
            "showing",
            "single_line",
            "stale",
            "transient",
            "vertical",
            "visible",
            "manages_descendants",
            "indeterminate",
            "required",
            "truncated",
            "animated",
            "invalid_entry",
            "supports_autocompletion",
            "selectable_text",
            "is_default",
            "visited",
            "checkable",
            "has_popup",
            "read_only",
        };

        internal static Dictionary<string, string> name_mapping;

        static AtSpiState()
        {
            name_mapping = new Dictionary<string, string>();

            foreach (string name in state_names)
            {
                if (name.Contains("_"))
                    name_mapping[name.Replace("_", "")] = name;
                name_mapping[name] = name;
            }
        }
        
        internal AtSpiState(AtSpiElement element)
        {
            Element = element;
        }

        public readonly AtSpiElement Element;

        public HashSet<string> states = new HashSet<string>();

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{Element}.spi_states [");
            bool delimiter = false;
            foreach (string state in states)
            {
                if (delimiter)
                    sb.Append("|");
                sb.Append(state);
                delimiter = true;
            }
            sb.Append("]");
            return sb.ToString();
        }

        internal bool TryEvaluateIdentifier(string id, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on,
            out UiDomBoolean result)
        {
            if (name_mapping.TryGetValue(id, out var state))
            {
                depends_on.Add((Element, new BinaryExpression(
                        new IdentifierExpression("spi_state"),
                        new IdentifierExpression(state),
                        GudlToken.Dot
                    )));
                result = UiDomBoolean.FromBool(states.Contains(state));
                return true;
            }
            result = null;
            return false;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (TryEvaluateIdentifier(id, depends_on, out var result))
                return result;
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        internal void SetStates(uint[] uint_states)
        {
            var old_states = states;
            states = new HashSet<string>();
            uint state = 0;
            foreach (uint flags in uint_states)
            {
                for (int i=0; i<32; i++)
                {
                    if ((flags & (1 << i)) != 0 && state < state_names.Length)
                    {
                        var name = state_names[state];
                        if (!old_states.Remove(name))
                            old_states.Add(name);
                        states.Add(name);
                    }
                    state++;
                }
            }
            Element.StatesChanged(old_states);
        }
    }
}
