using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal class HwndMsaaChildProvider : UiDomProviderBase
    {
        internal HwndMsaaChildProvider(UiDomElement element, HwndProvider hwndRoot, int childId) : base()
        {
            Element = element;
            HwndRoot = hwndRoot;
            ChildId = childId;

            // TODO: If HwndRoot has an AccessibleProvider, use it to get an AccessibleProvider
            // for this child.
        }

        public UiDomElement Element { get; }
        public HwndProvider HwndRoot { get; }
        public int ChildId { get; }

        public override void DumpProperties(UiDomElement element)
        {
            Console.WriteLine($"  msaa_child_id: {ChildId}");
            HwndRoot.ChildDumpProperties();
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (identifier == "msaa_child_id")
                return new UiDomInt(ChildId);
            return HwndRoot.ChildEvaluateIdentifier(identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (identifier == "child_id")
                return new UiDomInt(ChildId);
            return HwndRoot.ChildEvaluateIdentifierLate(identifier, depends_on);
        }
    }
}