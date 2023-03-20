using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Exceptions;
using FlaUI.Core.Identifiers;
using FlaUI.UIA3;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;
using AccessibilityRole = FlaUI.Core.WindowsAPI.AccessibilityRole;
using IAccessible = Accessibility.IAccessible;
using IUIAutomationLegacyIAccessiblePattern = Interop.UIAutomationClient.IUIAutomationLegacyIAccessiblePattern;

namespace Xalia.Uia
{
    public class UiaConnection : UiDomRoot
    {
        static int MonotonicElementId; // For cases where we can't get any unique id for an element

        static List<WINEVENTPROC> event_proc_delegates = new List<WINEVENTPROC>(); // to make sure delegates aren't GC'd while in use

        internal Dictionary<string, PropertyId> names_to_property = new Dictionary<string, PropertyId>();
        internal Dictionary<PropertyId, string> properties_to_name = new Dictionary<PropertyId, string>();

        bool polling_focus;
        CancellationTokenSource focus_poll_token;

        // Dictionary mapping parent,role to a set of known child ids and elements
        ConcurrentDictionary<string, ConcurrentDictionary<AccessibilityRole, ConcurrentBag<(string, AutomationElement)>>> no_id_elements =
            new ConcurrentDictionary<string, ConcurrentDictionary<AccessibilityRole, ConcurrentBag<(string, AutomationElement)>>>();

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
            RegisterPropertyMapping("uia_automation_id", Automation.PropertyLibrary.Element.AutomationId);
            RegisterPropertyMapping("uia_class_name", Automation.PropertyLibrary.Element.ClassName);
            RegisterPropertyMapping("uia_enabled", Automation.PropertyLibrary.Element.IsEnabled);
            RegisterPropertyMapping("uia_keyboard_focusable", Automation.PropertyLibrary.Element.IsKeyboardFocusable);
            RegisterPropertyMapping("uia_has_keyboard_focus", Automation.PropertyLibrary.Element.HasKeyboardFocus);
            RegisterPropertyMapping("uia_bounding_rectangle", Automation.PropertyLibrary.Element.BoundingRectangle);
            RegisterPropertyMapping("uia_name", Automation.PropertyLibrary.Element.Name);
            RegisterPropertyMapping("uia_offscreen", Automation.PropertyLibrary.Element.IsOffscreen);
            RegisterPropertyMapping("uia_selected", Automation.PropertyLibrary.SelectionItem.IsSelected);
            RegisterPropertyMapping("uia_expand_collapse_state", Automation.PropertyLibrary.ExpandCollapse.ExpandCollapseState);
            RegisterPropertyMapping("uia_orientation", Automation.PropertyLibrary.Element.Orientation);
            RegisterPropertyMapping("uia_framework_id", Automation.PropertyLibrary.Element.FrameworkId);
            RegisterPropertyMapping("msaa_role", Automation.PropertyLibrary.LegacyIAccessible.Role);
            RegisterPropertyMapping("aria_role", Automation.PropertyLibrary.Element.AriaRole);

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
                    Automation.EventLibrary.Window.WindowClosedEvent, TreeScope.Element | TreeScope.Descendants,
                    OnWindowClosedBackground);

                DesktopElement.RegisterAutomationEvent(
                    Automation.EventLibrary.Element.MenuModeStartEvent, TreeScope.Element | TreeScope.Descendants,
                    OnMenuModeStartBackground);

                DesktopElement.RegisterAutomationEvent(
                    Automation.EventLibrary.Element.MenuModeEndEvent, TreeScope.Element | TreeScope.Descendants,
                    OnMenuModeEndBackground);

                DesktopElement.RegisterAutomationEvent(
                    Automation.EventLibrary.Element.MenuOpenedEvent, TreeScope.Element | TreeScope.Descendants,
                    OnMenuOpenedBackground);

                DesktopElement.RegisterAutomationEvent(
                    Automation.EventLibrary.Element.MenuClosedEvent, TreeScope.Element | TreeScope.Descendants,
                    OnMenuClosedBackground);

