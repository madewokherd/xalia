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
using FlaUI.Core.Exceptions;
using FlaUI.Core.Identifiers;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Uia
{
    public class UiaConnection : UiDomRoot
    {
        static int DummyElementId; // For the worst case, where we can't get any unique id for an element

        static List<WINEVENTPROC> event_proc_delegates = new List<WINEVENTPROC>(); // to make sure delegates aren't GC'd while in use

        internal Dictionary<string, PropertyId> names_to_property = new Dictionary<string, PropertyId>();
        internal Dictionary<PropertyId, string> properties_to_name = new Dictionary<PropertyId, string>();

        public UiaConnection(bool use_uia3, GudlStatement[] rules, IUiDomApplication app) : base(rules, app)
        {
            MainContext = SynchronizationContext.Current;
            CommandThread = new UiaCommandThread();
            Utils.RunTask(InitUia(use_uia3));
        }

        private async Task InitUia(bool use_uia3)
        {
            await CommandThread.OnBackgroundThread(() =>
            {
                if (use_uia3)
                    Automation = new FlaUI.UIA3.UIA3Automation();
                else
                    Automation = new FlaUI.UIA2.UIA2Automation();
            });

            // If a top-level window does not support WindowPattern, we don't get
            // any notification from UIAutomation when it's created.
            var eventprocdelegate = new WINEVENTPROC(OnMsaaEvent);

            event_proc_delegates.Add(eventprocdelegate);

            SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_DESTROY, IntPtr.Zero,
                eventprocdelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            RegisterPropertyMapping("uia_control_type", Automation.PropertyLibrary.Element.ControlType);
            RegisterPropertyMapping("uia_enabled", Automation.PropertyLibrary.Element.IsEnabled);
            RegisterPropertyMapping("uia_bounding_rectangle", Automation.PropertyLibrary.Element.BoundingRectangle);

            await CommandThread.OnBackgroundThread(() =>
            {
                var DesktopElement = Automation.GetDesktop();

                Automation.RegisterFocusChangedEvent(OnFocusChangedBackground);

                DesktopElement.RegisterStructureChangedEvent(
                    TreeScope.Element | TreeScope.Descendants, OnStructureChangedBackground);

                DesktopElement.RegisterPropertyChangedEvent(
                    TreeScope.Element | TreeScope.Descendants, OnPropertyChangedBackground,
                    properties_to_name.Keys.ToArray());

                DesktopElement.RegisterAutomationEvent(
                    Automation.EventLibrary.Window.WindowOpenedEvent, TreeScope.Element | TreeScope.Descendants,
                    OnWindowOpenedBackground);

                DesktopElement.RegisterAutomationEvent(
                    Automation.EventLibrary.Window.WindowOpenedEvent, TreeScope.Element | TreeScope.Descendants,
                    OnWindowClosedBackground);
            });

            Utils.RunTask(UpdateFocusedElement());

            UpdateChildren(); // in case children changed before the events were registered
        }

        HashSet<IntPtr> visible_toplevel_hwnds = new HashSet<IntPtr>();

        Dictionary<IntPtr, UiaElement> toplevels_by_hwnd = new Dictionary<IntPtr, UiaElement>();

        internal bool IsUiaToplevel(IntPtr hwnd)
        {
            if (!WindowIsVisible(hwnd))
                return false;

            if (GetAncestor(hwnd, GA_PARENT) != GetDesktopWindow())
                return false;

            var owner_window = GetWindow(hwnd, GW_OWNER);

            if (owner_window != IntPtr.Zero && WindowIsVisible(owner_window))
                return false;

            return true;
        }

        internal void UpdateChildren()
        {
            var missing_hwnds = new HashSet<IntPtr>(visible_toplevel_hwnds);

            foreach (var hwnd in EnumWindows())
            {
                if (!IsUiaToplevel(hwnd))
                    continue;

                if (missing_hwnds.Contains(hwnd))
                {
                    missing_hwnds.Remove(hwnd);
                }
                else
                {
                    visible_toplevel_hwnds.Add(hwnd);
                    Utils.RunTask(AddToplevelHwnd(hwnd));
                }
            }

            foreach (var hwnd in missing_hwnds)
            {
                visible_toplevel_hwnds.Remove(hwnd);
                if (toplevels_by_hwnd.TryGetValue(hwnd, out var element))
                {
                    toplevels_by_hwnd.Remove(hwnd);
                    RemoveChild(Children.IndexOf(element));
                }
            }
        }

        internal Task<UiaElementWrapper> WrapperFromHwnd(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || hwnd == GetDesktopWindow() || !WindowIsVisible(hwnd))
                return Task.FromResult(UiaElementWrapper.InvalidElement);

            GetWindowThreadProcessId(hwnd, out int pid);

            return CommandThread.OnBackgroundThread(() =>
            {
                try
                {
                    var element = Automation.FromHandle(hwnd);

                    return WrapElement(element);
                }
                catch (COMException)
                {
                    return UiaElementWrapper.InvalidElement;
                }
                catch (ElementNotAvailableException)
                {
                    return UiaElementWrapper.InvalidElement;
                }
            }, pid);
        }
        internal Task<UiaElementWrapper> WrapperFromHwnd(IntPtr hwnd, int objectId, int childId)
        {
            if ((objectId == OBJID_WINDOW || objectId == OBJID_CLIENT) && childId == CHILDID_SELF)
            {
                return WrapperFromHwnd(hwnd);
            }

            if (!(Automation is FlaUI.UIA3.UIA3Automation))
            {
                // Hopefully we don't need to handle UIA2 because it translates the events for us.
                return Task.FromResult(UiaElementWrapper.InvalidElement);
            }

            if (hwnd == IntPtr.Zero || hwnd == GetDesktopWindow() || !WindowIsVisible(hwnd))
                return Task.FromResult(UiaElementWrapper.InvalidElement);

            GetWindowThreadProcessId(hwnd, out int pid);

            return CommandThread.OnBackgroundThread(() =>
            {
                try
                {
                    int hr = AccessibleObjectFromEvent(hwnd, objectId, childId, out var acc, out var res_childid);

                    Marshal.ThrowExceptionForHR(hr);

                    var au3 = (FlaUI.UIA3.UIA3Automation)Automation;

                    var native = au3.NativeAutomation.ElementFromIAccessible(acc, (int)res_childid);

                    var element = au3.WrapNativeElement(native);

                    return WrapElement(element);
                }
                catch (COMException)
                {
                    return UiaElementWrapper.InvalidElement;
                }
                catch (ElementNotAvailableException)
                {
                    return UiaElementWrapper.InvalidElement;
                }
            }, pid);
        }

        internal async Task AddToplevelHwnd(IntPtr hwnd)
        {
            UiaElementWrapper wrapper = await WrapperFromHwnd(hwnd);

            if (!visible_toplevel_hwnds.Contains(hwnd) || toplevels_by_hwnd.ContainsKey(hwnd) ||
                !wrapper.IsValid)
                return;

            AddChild(Children.Count, new UiaElement(wrapper));
        }

        private void OnPropertyChangedBackground(AutomationElement arg1, PropertyId arg2, object arg3)
        {
            var wrapper = WrapElement(arg1);
            MainContext.Post((state) =>
            {
                var element = LookupAutomationElement(wrapper);
                if (!(element is null) && properties_to_name.TryGetValue(arg2, out var name))
                    element.OnPropertyChange(name, arg2, arg3);
            }, null);
        }

        private void RegisterPropertyMapping(string name, PropertyId propid)
        {
            names_to_property[name] = propid;
            properties_to_name[propid] = name;
        }

        private void OnStructureChangedBackground(AutomationElement arg1, StructureChangeType arg2, int[] arg3)
        {
            UiaElementWrapper wrapper;
            try
            {
                if (arg2 == StructureChangeType.ChildAdded)
                    wrapper = WrapElement(arg1.Parent);
                else
                    wrapper = WrapElement(arg1);
            }
            catch (COMException)
            {
                return;
            }
            MainContext.Post((state) =>
            {
                var element = LookupAutomationElement(wrapper);
                if (!(element is null))
                    element.OnChildrenChanged(arg2, arg3);
            }, null);
        }

        private void OnFocusChangedBackground(AutomationElement obj)
        {
            var wrapper = WrapElement(obj);
            MainContext.Post((state) =>
            {
                OnFocusChanged(wrapper);
            }, null);
        }

        private async Task UpdateFocusedElement()
        {
            FocusedElement = await CommandThread.GetFocusedElement(this);
        }

        private void OnWindowOpenedBackground(AutomationElement arg1, EventId arg2)
        {
            var wrapper = WrapElement(arg1.Parent);

            MainContext.Post((object state) =>
            {
                OnWindowOpened(wrapper);
            }, null);
        }

        private void OnWindowClosedBackground(AutomationElement arg1, EventId arg2)
        {
            var wrapper = WrapElement(arg1.Parent);

            MainContext.Post((object state) =>
            {
                OnWindowClosed(wrapper);
            }, null);
        }

        private void OnMsaaEvent(IntPtr hWinEventProc, uint eventId, IntPtr hwnd, int idObject, int idChild, int idEventThread, int dwmsEventTime)
        {
            switch (eventId)
            {
                case EVENT_SYSTEM_FOREGROUND:
                case EVENT_OBJECT_CREATE:
                case EVENT_OBJECT_DESTROY:
                    TranslateMsaaEvent(eventId, hwnd, idObject, idChild, idEventThread, dwmsEventTime);
                    break;
            }
        }

        private void TranslateMsaaEvent(uint eventId, IntPtr hwnd, int idObject, int idChild, int idEventThread, int dwmsEventTime)
        {
            Utils.RunTask(TranslateMsaaEventTask(eventId, hwnd, idObject, idChild, idEventThread, dwmsEventTime));
        }

        private async Task TranslateMsaaEventTask(uint eventId, IntPtr hwnd, int idObject, int idChild, int idEventThread, int dwmsEventTime)
        {
            if (eventId == EVENT_OBJECT_CREATE || eventId == EVENT_OBJECT_DESTROY)
            {
                // Can't retrieve the actual IAccessible for this event, get the parent instead.

                if (idChild != CHILDID_SELF)
                    idChild = CHILDID_SELF;
                else if (idObject != OBJID_WINDOW && idObject != OBJID_CLIENT)
                    idObject = OBJID_WINDOW;
                else
                {
                    var parent = GetAncestor(hwnd, GA_PARENT);
                    if (parent == GetDesktopWindow())
                        hwnd = GetWindow(hwnd, GW_OWNER);
                    else
                        hwnd = parent;
                    idObject = OBJID_CLIENT;
                }
            }

            var wrapper = await WrapperFromHwnd(hwnd, idObject, idChild);

            if (eventId == EVENT_SYSTEM_FOREGROUND)
            {
                // We may not know about the element yet, so just set the wrapper.
                ForegroundElement = wrapper;
                return;
            }

            if (!wrapper.IsValid)
            {
                if ((eventId == EVENT_OBJECT_CREATE || eventId == EVENT_OBJECT_DESTROY) &&
                    idChild == CHILDID_SELF &&
                    (idObject == OBJID_WINDOW || idObject == OBJID_CLIENT))
                {
                    // Parent may be effectively the desktop window.
                    UpdateChildren();
                }
                return;
            }

            var element = LookupAutomationElement(wrapper);

            if (element is null)
                return;

            switch (eventId)
            {
                case EVENT_OBJECT_CREATE:
                case EVENT_OBJECT_DESTROY:
                    element.UpdateChildren();
                    break;
            }
        }

        private void OnFocusChanged(UiaElementWrapper obj)
        {
            FocusedElement = obj;
        }

        private void OnWindowClosed(UiaElementWrapper parent)
        {
            var element = LookupAutomationElement(parent);
            if (!(element is null))
                element.UpdateChildren();
        }

        private void OnWindowOpened(UiaElementWrapper parent)
        {
            var element = LookupAutomationElement(parent);
            if (!(element is null))
                element.UpdateChildren();
        }

        public override string DebugId => "UiaConnection";

        public AutomationBase Automation { get; private set; }
        public SynchronizationContext MainContext { get; }
        
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

        UiaElementWrapper foreground_element;

        public UiaElementWrapper ForegroundElement
        {
            get { return foreground_element; }
            private set
            {
                var old_foreground_element = foreground_element;

                if (old_foreground_element.Equals(value))
                {
                    return;
                }

#if DEBUG
                Console.WriteLine($"Foreground window changed to {value.UniqueId}");
#endif

                foreground_element = value;

                PropertyChanged("msaa_foreground_element");

                if (LookupAutomationElement(old_foreground_element) is UiaElement old)
                {
                    old.PropertyChanged("msaa_foreground");
                }

                if (LookupAutomationElement(value) is UiaElement new_focused)
                {
                    new_focused.PropertyChanged("msaa_foreground");
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
                case "msaa_foreground_element":
                case "foreground_element":
                    depends_on.Add((Root, new IdentifierExpression("msaa_foreground_element")));
                    return (UiDomValue)LookupAutomationElement(ForegroundElement) ?? UiDomUndefined.Instance;
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
