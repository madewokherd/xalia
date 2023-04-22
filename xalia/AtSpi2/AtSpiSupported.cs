using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi2
{
    internal class AtSpiSupported : UiDomValue
    {
        public AtSpiSupported(string[] interfaces)
        {
            Interfaces = interfaces;
        }

        public string[] Interfaces { get; }

        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append($"spi_supported[");
            bool delimiter = false;
            foreach (string iface in Interfaces)
            {
                if (delimiter)
                    result.Append("|");
                String name;
                if (iface.StartsWith("org.a11y.atspi."))
                    name = ToSnakeCase(iface.Substring(15));
                else
                    name = iface;
                result.Append(name);
                delimiter = true;
            }
            result.Append("]");
            return result.ToString();
        }

        static string ToCamelCase(string input)
        {
            var sb = new StringBuilder();

            bool capitalize = true;

            foreach (char c in input)
            {
                if (c == '_')
                {
                    capitalize = true;
                    continue;
                }

                if (capitalize)
                {
                    sb.Append(char.ToUpper(c));
                    capitalize = false;
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        static string ToSnakeCase(string input)
        {
            var sb = new StringBuilder();

            bool at_start = true;

            foreach (char c in input)
            {
                if (at_start)
                {
                    sb.Append(char.ToLower(c));
                    at_start = false;
                    continue;
                }

                if (char.IsUpper(c))
                {
                    sb.Append("_");
                    sb.Append(char.ToLower(c));
                    continue;
                }

                sb.Append(c);
                at_start = false;
            }

            return sb.ToString();
        }
        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            string iface_name;

            if (id.Contains("."))
            {
                iface_name = id;
            }
            else
            {
                iface_name = "org.a11y.atspi." + ToCamelCase(id);
            }

            return UiDomBoolean.FromBool(Interfaces.Contains(iface_name));
        }
    }
}
