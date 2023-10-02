﻿using Accessibility;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
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

        private static string[] tracked_properties = new string[] { "recurse_method" };

        public HwndProvider RootHwnd { get; }
        public UiDomElement Element { get; private set; }
        public Win32Connection Connection => RootHwnd.Connection;
        public int Tid => RootHwnd.Tid;
        public IAccessible IAccessible { get; private set; }
        public int ChildId { get; }

        enum RecurseMethod
        {
            None,
            IEnumVARIANT,
            accChild,
            // accNavigate,
            Auto
        }
        private RecurseMethod _recurseMethod;

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
            if (e is ArgumentException)
            {
                // thrown for stale childid's
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

        public override void NotifyElementRemoved(UiDomElement element)
        {
            _recurseMethod = RecurseMethod.None;
            base.NotifyElementRemoved(element);
        }

        public override void DumpProperties(UiDomElement element)
        {
            if (RoleKnown)
                Utils.DebugWriteLine($"  msaa_role: {RoleAsValue}");
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
            }
            return RootHwnd.ChildEvaluateIdentifier(identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                        return new UiDomString("msaa");
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
                            break;
                        }
                }
            }
            return false;
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
                children = await Connection.CommandThread.OnBackgroundThread(() =>
                {
                    List<ElementIdentifier> result;
                    var count = 0;
                    if (_recurseMethod != RecurseMethod.None)
                        count = IAccessible.accChildCount;
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
                        case RecurseMethod.Auto:
                            result = GetChildrenEnumVariantBackground(count);
                            if (result is null)
                                result = GetChildrenAccChildBackground(count);
                            break;
                    }
                    return result;
                }, Tid);
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
                var v = IAccessible.accChild[i + 1];
                if (v is int vi && vi == CHILDID_SELF)
                    continue;
                if (!seen.Add(v))
                    continue;
                result.Add(ElementIdFromVariantBackground(v));
            }

            return result;
        }

        private ElementIdentifier ElementIdFromVariantBackground(object variant)
        {
            ElementIdentifier result = default;
            result.root_hwnd = RootHwnd.Hwnd;
            IAccessible acc;
            if (variant is int childid)
            {
                var child = IAccessible.accChild[childid];
                if (child is null)
                {
                    // Child without its own IAccessible
                    result.acc = IAccessible;
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
                    IntPtr root_hwnd = oleWindow.GetWindow();
                    if (root_hwnd != IntPtr.Zero && root_hwnd != RootHwnd.Hwnd)
                    {
                        result.root_hwnd = root_hwnd;
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

            // TODO: Check for IAccessibleEx (UIA)

            result.acc = acc;

            IAccessible2 acc2 = QueryIAccessible2(acc);

            if (!(acc2 is null))
            {
                result.acc2 = acc2;
                result.acc2_uniqueId = acc2.uniqueID;
            }

            return result;
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
                                default:
                                    new_method = RecurseMethod.None;
                                    break;
                            }
                        }
                        else
                            new_method = RecurseMethod.None;
                        WatchChildren(new_method);
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
