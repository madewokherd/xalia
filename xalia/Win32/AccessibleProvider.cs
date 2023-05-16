using Accessibility;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class AccessibleProvider : UiDomProviderBase
    {
        public AccessibleProvider(HwndProvider root_hwnd, UiDomElement element,
            IAccessible accessible, int child_id)
        {
            RootHwnd = root_hwnd;
            Element = element;
            IAccessible = accessible;
            ChildId = child_id;
        }

        public HwndProvider RootHwnd { get; }
        public UiDomElement Element { get; private set; }
        public Win32Connection Connection => RootHwnd.Connection;
        public IAccessible IAccessible { get; private set; }
        public int ChildId { get; }

        public int Role { get; private set; }
        public bool RoleKnown { get; private set; }
        private bool _fetchingRole;

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
            { "role", "msaa_role" },
            { "control_type", "msaa_role" },
        };

        static AccessibleProvider()
        {
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

        public override void DumpProperties(UiDomElement element)
        {
            if (RoleKnown)
                Utils.DebugWriteLine($"  {Element}.msaa_role: {RoleAsValue}");
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
            }
            return UiDomUndefined.Instance;
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
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
            return UiDomUndefined.Instance;
        }

        public override void NotifyElementRemoved(UiDomElement element)
        {
            Element = null;
            IAccessible = null;
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
                            break;
                        }
                }
            }
            return false;
        }

        private async Task FetchRole()
        {
            object role_obj;
            try
            {
                role_obj = await Connection.CommandThread.OnBackgroundThread(() =>
                {
                    return IAccessible.accRole[ChildId];
                }, RootHwnd.Tid + 1);
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
    }
}
