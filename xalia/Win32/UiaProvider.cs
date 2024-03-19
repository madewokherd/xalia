using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class UiaProvider : UiDomProviderBase
    {
        public UiaProvider(HwndProvider root_hwnd, UiDomElement element, IRawElementProviderSimple prov)
        {
            RootHwnd = root_hwnd;
            Element = element;
            Provider = prov;
        }

        public HwndProvider RootHwnd { get; }
        public UiDomElement Element { get; }
        public Win32Connection Connection => RootHwnd.Connection;
        public int Tid => RootHwnd.Tid;
        public CommandThread CommandThread => RootHwnd.CommandThread;

        public IRawElementProviderSimple Provider { get; }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_uia_element":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }
    }
}