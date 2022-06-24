using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

            Utils.RunTask(SetupFocusedElement());
        }

        private async Task SetupFocusedElement()
        {
            await EventThread.RegisterFocusChangedEventAsync(Automation, OnFocusChanged);

            FocusedElement = await CommandThread.GetFocusedElement(Automation);
        }

        private void OnFocusChanged(AutomationElement obj)
        {
            Console.WriteLine("OnFocusChanged");
            FocusedElement = obj;
        }

        public override string DebugId => "UiaConnection";

        public AutomationBase Automation { get; }

        public UiaElement DesktopElement { get; }

        public UiaEventThread EventThread { get; }
        
        public UiaCommandThread CommandThread { get; }

        internal Dictionary<AutomationElement, UiaElement> elements_by_uia = new Dictionary<AutomationElement, UiaElement>();

        public UiaElement LookupAutomationElement(AutomationElement ae)
        {
            if (ae is null)
                return null;
            elements_by_uia.TryGetValue(ae, out var result);
            return result;
        }

        AutomationElement focused_element;

        public AutomationElement FocusedElement
        {
            get { return focused_element; }
            private set
            {
                var old_focused_element = focused_element;

                focused_element = value;

                PropertyChanged("uia_focused_element");

                if (LookupAutomationElement(old_focused_element) is UiaElement old)
                {
                    old.PropertyChanged("uia_focused");
                }

                if (LookupAutomationElement(value) is UiaElement new_focused)
                {
                    new_focused.PropertyChanged("uia_focused");
                }
            }
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "uia_focused_element":
                case "focused_element":
                    depends_on.Add((Root, new IdentifierExpression("uia_focused_element")));
                    return (UiDomValue)LookupAutomationElement(FocusedElement) ?? UiDomUndefined.Instance;
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
