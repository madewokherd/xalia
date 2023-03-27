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
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i=0; i<aliases.Length; i+=2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
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
        
        public MsaaElement(MsaaElementWrapper wrapper, UiaConnection root) : base(root)
        {
            ElementWrapper = wrapper;
            Root = root;
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
        private CancellationTokenSource _fetchingStateCancel;

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
            if (!value)
            {
                _fetchingState = false;
                if (!(_fetchingStateCancel is null))
                {
                    _fetchingStateCancel.Cancel();
                    _fetchingStateCancel = null;
                }
            }
            base.SetAlive(value);
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (property_aliases.TryGetValue(id, out string aliased))
            {
                var value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;
                id = aliased;
            }

            if (UiaElement.msaa_name_to_role.TryGetValue(id, out var role))
            {
                var value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;

                depends_on.Add((this, new IdentifierExpression("msaa_role")));
                if (_roleKnown)
                {
                    return UiDomBoolean.FromBool(_role == role);
                }
            }

            if (msaa_name_to_state.TryGetValue(id, out var state))
            {
                var value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;

                depends_on.Add((this, new IdentifierExpression("msaa_state")));
                if (_stateKnown)
                {
                    return UiDomBoolean.FromBool((_state & state) != 0);
                }
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
                    {
                        if (_role >= 0 && _role < UiaElement.msaa_role_to_enum.Length)
                        {
                            return UiaElement.msaa_role_to_enum[_role];
                        }
                        return new UiDomInt(_role);
                    }
                    return UiDomUndefined.Instance;
                case "msaa_state":
                    depends_on.Add((this, new IdentifierExpression(id)));
                    if (_stateKnown)
                        return new MsaaState(_state);
                    return UiDomUndefined.Instance;
                case "visible":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;

                        depends_on.Add((this, new IdentifierExpression("msaa_state")));
                        if (_stateKnown)
                            return UiDomBoolean.FromBool((_state & STATE_SYSTEM_INVISIBLE) == 0);
                        return UiDomUndefined.Instance;
                    }
                case "enabled":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;

                        depends_on.Add((this, new IdentifierExpression("msaa_state")));
                        if (_stateKnown)
                            return UiDomBoolean.FromBool((_state & STATE_SYSTEM_UNAVAILABLE) == 0);
                        return UiDomUndefined.Instance;
                    }
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
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
            {
                if (_role >= 0 && _role < UiaElement.msaa_role_to_enum.Length)
                    Utils.DebugWriteLine($"  msaa_role: {UiaElement.msaa_role_to_enum[_role]}");
                else
                    Utils.DebugWriteLine($"  msaa_role: {_role}");
            }
            if (_stateKnown)
            {
                Utils.DebugWriteLine($"  msaa_state: {new MsaaState(_state)}");
            }
            base.DumpProperties();
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
                            if (!_stateKnown && !_fetchingState)
                            {
                                _fetchingState = true;
                                Utils.RunTask(FetchState());
                            }
                            break;
                        }
                }
            }
            base.WatchProperty(expression);
        }

        private async Task FetchState()
        {
            object state_obj;
            var cancel = new CancellationTokenSource();
            _fetchingStateCancel = cancel;
            try
            {
                state_obj = await Root.CommandThread.OnBackgroundThread(() =>
                {
                    return ElementWrapper.Accessible.accState[ElementWrapper.ChildId];
                }, ElementWrapper);
            }
            catch (Exception e)
            {
                if (!UiaElement.IsExpectedException(e))
                    throw;
                return;
            }

            if (cancel.IsCancellationRequested)
                return;

            if (state_obj is null)
            {
                Utils.DebugWriteLine($"WARNING: accState returned NULL for {this}");
            }
            else if (!(state_obj is int state))
            {
                Utils.DebugWriteLine($"WARNING: accState returned {state_obj.GetType()} instead of int for {this}");
            }
            else
            {
                _stateKnown = true;
                _state = state;
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.msaa_state: {new MsaaState(state)}");
                PropertyChanged("msaa_state");
            }
        }

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
                {
                    if (_role >= 0 && _role < UiaElement.msaa_role_to_enum.Length)
                        Utils.DebugWriteLine($"{this}.msaa_role: {UiaElement.msaa_role_to_enum[_role]}");
                    else
                        Utils.DebugWriteLine($"{this}.msaa_role: {_role}");
                }
                PropertyChanged("msaa_role");
            }
        }
    }
}
