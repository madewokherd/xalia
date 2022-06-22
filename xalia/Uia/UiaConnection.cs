using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Uia
{
    internal class UiaConnection : UiDomRoot
    {
        public UiaConnection(GudlStatement[] rules, IUiDomApplication app) : base(rules, app)
        {
            Automation = new UIA3Automation();
            EventThread = new UiaEventThread();
            CommandThread = new UiaCommandThread();
            DesktopElement = new UiaElement(Automation.GetDesktop(), this);
            AddChild(0, DesktopElement);
        }

        public override string DebugId => "UiaConnection";

        public AutomationBase Automation { get; }

        public UiaElement DesktopElement { get; }

        public UiaEventThread EventThread { get; }
        
        public UiaCommandThread CommandThread { get; }
    }
}
