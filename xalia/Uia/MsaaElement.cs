using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Uia
{
    internal class MsaaElement : UiDomElement
    {
        private static string[] tracked_properties = { "recurse", "poll_msaa_state", "msaa_navigate_children" };
        private static readonly Dictionary<string, string> property_aliases;

        static MsaaElement()
        {
            string[] aliases = {
                "hwnd", "msaa_hwnd",
                "pid", "msaa_pid",
                "child_id", "msaa_child_id",
                "application_name", "msaa_process_name",
                "process_name", "msaa_process_name",
                "role", "msaa_role",
                "control_type", "msaa_role",
                "state", "msaa_state",
                "x", "msaa_x",
                "y", "msaa_y",
                "width", "msaa_width",
                "height", "msaa_height",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i=0; i<aliases.Length; i+=2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
            msaa_role_to_enum = new UiDomEnum[msaa_role_names.Length];
            msaa_name_to_role = new Dictionary<string, int>();
            for (int i = 0; i < msaa_role_names.Length; i++)
            {
                string name = msaa_role_names[i];
                string[] names;
                if (name.Contains("_"))
                    names = new[] { name, name.Replace("_", "") };
                else
                    names = new[] { name };
                foreach (string rolename in names)
                    msaa_name_to_role[rolename] = i;
                msaa_role_to_enum[i] = new UiDomEnum(names);
            }

            msaa_name_to_state = new Dictionary<string, int>();
            int flag = 1;
            foreach (var name in msaa_state_names)
            {
                msaa_name_to_state[name] = flag;
                flag <<= 1;
            }
            msaa_name_to_state["disabled"] = STATE_SYSTEM_UNAVAILABLE;
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

        internal static readonly UiDomEnum[] msaa_role_to_enum;
        internal static readonly Dictionary<string, int> msaa_name_to_role;
        
        public MsaaElement(MsaaElementWrapper wrapper, UiaConnection root) : base(root)
        {
            ElementWrapper = wrapper;
            Root = root;
            RegisterTrackedProperties(tracked_properties);
        }

        public override string DebugId => ElementWrapper.UniqueId;

        public MsaaElementWrapper ElementWrapper { get; }
        public new UiaConnection Root { get; }

        private string _processName;

        private int _role;
        private bool _roleKnown;
        private bool _fetchingRole;

        private int _state;
        private bool _stateKnown;
        private bool _fetchingState;
        private int _stateChangeCount;
        private bool _watchingState;
        private bool _pollingState;

        private bool _watchingChildren;
        private bool _navigateChildren;
        private bool _childrenKnown;

        private int _locationX;
        private int _locationY;
        private int _locationWidth;
        private int _locationHeight;
        private bool _locationKnown;
        private bool _fetchingLocation;

        internal static string[] msaa_state_names =
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
            "protected",
        };

        internal static Dictionary<string, int> msaa_name_to_state;

        protected override void SetAlive(bool value)
        {
            if (value)
            {
                Root.msaa_elements_by_id[ElementWrapper.UniqueId] = this;
            }
            else
            {
                _watchingState = false;
                Root.msaa_elements_by_id.Remove(ElementWrapper.UniqueId);
            }
            base.SetAlive(value);
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            UiDomValue value;

            if (property_aliases.TryGetValue(id, out string aliased))
            {
                value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;
                id = aliased;
            }

            switch (id)
            {
                case "is_msaa_element":
                    return UiDomBoolean.True;
                case "msaa_hwnd":
                    return new UiDomInt((int)ElementWrapper.Hwnd);
                case "msaa_child_id":
                    return new UiDomInt(ElementWrapper.ChildId);
                case "msaa_pid":
                    return new UiDomInt(ElementWrapper.Pid);
                case "msaa_process_name":
                    try
                    {
                        if (_processName is null)
                        {
                            using (var process = Process.GetProcessById(ElementWrapper.Pid))
                                _processName = process.ProcessName;
                        }
                    }
                    catch (ArgumentException)
                    {
                        return UiDomUndefined.Instance;
                    }
                    return new UiDomString(_processName);
                case "msaa_role":
                    depends_on.Add((this, new IdentifierExpression(id)));
                    if (_roleKnown)
                        return MsaaRoleToValue(_role);
                    return UiDomUndefined.Instance;
                case "msaa_state":
                    depends_on.Add((this, new IdentifierExpression(id)));
                    if (_stateKnown)
                        return new MsaaState(_state);
                    return UiDomUndefined.Instance;
                case "msaa_x":
                    depends_on.Add((this, new IdentifierExpression("msaa_location")));
                    if (_locationKnown)
                        return new UiDomInt(_locationX);
                    return UiDomUndefined.Instance;
                case "msaa_y":
                    depends_on.Add((this, new IdentifierExpression("msaa_location")));
                    if (_locationKnown)
                        return new UiDomInt(_locationY);
                    return UiDomUndefined.Instance;
                case "msaa_width":
                    depends_on.Add((this, new IdentifierExpression("msaa_location")));
                    if (_locationKnown)
                        return new UiDomInt(_locationWidth);
                    return UiDomUndefined.Instance;
                case "msaa_height":
                    depends_on.Add((this, new IdentifierExpression("msaa_location")));
                    if (_locationKnown)
                        return new UiDomInt(_locationHeight);
                    return UiDomUndefined.Instance;
            }

            value = base.EvaluateIdentifierCore(id, root, depends_on);
            if (!value.Equals(UiDomUndefined.Instance))
                return value;

            switch (id)
            {
                case "visible":
                    depends_on.Add((this, new IdentifierExpression("msaa_state")));
                    if (_stateKnown)
                        return UiDomBoolean.FromBool((_state & STATE_SYSTEM_INVISIBLE) == 0);
                    return UiDomUndefined.Instance;
                case "enabled":
                    depends_on.Add((this, new IdentifierExpression("msaa_state")));
                    if (_stateKnown)
                        return UiDomBoolean.FromBool((_state & STATE_SYSTEM_UNAVAILABLE) == 0);
                    return UiDomUndefined.Instance;
            }

            if (msaa_name_to_role.TryGetValue(id, out var role))
            {
                depends_on.Add((this, new IdentifierExpression("msaa_role")));
                if (_roleKnown)
                {
                    return UiDomBoolean.FromBool(_role == role);
                }
            }

            if (msaa_name_to_state.TryGetValue(id, out var state))
            {
                depends_on.Add((this, new IdentifierExpression("msaa_state")));
                if (_stateKnown)
                {
                    return UiDomBoolean.FromBool((_state & state) != 0);
                }
            }

            return value;
        }

        protected override void DumpProperties()
        {
            if (ElementWrapper.Hwnd != IntPtr.Zero)
                Utils.DebugWriteLine($"  msaa_hwnd: {ElementWrapper.Hwnd}");
            if (ElementWrapper.ChildId != 0)
                Utils.DebugWriteLine($"  msaa_child_id: {ElementWrapper.ChildId}");
            Utils.DebugWriteLine($"  msaa_pid: {ElementWrapper.Pid}");
            if (!(_processName is null))
                Utils.DebugWriteLine($"  msaa_process_name: {_processName}");
            if (_roleKnown)
                Utils.DebugWriteLine($"  msaa_role: {MsaaRoleToValue(_role)}");
            if (_stateKnown)
                Utils.DebugWriteLine($"  msaa_state: {new MsaaState(_state)}");
            if (_locationKnown)
            {
                Utils.DebugWriteLine($"  msaa_x: {_locationX}");
                Utils.DebugWriteLine($"  msaa_y: {_locationY}");
                Utils.DebugWriteLine($"  msaa_width: {_locationWidth}");
                Utils.DebugWriteLine($"  msaa_height: {_locationHeight}");
            }
            base.DumpProperties();
        }

        internal static UiDomValue MsaaRoleToValue(int role)
        {
            if (role >= 0 && role < msaa_role_to_enum.Length)
                return msaa_role_to_enum[role];
            else
                return new UiDomInt(role);
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "msaa_role":
                        {
                            if (!_roleKnown && !_fetchingRole)
                            {
                                _fetchingRole = true;
                                Utils.RunTask(FetchRole());
                            }
                            break;
                        }
                    case "msaa_state":
                        {
                            _watchingState = true;
                            if (!_stateKnown && !_fetchingState)
                            {
                                _fetchingState = true;
                                Utils.RunTask(FetchState(false));
                            }
                            if (_pollingState)
                            {
                                PollProperty(expression, PollState, 200);
                            }
                            break;
                        }
                    case "msaa_location":
                        {
                            if (!_locationKnown && !_fetchingLocation)
                            {
                                _fetchingLocation = true;
                                Utils.RunTask(FetchLocation());
                            }
                            break;
                        }
                }
            }
            base.WatchProperty(expression);
        }

        protected override void UnwatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "msaa_state":
                        _watchingState = false;
                        EndPollProperty(expression);
                        break;
                }
            }
            base.UnwatchProperty(expression);
        }

        private async Task FetchState(bool polling)
        {
            object state_obj;
            int change_count = _stateChangeCount;
            try
            {
                state_obj = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    return ElementWrapper.Accessible.accState[ElementWrapper.ChildId];
                }, polling ? ElementWrapper.Pid + 2 : ElementWrapper.Pid);
            }
            catch (Exception e)
            {
                if (!UiaElement.IsExpectedException(e))
                    throw;
                return;
            }

            if (change_count != _stateChangeCount)
                return;

            if (state_obj is null)
            {
                Utils.DebugWriteLine($"WARNING: accState returned NULL for {this}");
            }
            else if (!(state_obj is int state))
            {
                Utils.DebugWriteLine($"WARNING: accState returned {state_obj.GetType()} instead of int for {this}");
            }
            else if (!_stateKnown || _state != state)
            {
                _stateKnown = true;
                _state = state;
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.msaa_state: {new MsaaState(state)}");
                PropertyChanged("msaa_state");
            }
        }

        private Task PollState() => FetchState(true);

        private async Task FetchRole()
        {
            object role_obj;
            try
            {
                role_obj = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    return ElementWrapper.Accessible.accRole[ElementWrapper.ChildId];
                }, ElementWrapper);
            }
            catch (Exception e)
            {
                if (!UiaElement.IsExpectedException(e))
                    throw;
                return;
            }
            if (role_obj is null)
            {
                Utils.DebugWriteLine($"WARNING: accRole returned NULL for {this}");
            }
            else if (!(role_obj is int role))
            {
                Utils.DebugWriteLine($"WARNING: accRole returned {role_obj.GetType()} instead of int for {this}");
            }
            else
            {
                _role = role;
                _roleKnown = true;
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.msaa_role: {MsaaRoleToValue(_role)}");
                PropertyChanged("msaa_role");
            }
        }

        private async Task FetchLocation()
        {
            (int, int, int, int) location;
            try
            {
                location = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    ElementWrapper.Accessible.accLocation(out var x, out var y, out var width, out var height,
                        ElementWrapper.ChildId);
                    return (x, y, width, height);
                }, ElementWrapper.Pid);
            }
            catch (Exception e)
            {
                if (!UiaElement.IsExpectedException(e))
                    throw;
                return;
            }

            _locationKnown = true;
            _locationX = location.Item1;
            _locationY = location.Item2;
            _locationWidth = location.Item3;
            _locationHeight = location.Item4;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.msaa_location: {location}");
            PropertyChanged("msaa_location");
        }

        internal void MsaaStateChange()
        {
            _stateChangeCount++;
            if (_watchingState)
            {
                Utils.RunTask(FetchState(false));
            }
            else if (_stateKnown)
            {
                _stateKnown = false;
            }
        }

        protected override void TrackedPropertyChanged(string name, UiDomValue new_value)
        {
            switch (name)
            {
                case "recurse":
                    if (new_value.ToBool())
                    {
                        WatchChildren();
                    }
                    else
                    {
                        UnwatchChildren();
                    }
                    break;
                case "poll_msaa_state":
                    if (new_value.ToBool() != _pollingState)
                    {
                        _pollingState = new_value.ToBool();
                        if (_pollingState)
                        {
                            if (_watchingState)
                                PollProperty(new IdentifierExpression("msaa_state"), PollState, 200);
                        }
                        else
                            EndPollProperty(new IdentifierExpression("msaa_state"));
                    }
                    break;
                case "msaa_navigate_children":
                    if (new_value.ToBool() != _navigateChildren)
                    {
                        _navigateChildren = new_value.ToBool();
                        if (_watchingChildren)
                            Utils.RunTask(FetchChildren());
                    }
                    break;
            }
            base.TrackedPropertyChanged(name, new_value);
        }

        private void WatchChildren()
        {
            if (_watchingChildren)
                return;
            _watchingChildren = true;

            if (!_childrenKnown)
                Utils.RunTask(FetchChildren());
        }

        private async Task FetchChildren()
        {
            if (!_watchingChildren)
                return;

            List<MsaaElementWrapper> children = await GetChildren(!_childrenKnown || Children.Count == 0);

            if (!_watchingChildren)
                return;

            if (children is null)
            {
                RemoveAllChildren();
                return;
            }

            // Ignore any duplicate children
            HashSet<string> seen_children = new HashSet<string>();
            int i = 0;
            while (i < children.Count)
            {
                if (!seen_children.Add(children[i].UniqueId))
                {
                    children.RemoveAt(i);
                    continue;
                }
                i++;
            }

            // First remove any existing children that are missing or out of order
            i = 0;
            foreach (var new_child in children)
            {
                if (!Children.Exists((UiDomElement element) => ElementMatches(element, new_child)))
                    continue;
                while (!ElementMatches(Children[i], new_child))
                {
                    RemoveChild(i);
                }
                i++;
            }

            // Remove any remaining missing children
            while (i < Children.Count && Children[i] is MsaaElement)
                RemoveChild(i);

            // Add any new children
            i = 0;
            foreach (var new_child in children)
            {
                if (Children.Count <= i || !ElementMatches(Children[i], new_child))
                {
                    if (Root.msaa_elements_by_id.ContainsKey(new_child.UniqueId))
                    {
                        // Child element is a duplicate of another element somewhere in the tree.
                        continue;
                    }
                    AddChild(i, new MsaaElement(new_child, Root));
                }
                i += 1;
            }

            _childrenKnown = true;
        }

        private bool ElementMatches(UiDomElement element, MsaaElementWrapper wrapper)
        {
            return element is MsaaElement msaa && msaa.ElementWrapper.UniqueId == wrapper.UniqueId;
        }

        private List<MsaaElementWrapper> GetChildrenBackground_accChild(bool assumeUnique)
        {
            var acc = ElementWrapper.Accessible;

            int child_count = acc.accChildCount;

            if (child_count == 0)
                return null;

            var result = new List<MsaaElementWrapper>(child_count);

            for (int i=1; i <= child_count;  i++)
            {
                object child = acc.accChild[i];

                if (ElementWrapper.FromVariantBackground(child, assumeUnique, out var child_wrapper))
                {
                    result.Add(child_wrapper);
                }
                else
                    return null;
            }
            return result;
        }

        private List<MsaaElementWrapper> GetChildrenBackground_accNavigate(bool assumeUnique)
        {
            var wrapper = ElementWrapper;
            MsaaElementWrapper child_wrapper;

            object child = wrapper.Accessible.accNavigate(NAVDIR_FIRSTCHILD, wrapper.ChildId);
            if (child is null || !wrapper.FromVariantBackground(child, assumeUnique, out child_wrapper))
                return null;

            var result = new List<MsaaElementWrapper> { child_wrapper };
            wrapper = child_wrapper;

            while (true)
            {
                child = wrapper.Accessible.accNavigate(NAVDIR_NEXT, wrapper.ChildId); 
                if (child is null || !wrapper.FromVariantBackground(child, assumeUnique, out child_wrapper))
                    break;

                result.Add(child_wrapper);
                wrapper = child_wrapper;
            }

            return result;
        }

        private async Task<List<MsaaElementWrapper>> GetChildren(bool assumeUnique)
        {
            try
            {
                if (_navigateChildren)
                    return await Root.CommandThread.OnBackgroundThread(() =>
                    {
                        return GetChildrenBackground_accNavigate(assumeUnique);
                    },
                    ElementWrapper);
                if (ElementWrapper.ChildId == CHILDID_SELF)
                    return await Root.CommandThread.OnBackgroundThread(() => {
                        return GetChildrenBackground_accChild(assumeUnique);
                    },
                    ElementWrapper);
            }
            catch (Exception e)
            {
                if (UiaElement.IsExpectedException(e))
                    return null;
                throw;
            }
            throw new NotImplementedException(); // try IEnumVARIANT
        }

        private void RemoveAllChildren()
        {
            for (int i=Children.Count-1; i>=0; i--)
            {
                if (Children[i] is MsaaElement)
                    RemoveChild(i);
            }
        }

        private void UnwatchChildren()
        {
            if (!_watchingChildren)
                return;
            _watchingChildren = false;

            RemoveAllChildren();
            _childrenKnown = false;
        }
    }
}