                DesktopElement.RegisterAutomationEvent(
                    Automation.EventLibrary.SelectionItem.ElementSelectedEvent, TreeScope.Element | TreeScope.Descendants,
                    OnSelectedBackground);

                DesktopElement.RegisterAutomationEvent(
                    Automation.EventLibrary.SelectionItem.ElementAddedToSelectionEvent, TreeScope.Element | TreeScope.Descendants,
                    OnAddedToSelectionBackground);

                DesktopElement.RegisterAutomationEvent(
                    Automation.EventLibrary.SelectionItem.ElementRemovedFromSelectionEvent, TreeScope.Element | TreeScope.Descendants,
                    OnRemovedFromSelectionBackground);
            });

            Utils.RunTask(UpdateFocusedElement());

            UpdateChildren(); // in case children changed before the events were registered

            Utils.RunTask(UpdateActiveWindow());
        }

        private void OnSelected(object s)
        {
            var wrapper = (UiaElementWrapper)s;

            var element = LookupAutomationElement(wrapper);

            if (element != null)
            {
                foreach (var sibling in element.Parent.Children)
                {
                    ((UiaElement)sibling).OnPropertyChange("uia_selected", Automation.PropertyLibrary.SelectionItem.IsSelected,
                        sibling == element);
                }
            }
        }

        private void OnSelectedBackground(AutomationElement arg1, EventId arg2)
        {
            var wrapper = WrapElement(arg1);
            MainContext.Post(OnSelected, wrapper);
        }

        private void OnAddedToSelection(object s)
        {
            var wrapper = (UiaElementWrapper)s;

            var element = LookupAutomationElement(wrapper);

            if (element != null)
            {
                element.OnPropertyChange("uia_selected", Automation.PropertyLibrary.SelectionItem.IsSelected, true);
            }
        }
        
        private void OnAddedToSelectionBackground(AutomationElement arg1, EventId arg2)
        {
            var wrapper = WrapElement(arg1);
            MainContext.Post(OnAddedToSelection, wrapper);
        }

        private void OnRemovedFromSelection(object s)
        {
            var wrapper = (UiaElementWrapper)s;

            var element = LookupAutomationElement(wrapper);

            if (element != null)
            {
                element.OnPropertyChange("uia_selected", Automation.PropertyLibrary.SelectionItem.IsSelected, false);
            }
        }

        private void OnRemovedFromSelectionBackground(AutomationElement arg1, EventId arg2)
        {
            var wrapper = WrapElement(arg1);
            MainContext.Post(OnRemovedFromSelection, wrapper);
        }

        internal List<UiaElementWrapper> menu_elements = new List<UiaElementWrapper>();

        public UiaElementWrapper UiaOpenedMenu
        {
            get
            {
                return menu_elements.LastOrDefault();
            }
        }

        public bool UiaInMenu
        {
            get
            {
                return menu_elements.Count >= 1;
            }
        }

        public bool UiaInSubmenu
        {
            get
            {
                return menu_elements.Count >= 2;
            }
        }

        private void OnMenuClosed(object state)
        {
            // A closing menu may no longer have an HWND or RuntimeId.
            // Just assume it was the last menu to be opened.
            if (menu_elements.Count == 0)
                return;

            menu_elements.RemoveAt(menu_elements.Count - 1);

            PropertyChanged("uia_opened_menu");

            if (menu_elements.Count == 0)
                PropertyChanged("uia_in_menu");
            else if (menu_elements.Count == 1)
                PropertyChanged("uia_in_submenu");
        }

        private void OnMenuClosedBackground(AutomationElement arg1, EventId arg2)
        {
            MainContext.Post(OnMenuClosed, null);
        }

        private void OnMenuOpened(object state)
        {
            var wrapper = (UiaElementWrapper)state;

            menu_elements.Add(wrapper);

            PropertyChanged("uia_opened_menu");

            if (menu_elements.Count == 1)
                PropertyChanged("uia_in_menu");
            else if (menu_elements.Count == 2)
                PropertyChanged("uia_in_submenu");
        }

        private void OnMenuOpenedBackground(AutomationElement arg1, EventId arg2)
        {
            var wrapper = WrapElement(arg1);

            MainContext.Post(OnMenuOpened, wrapper);
        }

        public bool UiaMenuMode { get; private set; } = false;

        private void OnMenuModeEndBackground(AutomationElement arg1, EventId arg2)
        {
            MainContext.Post((object s) =>
            {
                UiaMenuMode = false;
                PropertyChanged("uia_menu_mode");
            }, null);
        }

        private void OnMenuModeStartBackground(AutomationElement arg1, EventId arg2)
        {
            MainContext.Post((object s) =>
            {
                UiaMenuMode = true;
                PropertyChanged("uia_menu_mode");
            }, null);
        }

        HashSet<IntPtr> visible_toplevel_hwnds = new HashSet<IntPtr>();

        Dictionary<IntPtr, UiaElement> toplevels_by_hwnd = new Dictionary<IntPtr, UiaElement>();

        internal bool IsUiaToplevel(IntPtr hwnd)
        {
            if (!WindowIsVisible(hwnd))
                return false;

            if (GetAncestor(hwnd, GA_PARENT) != GetDesktopWindow())
                return false;

            var window_class = RealGetWindowClass(hwnd);

            if (window_class == "#32770")
            {
                // Owned dialog windows show up as children of the owner in the UIA tree
                var owner_window = GetWindow(hwnd, GW_OWNER);

                if (owner_window != IntPtr.Zero && WindowIsVisible(owner_window))
                    return false;
            }

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
            if (elements_by_id.TryGetValue($"hwnd-{hwnd}", out var cached))
                return Task.FromResult(cached.ElementWrapper);

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
                catch (TimeoutException)
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
            if (objectId == OBJID_WINDOW || objectId == OBJID_CLIENT)
            {
                if (childId == CHILDID_SELF)
                    return WrapperFromHwnd(hwnd);
                if (elements_by_id.TryGetValue($"hwnd-{hwnd}-{childId}", out var cached))
                    return Task.FromResult(cached.ElementWrapper);
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
                catch (ArgumentException)
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

            var element = new UiaElement(wrapper);

            AddChild(Children.Count, element);
            toplevels_by_hwnd[hwnd] = element;
        }

        private void OnPropertyChangedBackground(AutomationElement arg1, PropertyId arg2, object arg3)
        {
            var wrapper = WrapElement(arg1);
            if (arg3 is null)
            {
                try
                {
                    arg1.FrameworkAutomationElement.TryGetPropertyValue(arg2, out arg3);
                }
                catch (COMException) { }
                catch (ElementNotAvailableException) { }
            }
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
            UiaElementWrapper wrapper;
            try
            {
                wrapper = await CommandThread.OnBackgroundThread(() =>
                {
                    var result = Automation.FocusedElement();
                    return WrapElement(result);
                });
            }
            catch (Exception e)
            {
                if (!UiaElement.IsExpectedException(e))
                    throw;
                wrapper = default;
            }

            FocusedElement = wrapper;
        }

        private async Task PollFocusedElement()
        {
            if (!polling_focus)
                return;

            await UpdateFocusedElement();

            focus_poll_token = new CancellationTokenSource();

            try
            {
                await Task.Delay(200, focus_poll_token.Token);
            }
            catch (TaskCanceledException)
            {
                focus_poll_token = null;
                return;
            }

            focus_poll_token = null;
            Utils.RunTask(PollFocusedElement());
        }

        protected override void DeclarationsChanged(Dictionary<string, (GudlDeclaration, UiDomValue)> all_declarations, HashSet<(UiDomElement, GudlExpression)> dependencies)
        {
            bool poll_focus = all_declarations.TryGetValue("poll_focus", out var ui_dom_poll_focus) && ui_dom_poll_focus.Item2.ToBool();

            if (poll_focus != polling_focus)
            {
                polling_focus = poll_focus;

                if (poll_focus)
                {
                    Utils.RunTask(PollFocusedElement());
                }
                else if (!(focus_poll_token is null))
                {
                    focus_poll_token.Cancel();
                    focus_poll_token = null;
                }
            }

            base.DeclarationsChanged(all_declarations, dependencies);
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
            UiaElementWrapper wrapper;
            try
            {
                wrapper = WrapElement(arg1.Parent);
            }
            catch (NullReferenceException)
            {
                // This can be thrown by AutomationElement.get_Parent
                return;
            }

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
                Utils.RunTask(UpdateActiveWindow());
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
            Utils.RunTask(UpdateActiveWindow());
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

        public UiaElementWrapper WrapElement(AutomationElement ae, string parent_id=null, bool assume_unique=false)
        {
            if (ae is null)
                return UiaElementWrapper.InvalidElement;
            try
            {
                return new UiaElementWrapper(ae, this, parent_id, assume_unique);
            }
            catch (Exception e)
            {
                if (UiaElement.IsExpectedException(e))
                    return UiaElementWrapper.InvalidElement;
                throw;
            }
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

                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"Focus changed to {value.UniqueId}");

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

                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"Foreground window changed to {value.UniqueId}");

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

        UiaElementWrapper active_element;

        public UiaElementWrapper ActiveElement
        {
            get { return active_element; }
            private set
            {
                var old_active_element = active_element;

                if (old_active_element.Equals(value))
                {
                    return;
                }

                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"Active window changed to {value.UniqueId}");

                active_element = value;

                PropertyChanged("win32_active_element");

                if (LookupAutomationElement(old_active_element) is UiaElement old)
                {
                    old.PropertyChanged("win32_active");
                }

                if (LookupAutomationElement(value) is UiaElement new_active)
                {
                    new_active.PropertyChanged("win32_active");
                }
            }
        }

        bool updating_active_window;
        bool inflight_updating_active_window;

        internal async Task UpdateActiveWindow()
        {
            if (updating_active_window)
            {
                inflight_updating_active_window = true;
                return;
            }

            updating_active_window = true;

            GUITHREADINFO info = default;
            info.cbSize = Marshal.SizeOf<GUITHREADINFO>();
            GetGUIThreadInfo(0, ref info);

            try
            {
                ActiveElement = await WrapperFromHwnd(info.hwndActive);
            }
            catch (Exception e)
            {
                Utils.DebugWriteLine("Error getting element for active window:");
                Utils.DebugWriteLine(e);
            }

            updating_active_window = false;
            if (inflight_updating_active_window)
            {
                inflight_updating_active_window = false;
                Utils.RunTask(UpdateActiveWindow());
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
                case "active_element":
                case "win32_active_element":
                    depends_on.Add((Root, new IdentifierExpression("win32_active_element")));
                    return (UiDomValue)LookupAutomationElement(ActiveElement) ?? UiDomUndefined.Instance;
                case "menu_mode":
                case "uia_menu_mode":
                    depends_on.Add((Root, new IdentifierExpression("uia_menu_mode")));
                    return UiDomBoolean.FromBool(UiaMenuMode);
                case "opened_menu":
                case "uia_opened_menu":
                    depends_on.Add((Root, new IdentifierExpression("uia_opened_menu")));
                    return (UiDomValue)LookupAutomationElement(UiaOpenedMenu) ?? UiDomUndefined.Instance;
                case "in_menu":
                case "uia_in_menu":
                    depends_on.Add((Root, new IdentifierExpression("uia_in_menu")));
                    return UiDomBoolean.FromBool(UiaInMenu);
                case "in_submenu":
                case "uia_in_submenu":
                    depends_on.Add((Root, new IdentifierExpression("uia_in_submenu")));
                    return UiDomBoolean.FromBool(UiaInSubmenu);
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override void DumpProperties()
        {
            var focused = LookupAutomationElement(focused_element);
            if (!(focused is null))
                Utils.DebugWriteLine($"  uia_focused_element: {focused}");
            var foreground = LookupAutomationElement(foreground_element);
            if (!(foreground is null))
                Utils.DebugWriteLine($"  msaa_foreground_element: {foreground}");
            var active = LookupAutomationElement(active_element);
            if (!(active is null))
                Utils.DebugWriteLine($"  win32_active_element: {active}");
            if (UiaMenuMode)
                Utils.DebugWriteLine($"  uia_menu_mode: true");
            var opened_menu = LookupAutomationElement(UiaOpenedMenu);
            if (!(opened_menu is null))
                Utils.DebugWriteLine($"  uia_opened_menu: {opened_menu}");
            if (UiaInMenu)
                Utils.DebugWriteLine($"  uia_in_menu: true");
            if (UiaInSubmenu)
                Utils.DebugWriteLine($"  uia_in_submenu: true");
            base.DumpProperties();
        }

        internal IAccessible GetIAccessibleBackground(AutomationElement element, out int child_id)
        {
            child_id = default;

            var fae = element.FrameworkAutomationElement as UIA3FrameworkAutomationElement;
            if (fae is null)
                return null;

            var nae = fae.NativeElement;

            try
            {
                var lia = nae.GetCurrentPattern(Automation.PatternLibrary.LegacyIAccessiblePattern.Id) as IUIAutomationLegacyIAccessiblePattern;
                if (lia is null)
                    return null;

                child_id = lia.CurrentChildId;

                return (IAccessible)lia.GetIAccessible();
            }
            catch (COMException e)
            {
                if (!UiaElement.IsExpectedException(e))
                    Utils.OnError(e);
                return null;
            }
        }

        internal bool HasNonIdChildren(UiaElementWrapper wrapper)
        {
            return no_id_elements.ContainsKey(wrapper.UniqueId);
        }

        internal string BlockingGetElementId(AutomationElement element, out IntPtr hwnd, string parent_id=null, bool assume_unique=false)
        {
            // hwnd/childid pair
            try
            {
                if (element.FrameworkAutomationElement.TryGetPropertyValue<IntPtr>(
                    Automation.PropertyLibrary.Element.NativeWindowHandle, out hwnd))
                {
                    int childid = 0;

                    try
                    {
                        if (element.FrameworkAutomationElement.TryGetPropertyValue<int>(
                            Automation.PropertyLibrary.LegacyIAccessible.ChildId, out var cid))
                        {
                            childid = cid;
                        }
                    }
                    catch (COMException) { }

                    if (childid == 0)
                    {
                        return $"hwnd-{hwnd}";
                    }
                    else
                    {
                        return $"hwnd-{hwnd}-{childid}";
                    }
                }
            }
            catch (COMException)
            {
                // Fall back on other methods
            }

            hwnd = IntPtr.Zero;

            // UIAutomation runtimeid
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

            var acc = GetIAccessibleBackground(element, out var child_id);
            AutomationElement parent = null;
            try
            {
                parent = element.Parent;
            }
            catch (Exception e)
            {
                if (!UiaElement.IsExpectedException(e))
                    throw;
            }

            if (!(acc is null) && !(parent is null))
            {
                if (parent_id is null)
                    parent_id = BlockingGetElementId(parent, out var _unused);

                // Query for IAccessible2 directly, for old Qt versions
                var acc2 = QueryIAccessible2(acc);

                if (!(acc2 is null))
                {
                    if (child_id != 0)
                        return $"{parent_id}-acc2-{acc2.uniqueID}-{child_id}";
                    return $"{parent_id}-acc2-{acc2.uniqueID}";
                }

                // Use MSAA attributes and comparison to find elements
                if (!no_id_elements.TryGetValue(parent_id, out var roles))
                {
                    // TryAdd may fail, in which case we use whatever was actually added
                    no_id_elements.TryAdd(parent_id, new ConcurrentDictionary<AccessibilityRole, ConcurrentBag<(string, AutomationElement)>>());
                    roles = no_id_elements[parent_id];
                }
                AccessibilityRole role = default;
                try
                {
                    role = element.Patterns.LegacyIAccessible.Pattern.Role.ValueOrDefault;
                }
                catch (Exception e2)
                {
                    if (!UiaElement.IsExpectedException(e2))
                        throw;
                }
                if (!roles.TryGetValue(role, out var values))
                {
                    // TryAdd may fail, in which case we use whatever was actually added
                    roles.TryAdd(role, new ConcurrentBag<(string, AutomationElement)>());
                    values = roles[role];
                }
                while (true)
                {
                    // search for already-seen item
                    var values_list = values.ToList();
                    if (!assume_unique && MsaaSiblingSearchList(element, values_list, out var item))
                        return item.Item1;
                    lock (values)
                    {
                        if (!assume_unique && values.Count != values_list.Count)
                        {
                            // bag was modified during iteration, try again
                            continue;
                        }
                        // add item
                        var result = $"element-{Interlocked.Increment(ref MonotonicElementId)}";
                        values.Add((result, element));
                        return result;
                    }
                }
            }

            var id = Interlocked.Increment(ref MonotonicElementId);
            return $"incomparable-element-{id}";
        }

        struct MsaaSiblingComparisonInfo
        {
            public int child_id;
            public int left, top, width, height;
            public int state;
            public int index;
        }

        private bool GetSiblingComparisonInfo(AutomationElement element, MsaaSiblingComparisonInfo? comparand, out MsaaSiblingComparisonInfo result)
        {
            result = default;
            var acc = GetIAccessibleBackground(element, out var child_id);
            if (acc is null)
                return false;
            result.child_id = child_id;
            if (comparand.HasValue && comparand.Value.child_id != result.child_id)
                return false;
            try
            {
                acc.accLocation(out result.left, out result.top, out result.width, out result.height,
                    child_id);
            }
            catch (Exception e)
            {
                if (!UiaElement.IsExpectedException(e))
                    throw;
            }
            if (comparand.HasValue &&
                (comparand.Value.left != result.left ||
                 comparand.Value.top != result.top ||
                 comparand.Value.width != result.width ||
                 comparand.Value.height != result.height))
                return false;
            try
            {
                result.state = (int)acc.accState[child_id];
            }
            catch (InvalidCastException) { }
            catch (Exception e)
            {
                if (!UiaElement.IsExpectedException(e))
                    throw;
            }
            if (comparand.HasValue && comparand.Value.state != result.state)
                return false;
            if (child_id == 0)
            {
                try
                {
                    int index = 0;
                    var nav_acc = acc;
                    int nav_child_id = child_id;
                    while (true)
                    {
                        if (!AccessibleNavigate(ref nav_acc, ref nav_child_id, NAVDIR_PREVIOUS))
                            break;
                        index++;
                        if (comparand.HasValue && comparand.Value.index < index)
                            return false;
                    }
                    result.index = index;
                    if (comparand.HasValue && comparand.Value.index != result.index)
                        return false;
                }
                catch (Exception e)
                {
                    if (!UiaElement.IsExpectedException(e))
                        throw;
                }
            }
            return true;
        }

        private bool MsaaSiblingSearchList(AutomationElement item1, List<(string, AutomationElement)> list,
            out (string, AutomationElement) result)
        {
            result = default;

            while (true)
            {
                if (!GetSiblingComparisonInfo(item1, null, out var info1))
                    return false;

                bool found = false;

                foreach (var item2 in list)
                {
                    if (GetSiblingComparisonInfo(item2.Item2, info1, out var _unused))
                    {
                        found = true;
                        result = item2;
                        break;
                    }
                }

                if (!GetSiblingComparisonInfo(item1, info1, out var _unused2))
                {
                    // info was modified during search
                    result = default;
                    return false;
                }

                return found;
            }
        }

        internal void NotifyElementDefunct(UiaElement element)
        {
            no_id_elements.TryRemove(element.DebugId, out var _unused);
        }
    }
}
