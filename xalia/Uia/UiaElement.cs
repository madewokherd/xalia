using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;

using Xalia.UiDom;

namespace Xalia.Uia
{
    internal class UiaElement : UiDomObject
    {
        static long NextDebugId;

        public UiaElement(AutomationElement element, UiaConnection root) : base(root)
        {
            AutomationElement = element;
            // There doesn't seem to be any reliable non-blocking away to get an element id, so we make one up
            long debug_id = Interlocked.Increment(ref NextDebugId);
            DebugId = $"UIA:{debug_id}";
        }

        public AutomationElement AutomationElement { get; }

        public override string DebugId { get; }
    }
}
