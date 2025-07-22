using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;
using IServiceProvider = Xalia.Interop.Win32.IServiceProvider;

namespace Xalia.Win32
{
    internal class AccessibleProvider : UiDomProviderBase
    {
        public AccessibleProvider(HwndProvider root_hwnd, UiDomElement element,
            IAccessible accessible, int child_id)
        {
            if (element is null)
                throw new ArgumentNullException(nameof(element));
            if (accessible is null)
                throw new ArgumentNullException(nameof(accessible));
            RootHwnd = root_hwnd;
            Element = element;
            IAccessible = accessible;
            ChildId = child_id;
        }

        private static string[] tracked_properties = new string[] {
            "recurse_method", "poll_msaa_state", "poll_msaa_location",
        };

        private const int DEFAULT_POLL_INTERVAL = 200;

        public HwndProvider RootHwnd { get; }
        public UiDomElement Element { get; private set; }
        public Win32Connection Connection => RootHwnd.Connection;
        public int Tid => RootHwnd.Tid;
        public CommandThread CommandThread => RootHwnd.CommandThread;
        public IAccessible IAccessible { get; private set; }
        public int ChildId { get; }

        enum RecurseMethod
        {
            None,
            IEnumVARIANT,
            accChild,
            accNavigate,
            Auto
        }
        private RecurseMethod _recurseMethod;

        public int Role { get; private set; }
        public bool RoleKnown { get; private set; }
        private bool _fetchingRole;

        public int State { get; private set; }
        public bool StateKnown { get; private set; }
        private bool _watchingState;
        private int _stateChangeCount;
        private bool _pollingState;

        public string Name { get; private set; }
        public bool NameKnown { get; private set; }
        private bool _watchingName;
        private int _nameChangeCount;

        public RECT Location { get; private set; }
        public bool LocationKnown { get; private set; }
        private bool _watchingLocation;
        private int _locationChangeCount;
        private bool _pollingLocation;

        public string DefaultAction { get; private set; }
        public bool DefaultActionKnown { get; private set; }
        private bool _watchingDefaultAction;
        private int _defaultActionChangeCount;

        static bool DebugExceptions = Environment.GetEnvironmentVariable("XALIA_DEBUG_EXCEPTIONS") != "0";
        internal static bool IsExpectedException(Exception e)
        {
#if DEBUG
            if (DebugExceptions)
            {
                Utils.DebugWriteLine("WARNING: Exception:");
                Utils.DebugWriteLine(e);
            }
#endif
            if (e is InvalidOperationException)
            {
                return true;
            }
            if (e is COMException com)
            {
                switch (com.ErrorCode)
                {
                    case unchecked((int)0x80004005): // E_FAIL
                    case unchecked((int)0x80010012): // RPC_E_SERVER_DIED_DNE
                    case unchecked((int)0x80010108): // RPC_E_DISCONNECTED
                    case unchecked((int)0x80020003): // DISP_E_MEMBERNOTFOUND
                    case unchecked((int)0x800401FD): // CO_E_OBJNOTCONNECTED
                    case unchecked((int)0x80040201): // EVENT_E_ALL_SUBSCRIBERS_FAILED
                    case unchecked((int)0x80070490): // HRESULT_FROM_WIN32(ERROR_NOT_FOUND)
                    case unchecked((int)0x800706B5): // RPC_S_UNKNOWN_IF
                    case unchecked((int)0x800706BA): // RPC_E_SERVER_UNAVAILABLE
                    case unchecked((int)0x800706BE): // RPC_S_CALL_FAILED
                    case unchecked((int)0x80131505): // UIA_E_TIMEOUT
                        return true;
                }
            }
            if (e is UnauthorizedAccessException)
            {
                return true;
            }
            if (e is ArgumentException)
            {
                // thrown for stale childid's
                return true;
            }
            if (e is NotImplementedException)
            {
                return true;
            }
#if DEBUG
            return false;
#else
            if (DebugExceptions)
            {
                Utils.DebugWriteLine("WARNING: Exception ignored:");
                Utils.DebugWriteLine(e);
            }
            return true;
#endif
        }

