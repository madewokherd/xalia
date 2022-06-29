using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Uia
{
    public class UiaConnection : UiDomRoot
    {
        static int DummyElementId; // For the worst case, where we can't get any unique id for an element

        public UiaConnection(GudlStatement[] rules, IUiDomApplication app) : base(rules, app)
        {
            Automation = new UIA3Automation();
            EventThread = new UiaEventThread();
            CommandThread = new UiaCommandThread();
            DesktopElement = new UiaElement(WrapElement(Automation.GetDesktop()));
            AddChild(0, DesktopElement);

            Utils.RunTask(SetupFocusedElement());

            Utils.RunTask(SetupWindowEvents());
        }

        private async Task SetupFocusedElement()
        {
            await EventThread.RegisterFocusChangedEventAsync(DesktopElement.ElementWrapper, OnFocusChanged);

            FocusedElement = await CommandThread.GetFocusedElement(DesktopElement.ElementWrapper);
        }

        private void OnFocusChanged(UiaElementWrapper obj)
        {
            FocusedElement = obj;
        }

        private async Task SetupWindowEvents()
        {
            await EventThread.RegisterAutomationEventAsync(DesktopElement.ElementWrapper,
                Automation.EventLibrary.Window.WindowOpenedEvent, TreeScope.Element | TreeScope.Children,
                OnWindowOpened);
            await EventThread.RegisterAutomationEventAsync(DesktopElement.ElementWrapper,
                Automation.EventLibrary.Window.WindowOpenedEvent, TreeScope.Element | TreeScope.Children,
                OnWindowClosed);
            DesktopElement.UpdateChildren(); // in case children changed before the events were registered
        }

        private void OnWindowClosed(UiaElementWrapper obj)
        {
            DesktopElement.UpdateChildren();
        }

        private void OnWindowOpened(UiaElementWrapper obj)
        {
            DesktopElement.UpdateChildren();
        }

        public override string DebugId => "UiaConnection";

        public AutomationBase Automation { get; }

        public UiaElement DesktopElement { get; }

        public UiaEventThread EventThread { get; }
        
        public UiaCommandThread CommandThread { get; }

        internal Dictionary<string, UiaElement> elements_by_id = new Dictionary<string, UiaElement>();

        public UiaElement LookupAutomationElement(UiaElementWrapper ae)
        {
            if (!ae.IsValid)
                return null;
            elements_by_id.TryGetValue(ae.UniqueId, out var result);
            return result;
        }

        public UiaElementWrapper WrapElement(AutomationElement ae)
        {
            if (ae is null)
                return UiaElementWrapper.InvalidElement;
            return new UiaElementWrapper(ae, this);
        }

        UiaElementWrapper focused_element;

        public UiaElementWrapper FocusedElement
        {
            get { return focused_element; }
            private set
            {
                var old_focused_element = focused_element;

                if (old_focused_element.Equals(value))
                {
                    return;
                }

#if DEBUG
                Console.WriteLine($"Focus changed to {value.UniqueId}");
#endif

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

        internal string BlockingGetElementId(AutomationElement element)
        {
            try
            {
                if (element.FrameworkAutomationElement.TryGetPropertyValue<int[]>(
                    Automation.PropertyLibrary.Element.RuntimeId, out var runtime_id))
                {
                    var sb = new StringBuilder();
                    sb.Append("uia-runtime-id");
                    foreach (var item in runtime_id)
                        sb.Append($"-{item}");
                    return sb.ToString();
                }
            }
            catch (COMException)
            {
                // Fall back on other methods
            }

            // TODO: If automationid is provided, use automationid + parent id

            // TODO: If hwnd differs from parent, use hwnd + parent id

            // TOOD: Query for IAccessible2 directly using service id IAccessible, for old Qt versions

            // TODO: Use NAV_PREVIOUS if supported to find index in parent, and use it for comparison.
            // This requires saving the element along with the parent, because the index may change.

            // TODO: If NAV_PREVIOUS is not supported, use every MSAA property we can find for comparison.

            var id = Interlocked.Increment(ref DummyElementId);
            return $"incomparable-element-{id}";
        }
    }
}