        static Dictionary<string, string> property_aliases = new Dictionary<string, string>
        {
            { "name", "msaa_name" },
            { "x", "msaa_x" },
            { "y", "msaa_y" },
            { "width", "msaa_width" },
            { "height", "msaa_height" },
            { "default_action", "msaa_default_action" },
        };

        static AccessibleProvider()
        {
            msaa_role_to_enum = new UiDomEnum[msaa_role_names.Length];
            msaa_name_to_role = new Dictionary<string, int>();
            msaa_name_to_state = new Dictionary<string, int>();
            for (int i = 0; i < msaa_role_names.Length; i++)
            {
                string name = msaa_role_names[i];
                string[] names;
                if (name == "cell")
                    names = new[] { "cell", "table_cell", "tablecell" };
                else if (name == "row_header")
                    names = new[] { "row_header", "rowheader", "table_row_header", "tablerowheader" };
                else if (name == "column_header")
                    names = new[] { "column_header", "columnheader", "column_row_header", "columnrowheader" };
                else if (name == "row")
                    names = new[] { "row", "table_row", "tablerow" };
                else if (name == "push_button")
                    names = new[] { "push_button", "pushbutton", "button" };
                else if (name == "link")
                    names = new[] { "link", "hyperlink" };
                else if (name.Contains("_"))
                    names = new[] { name, name.Replace("_", "") };
                else
                    names = new[] { name };
                foreach (string rolename in names)
                    msaa_name_to_role[rolename] = i;
                msaa_role_to_enum[i] = new UiDomEnum(names);
            }
            for (int i = 0; i < msaa_state_names.Length; i++)
            {
                msaa_name_to_state[msaa_state_names[i]] = 1 << i;
            }
        }

        internal static readonly string[] msaa_role_names =
        {
            "unknown",
            "title_bar",
            "menu_bar",
            "scroll_bar",
            "grip",
            "sound",
            "cursor",
            "caret",
            "alert",
            "window",
            "client",
            "menu_popup",
            "menu_item",
            "tool_tip",
            "application",
            "document",
            "pane",
            "chart",
            "dialog",
            "border",
            "grouping",
            "separator",
            "tool_bar",
            "status_bar",
            "table",
            "column_header",
            "row_header",
            "column",
            "row",
            "cell",
            "link",
            "help_balloon",
            "character",
            "list",
            "list_item",
            "outline",
            "outline_item",
            "page_tab",
            "property_page",
            "indicator",
            "graphic",
            "static_text",
            "text",
            "push_button",
            "check_button",
            "radio_button",
            "combo_box",
            "drop_list",
            "progress_bar",
            "dial",
            "hotkey_field",
            "slider",
            "spin_button",
            "diagram",
            "animation",
            "equation",
            "button_dropdown",
            "button_menu",
            "button_dropdown_grid",
            "white_space",
            "page_tab_list",
            "clock",
            "split_button",
            "ip_address",
            "outline_button",
        };

        internal static readonly string[] msaa_state_names =
        {
            "unavailable",
            "selected",
            "focused",
            "pressed",
            "checked",
            "mixed",
            "readonly",
            "hottracked",
            "default",
            "expanded",
            "collapsed",
            "busy",
            "floating",
            "marqueed",
            "animated",
            "invisible",
            "offscreen",
            "sizeable",
            "moveable",
            "selfvoicing",
            "focusable",
            "selectable",
            "linked",
            "traversed",
            "multiselectable",
            "extselectable",
            "alert_low",
            "alert_medium",
            "alert_high",
            "protected"
        };

        internal static readonly UiDomEnum[] msaa_role_to_enum;
        internal static readonly Dictionary<string, int> msaa_name_to_role;
        internal static readonly Dictionary<string, int> msaa_name_to_state;

        public UiDomValue RoleAsValue
        {
            get
            {
                if (RoleKnown)
                {
                    if (Role >= 0 && Role < msaa_role_to_enum.Length)
                        return msaa_role_to_enum[Role];
                    return new UiDomInt(Role);
                }
                return UiDomUndefined.Instance;
            }
        }

        public override void NotifyElementRemoved(UiDomElement element)
        {
            _recurseMethod = RecurseMethod.None;
            base.NotifyElementRemoved(element);
        }

        private static string GetStateNames(int state)
        {
            var state_names = new StringBuilder();
            bool first_flag = true;
            for (int i=0; i < msaa_state_names.Length; i++)
            {
                if ((state & (1 << i)) != 0)
                {
                    if (!first_flag)
                        state_names.Append("|");
                    first_flag = false;
                    state_names.Append(msaa_state_names[i]);
                }
            }
            return state_names.ToString();
        }

        public override void DumpProperties(UiDomElement element)
        {
            if (RoleKnown)
                Utils.DebugWriteLine($"  msaa_role: {RoleAsValue}");
            if (StateKnown)
                Utils.DebugWriteLine($"  msaa_state: 0x{State:X} ({GetStateNames(State)})");
            if (NameKnown)
                Utils.DebugWriteLine($"  msaa_name: {Name}");
            if (LocationKnown)
            {
                Utils.DebugWriteLine($"  msaa_x {Location.left}");
                Utils.DebugWriteLine($"  msaa_y {Location.top}");
                Utils.DebugWriteLine($"  msaa_width {Location.width}");
                Utils.DebugWriteLine($"  msaa_height {Location.height}");
            }
            if (DefaultActionKnown)
            {
                if (DefaultAction is null)
                    Utils.DebugWriteLine("  msaa_default_action: false");
                else
                    Utils.DebugWriteLine($"  msaa_default_action: {DefaultAction}");
            }
            if (RootHwnd.Element != Element)
                RootHwnd.ChildDumpProperties();
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_msaa_element":
                    return UiDomBoolean.True;
                case "msaa_role":
                    depends_on.Add((Element, new IdentifierExpression("msaa_role")));
                    return RoleAsValue;
                case "msaa_state":
                    depends_on.Add((Element, new IdentifierExpression(identifier)));
                    if (StateKnown)
                        return new UiDomInt(State);
                    break;
                case "msaa_state_names":
                    depends_on.Add((Element, new IdentifierExpression("msaa_state")));
                    if (StateKnown)
                    {
                        List<string> states = new List<string>();
                        for (int i = 0; i < msaa_state_names.Length; i++)
                        {
                            if ((State & (1 << i)) != 0)
                            {
                                states.Add(msaa_state_names[i]);
                            }
                        }
                        if (states.Count > 0)
                            return new UiDomEnum(states.ToArray());
                    }
                    break;
                case "msaa_name":
                    depends_on.Add((Element, new IdentifierExpression(identifier)));
                    if (NameKnown)
                        return new UiDomString(Name);
                    break;
                case "msaa_x":
                    depends_on.Add((Element, new IdentifierExpression("msaa_location")));
                    if (LocationKnown)
                        return new UiDomInt(Location.left);
                    break;
                case "msaa_y":
                    depends_on.Add((Element, new IdentifierExpression("msaa_location")));
                    if (LocationKnown)
                        return new UiDomInt(Location.top);
                    break;
                case "msaa_width":
                    depends_on.Add((Element, new IdentifierExpression("msaa_location")));
                    if (LocationKnown)
                        return new UiDomInt(Location.width);
                    break;
                case "msaa_height":
                    depends_on.Add((Element, new IdentifierExpression("msaa_location")));
                    if (LocationKnown)
                        return new UiDomInt(Location.height);
                    break;
                case "msaa_default_action":
                    depends_on.Add((Element, new IdentifierExpression(identifier)));
                    if (DefaultActionKnown)
                    {
                        if (DefaultAction is null)
                            return UiDomBoolean.False;
                        return new UiDomString(DefaultAction);
                    }
                    break;
                case "msaa_do_default_action":
                    return new UiDomRoutineAsync(Element, "msaa_do_default_action", DoDefaultActionAsync);
            }
            return RootHwnd.ChildEvaluateIdentifier(identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool() && ChildId == CHILDID_SELF)
                        return new UiDomString("msaa");
                    break;
                case "enabled":
                    depends_on.Add((Element, new IdentifierExpression("msaa_state")));
                    if (StateKnown)
                        return UiDomBoolean.FromBool((State & STATE_SYSTEM_UNAVAILABLE) == 0);
                    break;
                case "disabled":
                    depends_on.Add((Element, new IdentifierExpression("msaa_state")));
                    if (StateKnown)
                        return UiDomBoolean.FromBool((State & STATE_SYSTEM_UNAVAILABLE) != 0);
                    break;
                case "visible":
                    depends_on.Add((Element, new IdentifierExpression("msaa_state")));
                    if (StateKnown)
                        return UiDomBoolean.FromBool((State & (STATE_SYSTEM_INVISIBLE|STATE_SYSTEM_OFFSCREEN)) != STATE_SYSTEM_INVISIBLE);
                    break;
                case "do_default_action":
                    depends_on.Add((Element, new IdentifierExpression("msaa_default_action")));
                    if (DefaultActionKnown && !(DefaultAction is null))
                        return Element.EvaluateIdentifier("msaa_do_default_action", Element.Root, depends_on);
                    break;
                case "role":
                case "control_type":
                    depends_on.Add((Element, new IdentifierExpression("msaa_role")));
                    if (RoleKnown && Role != ROLE_SYSTEM_CLIENT)
                        return RoleAsValue;
                    break;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
            {
                return Element.EvaluateIdentifier(aliased, Element.Root, depends_on);
            }
            if (msaa_name_to_role.TryGetValue(identifier, out var role))
            {
                depends_on.Add((Element, new IdentifierExpression("msaa_role")));
                if (RoleKnown)
                {
                    return UiDomBoolean.FromBool(Role == role);
                }
            }
            if (msaa_name_to_state.TryGetValue(identifier, out var state))
            {
                depends_on.Add((Element, new IdentifierExpression("msaa_state")));
                if (StateKnown)
                    return UiDomBoolean.FromBool((State & state) != 0);
            }
            return RootHwnd.ChildEvaluateIdentifierLate(identifier, depends_on);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "msaa_role":
                        {
                            if (!RoleKnown && !_fetchingRole)
                            {
                                _fetchingRole = true;
                                Utils.RunTask(FetchRole());
                            }
                            return true;
                        }
                    case "msaa_state":
                        {
                            _watchingState = true;
                            if (!StateKnown)
                                Utils.RunTask(FetchState());
                            if (_pollingState)
                                Element.PollProperty(new IdentifierExpression("msaa_state"), PollState, DEFAULT_POLL_INTERVAL);
                            return true;
                        }
                    case "msaa_name":
                        {
                            _watchingName = true;
                            if (!NameKnown)
                                Utils.RunTask(FetchName());
                            return true;
                        }
                    case "msaa_location":
                        {
                            _watchingLocation = true;
                            if (!LocationKnown)
                                Utils.RunTask(FetchLocation());
                            if (_pollingLocation)
                                Element.PollProperty(new IdentifierExpression("msaa_location"), PollLocation, DEFAULT_POLL_INTERVAL);
                            return true;
                        }
                    case "msaa_default_action":
                        {
                            _watchingDefaultAction = true;
                            if (!DefaultActionKnown)
                                Utils.RunTask(FetchDefaultAction());
                            return true;
                        }
                }
            }
            return false;
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "msaa_state":
                        _watchingState = false;
                        Element.EndPollProperty(new IdentifierExpression("msaa_state"));
                        return true;
                    case "msaa_name":
                        _watchingName = false;
                        return true;
                    case "msaa_location":
                        _watchingLocation = false;
                        Element.EndPollProperty(new IdentifierExpression("msaa_location"));
                        return true;
                    case "msaa_default_action":
                        _watchingDefaultAction = false;
                        return true;
                }
            }
            return false;
        }

        private async Task FetchDefaultAction()
        {
            var old_change_count = _defaultActionChangeCount;
            string new_value;

            try
            {
                new_value = await CommandThread.OnBackgroundThread(() =>
                {
                    if (old_change_count != _defaultActionChangeCount)
                        return null;
                    return IAccessible.get_accDefaultAction(ChildId);
                }, CommandThreadPriority.Query);
            }
            catch (Exception e)
            {
                if (IsExpectedException(e))
                    return;
                throw;
            }

            if (old_change_count != _defaultActionChangeCount)
                // possibly stale value
                return;

            if (!DefaultActionKnown || DefaultAction != new_value)
            {
                DefaultActionKnown = true;
                DefaultAction = new_value;

                Element.PropertyChanged("msaa_default_action", new_value ?? "false");
            }
        }

        private async Task FetchState(bool polling)
        {
            var old_change_count = _stateChangeCount;
            int? new_state;

            try
            {
                new_state = await CommandThread.OnBackgroundThread(() =>
                {
                    if (old_change_count != _stateChangeCount)
                        return null;
                    return IAccessible.get_accState(ChildId) as int?;
                }, polling ? CommandThreadPriority.Poll : CommandThreadPriority.Query);
            }
            catch (Exception e)
            {
                if (IsExpectedException(e))
                    return;
                throw;
            }

            if (new_state is null)
                // should always be int
                return;

            if (old_change_count != _stateChangeCount)
                // possibly stale value
                return;

            if (!StateKnown || State != new_state)
            {
                StateKnown = true;
                State = (int)new_state;

                if (Element.MatchesDebugCondition())
                {
                    Utils.DebugWriteLine($"{Element}.msaa_state: 0x{State:X} ({GetStateNames(State)})");
                }

                Element.PropertyChanged("msaa_state");
            }
        }

        private Task FetchState()
        {
            return FetchState(false);
        }

        private Task PollState()
        {
            return FetchState(true);
        }

        private async Task FetchName()
        {
            var old_change_count = _nameChangeCount;
            string new_name;

            try
            {
                new_name = await CommandThread.OnBackgroundThread(() =>
                {
                    if (old_change_count != _nameChangeCount)
                        return null;
                    return IAccessible.get_accName(ChildId);
                }, CommandThreadPriority.Query);
            }
            catch (Exception e)
            {
                if (IsExpectedException(e))
                    return;
                throw;
            }

            if (new_name is null)
                return;

            if (old_change_count != _nameChangeCount)
                return;

            if (!NameKnown || Name != new_name)
            {
                NameKnown = true;
                Name = new_name;

                Element.PropertyChanged("msaa_name", new_name);
            }
        }

        private async Task FetchLocation(bool polling)
        {
            var old_change_count = _locationChangeCount;
            RECT new_location;

            try
            {
                new_location = await CommandThread.OnBackgroundThread(() =>
                {
                    if (old_change_count != _locationChangeCount)
                        return default;
                    IAccessible.accLocation(out int x, out int y, out int width, out int height, ChildId);
                    var result = new RECT();
                    result.left = x;
                    result.top = y;
                    result.right = x + width;
                    result.bottom = y + height;
                    return result;
                }, polling ? CommandThreadPriority.Poll : CommandThreadPriority.Query);
            }
            catch (Exception e)
            {
                if (IsExpectedException(e))
                    return;
                throw;
            }

            if (old_change_count != _locationChangeCount)
                // possibly stale value
                return;

            new_location = RootHwnd.DpiAdjustScreenRect(new_location);

            if (!LocationKnown || !Location.Equals(new_location))
            {
                LocationKnown = true;
                Location = new_location;

                if (Element.MatchesDebugCondition())
                {
                    Utils.DebugWriteLine($"{Element}.msaa_location: {new_location.left},{new_location.top} {new_location.width}x{new_location.height}");
                }

                Element.PropertyChanged("msaa_location");
            }
        }

        private Task FetchLocation()
        {
            return FetchLocation(false);
        }

        private Task PollLocation()
        {
            return FetchLocation(true);
        }

        void WatchChildren(RecurseMethod method)
        {
            if (method == _recurseMethod)
                return;
            _recurseMethod = method;
            if (method == RecurseMethod.None)
            {
                Element.UnsetRecurseMethodProvider(this);
            }
            else
            {
                if (Element.MatchesDebugCondition())
                    Utils.DebugWriteLine($"WatchChildren for {Element} (msaa {method})");
                Element.SetRecurseMethodProvider(this);
                Utils.RunTask(PollChildren());
            }
        }

        private async Task PollChildren()
        {
            List<ElementIdentifier> children;
            try
            {
                children = await CommandThread.OnBackgroundThread(() =>
                {
                    List<ElementIdentifier> result;
                    var count = 0;
                    if (_recurseMethod != RecurseMethod.None && _recurseMethod != RecurseMethod.accNavigate)
                        count = IAccessible.get_accChildCount();
                    switch (_recurseMethod)
                    {
                        case RecurseMethod.None:
                        default:
                            result = null;
                            break;
                        case RecurseMethod.IEnumVARIANT:
                            result = GetChildrenEnumVariantBackground(count);
                            break;
                        case RecurseMethod.accChild:
                            result = GetChildrenAccChildBackground(count);
                            break;
                        case RecurseMethod.accNavigate:
                            result = GetChildrenAccNavigateBackground();
                            break;
                        case RecurseMethod.Auto:
                            result = GetChildrenEnumVariantBackground(count);
                            if (result is null)
                                result = GetChildrenAccChildBackground(count);
                            break;
                    }
                    return result;
                }, CommandThreadPriority.Query);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }

            if (children is null || _recurseMethod == RecurseMethod.None)
                return;

            for (int i = children.Count - 1; i >= 0; i--)
            {
                // Remove any duplicate elements
                var existing_element = Connection.LookupElement(children[i]);
                if (!(existing_element is null) && existing_element.Parent != Element)
                    children.RemoveAt(i);
            }

            Element.SyncRecurseMethodChildren(children, Connection.GetElementName,
                Connection.CreateElement);
        }

        private List<ElementIdentifier> GetChildrenAccNavigateBackground()
        {
            var result = new List<ElementIdentifier>();

            object child = IAccessible.accNavigate(NAVDIR_FIRSTCHILD, ChildId);
            IAccessible base_acc = IAccessible;

            while (!(child is null) && !(child is int c && c == CHILDID_SELF))
            {
                var child_ei = ElementIdFromVariantBackground(child, base_acc);
                result.Add(child_ei);

                base_acc = child_ei.acc;
                child = base_acc.accNavigate(NAVDIR_NEXT, child_ei.child_id);
            }

            return result;
        }

        unsafe private List<ElementIdentifier> GetChildrenEnumVariantBackground(int count)
        {
            if (count == 0)
                return new List<ElementIdentifier>(0);

            IEnumVARIANT e;
            try
            {
                e = (IEnumVARIANT)IAccessible;
            }
            catch (InvalidCastException)
            {
                return null;
            }

            int res = e.Reset();
            Marshal.ThrowExceptionForHR(res);

            object[] variants = new object[count];
            res = e.Next(count, variants, new IntPtr(&count));
            Marshal.ThrowExceptionForHR(res);

            List<ElementIdentifier> result = new List<ElementIdentifier>(count);
            HashSet<object> seen = new HashSet<object>(count);
            foreach (var v in variants)
            {
                if (v is int i && i == CHILDID_SELF)
                    continue;
                if (!seen.Add(v))
                    continue;
                result.Add(ElementIdFromVariantBackground(v));
            }

            return result;
        }

        private List<ElementIdentifier> GetChildrenAccChildBackground(int count)
        {
            var result = new List<ElementIdentifier>(count);

            var seen = new HashSet<object>(count);
            for (int i = 0; i < count; i++)
            {
                var v = IAccessible.get_accChild(i + 1);
                if (v is null)
                {
                    // simple child - use parent IAccessible
                    result.Add(ElementIdFromVariantBackground(i + 1));
                    continue;
                }
                if (!seen.Add(v))
                    continue;
                result.Add(ElementIdFromVariantBackground(v));
            }

            return result;
        }

        public static bool UiaProviderFromIAccessibleBackground(IAccessible obj, out IRawElementProviderSimple uiaprov)
        {
            // Returns true if this is a bridged UIA element.
            // May return false and a non-null provider if it's a native IAccessible with a UIA provider.

            IServiceProvider sp = obj as IServiceProvider;

            if (!(sp is null))
            {
                try
                {
                    Guid sid = SID_IRawElemWrap, iid = IID_IUnknown;
                    var raw_prov = sp.QueryService(ref sid, ref iid);
                    if (raw_prov != IntPtr.Zero)
                    {
                        object obj_prov = Marshal.GetObjectForIUnknown(raw_prov);
                        uiaprov = obj_prov as IRawElementProviderSimple;
                        return true;
                    }
                }
                catch (NullReferenceException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (NotImplementedException)
                {
                }
                catch (InvalidCastException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (COMException)
                {
                }

                try
                {
                    Guid sid = IID_IAccessibleEx;
                    var raw_accex = sp.QueryService(ref sid, ref sid);
                    if (raw_accex != IntPtr.Zero)
                    {
                        object obj_accex = Marshal.GetObjectForIUnknown(raw_accex);
                        uiaprov = obj_accex as IRawElementProviderSimple;
                        return false;
                    }
                }
                catch (InvalidOperationException)
                {
                }
                catch (NotImplementedException)
                {
                }
                catch (InvalidCastException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (COMException)
                {
                }
            }

            uiaprov = null;
            return false;
        }

        public static ElementIdentifier ElementIdFromVariantBackground(object variant, IAccessible base_acc, IntPtr root_hwnd)
        {
            ElementIdentifier result = default;
            result.root_hwnd = root_hwnd;
            IAccessible acc;
            if (variant is int childid)
            {
                var child = childid == CHILDID_SELF ? base_acc : base_acc.get_accChild(childid);
                if (child is null)
                {
                    // Child without its own IAccessible
                    result.acc = base_acc;
                    result.child_id = childid;
                    return result;
                }
                acc = (IAccessible)child;
            }
            else if (variant is IAccessible ia)
                acc = ia;
            else if (variant is null)
                throw new InvalidOperationException($"variant is VT_EMPTY");
            else
                throw new InvalidOperationException($"wrong type {variant.GetType()}");

            IOleWindow oleWindow = acc as IOleWindow;
            if (!(oleWindow is null))
            {
                try
                {
                    IntPtr our_hwnd = oleWindow.GetWindow();
                    if (our_hwnd != IntPtr.Zero && our_hwnd != root_hwnd)
                    {
                        result.root_hwnd = our_hwnd;
                        result.is_root_hwnd = true;
                        return result;
                    }
                }
                catch (Exception e)
                {
                    if (!IsExpectedException(e))
                        throw;
                }
            }

            UiaProviderFromIAccessibleBackground(acc, out var uiaprov);

            result.acc = acc;
            result.prov = uiaprov;

            IAccessible2 acc2 = QueryIAccessible2(acc);

            if (!(acc2 is null))
            {
                if (acc2.windowHandle != root_hwnd)
                {
                    result.root_hwnd = acc2.windowHandle;
                    result.is_root_hwnd = true;
                    return result;
                }

                result.acc2 = acc2;
                result.acc2_uniqueId = acc2.uniqueID;
                return result;
            }

            if (!(uiaprov is null)) {
                var fragment = uiaprov as IRawElementProviderFragment;
                if (!(fragment is null))
                {
                    result.runtime_id = fragment.GetRuntimeId();
                    if (!(result.runtime_id is null))
                        return result;
                }
            }

            // Identify object by IUnknown pointer
            result.punk = Marshal.GetIUnknownForObject(acc);
            Marshal.Release(result.punk);

            return result;
        }

        private ElementIdentifier ElementIdFromVariantBackground(object variant, IAccessible base_acc)
        {
            return ElementIdFromVariantBackground(variant, base_acc, RootHwnd.Hwnd);
        }

        private ElementIdentifier ElementIdFromVariantBackground(object variant)
        {
            return ElementIdFromVariantBackground(variant, IAccessible);
        }

        public override string[] GetTrackedProperties()
        {
            return tracked_properties;
        }

        public override void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            switch (name)
            {
                case "recurse_method":
                    {
                        RecurseMethod new_method;
                        if (new_value is UiDomString s)
                        {
                            switch (s.Value)
                            {
                                case "msaa":
                                    new_method = RecurseMethod.Auto;
                                    break;
                                case "msaa_enum":
                                    new_method = RecurseMethod.IEnumVARIANT;
                                    break;
                                case "msaa_child":
                                    new_method = RecurseMethod.accChild;
                                    break;
                                case "msaa_navigate":
                                    new_method = RecurseMethod.accNavigate;
                                    break;
                                default:
                                    new_method = RecurseMethod.None;
                                    break;
                            }
                        }
                        else
                            new_method = RecurseMethod.None;
                        if (ChildId != CHILDID_SELF && new_method != RecurseMethod.accNavigate)
                            // Only accNavigate makes sense for simple children
                            new_method = RecurseMethod.None;
                        WatchChildren(new_method);
                        break;
                    }
                case "poll_msaa_state":
                    {
                        var new_polling_state = new_value.ToBool();
                        if (new_polling_state != _pollingState)
                        {
                            _pollingState = new_polling_state;
                            if (_watchingState && _pollingState)
                                Element.PollProperty(new IdentifierExpression("msaa_state"), PollState, DEFAULT_POLL_INTERVAL);
                            else
                                Element.EndPollProperty(new IdentifierExpression("msaa_state"));
                        }
                        break;
                    }
                case "poll_msaa_location":
                    {
                        var new_polling_location = new_value.ToBool();
                        if (new_polling_location != _pollingLocation)
                        {
                            _pollingLocation = new_polling_location;
                            if (_watchingLocation && _pollingLocation)
                                Element.PollProperty(new IdentifierExpression("msaa_location"), PollLocation, DEFAULT_POLL_INTERVAL);
                            else
                                Element.EndPollProperty(new IdentifierExpression("msaa_location"));
                        }
                        break;
                    }
            }
            base.TrackedPropertyChanged(element, name, new_value);
        }

        private async Task FetchRole()
        {
            object role_obj;
            try
            {
                role_obj = await CommandThread.OnBackgroundThread(() =>
                {
                    return IAccessible.get_accRole(ChildId);
                }, CommandThreadPriority.Query);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (role_obj is null)
            {
                Utils.DebugWriteLine($"WARNING: accRole returned NULL for {Element}");
            }
            else if (!(role_obj is int role))
            {
                Utils.DebugWriteLine($"WARNING: accRole returned {role_obj.GetType()} instead of int for {Element}");
            }
            else
            {
                Role = role;
                RoleKnown = true;
                if (Element.MatchesDebugCondition())
                    Utils.DebugWriteLine($"{Element}.msaa_role: {RoleAsValue}");
                Element.PropertyChanged("msaa_role");
            }
        }

        public void MsaaStateChange()
        {
            _stateChangeCount++;
            if (_watchingState)
                Utils.RunTask(FetchState());
            else
                StateKnown = false;
        }

        public void MsaaNameChange()
        {
            _nameChangeCount++;
            if (_watchingName)
                Utils.RunTask(FetchName());
            else
                NameKnown = false;
        }

        internal void MsaaAncestorLocationChange()
        {
            _locationChangeCount++;
            if (_watchingLocation)
                Utils.RunTask(FetchLocation());
            else
                LocationKnown = false;
        }

        public void MsaaDefaultActionChange()
        {
            _defaultActionChangeCount++;
            if (_watchingDefaultAction)
                Utils.RunTask(FetchDefaultAction());
            else
                DefaultActionKnown = false;
        }

        private async Task DoDefaultActionAsync(UiDomRoutineAsync obj)
        {
            try
            {
                await CommandThread.OnBackgroundThread(() =>
                {
                    IAccessible.accDoDefaultAction(ChildId);
                }, CommandThreadPriority.User);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
        }

        public async override Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            if (LocationKnown)
                return (true, Location.left + Location.width / 2, Location.top + Location.height / 2);
            try
            {
                return await CommandThread.OnBackgroundThread(() =>
                {
                    IAccessible.accLocation(out var left, out var top, out var width, out var height, ChildId);
                    return (true, left + width / 2, top + height / 2);
                }, CommandThreadPriority.User);
            }
            catch (Exception e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
            return (false, 0, 0);
        }

        internal void MsaaChildrenReordered()
        {
            if (_recurseMethod != RecurseMethod.None)
                Utils.RunTask(PollChildren());
        }
    }
}
