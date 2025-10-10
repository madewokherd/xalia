using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    class Accessible2Provider : UiDomProviderBase
    {
        public Accessible2Provider(HwndProvider hwndProvider, UiDomElement element, IAccessible2 accessible2)
        {
            RootHwnd = hwndProvider;
            Element = element;
            Accessible2 = accessible2;
        }

        public HwndProvider RootHwnd { get; }
        public UiDomElement Element { get; }
        public IAccessible2 Accessible2 { get; }
        public Win32Connection Connection => RootHwnd.Connection;
        public int Tid => RootHwnd.Tid;
        public CommandThread CommandThread => RootHwnd.CommandThread;

        public long Role { get; private set; }
        public bool RoleKnown { get; private set; }
        private bool _fetchingRole;

        static Accessible2Provider()
        {
            ia2_role_to_enum = new UiDomEnum[ia2_role_names.Length];
            ia2_name_to_role = new Dictionary<string, int>();
            for (int i = 0; i < ia2_role_names.Length; i++)
            {
                string name = ia2_role_names[i];
                string[] names;
                if (name.Contains("_"))
                    names = new[] { name, name.Replace("_", "") };
                else
                    names = new[] { name };
                foreach (string rolename in names)
                    ia2_name_to_role[rolename] = i + 0x401;
                ia2_role_to_enum[i] = new UiDomEnum(names);
            }
        }

        internal static readonly string[] ia2_role_names =
        {
            "canvas", // starts at id 0x401
            "caption",
            "check_menu_item",
            "color_chooser",
            "date_editor",
            "desktop_icon",
            "desktop_pane",
            "directory_pane",
            "editbar",
            "embedded_object",
            "endnote",
            "file_chooser",
            "font_chooser",
            "footer",
            "footnote",
            "from",
            "frame",
            "glass_pane",
            "header",
            "heading",
            "icon",
            "image_map",
            "input_method_window",
            "internal_frame",
            "label",
            "layered_pane",
            "note",
            "option_pane",
            "page",
            "paragraph",
            "radio_menu_item",
            "redundant_object",
            "root_pane",
            "ruler",
            "scroll_pane",
            "selection",
            "shape",
            "split_pane",
            "tear_off_menu",
            "terminal",
            "text_frame",
            "toggle_button",
            "view_port",
            "complementary_content",
        };
        private static readonly UiDomEnum[] ia2_role_to_enum;
        private static readonly Dictionary<string, int> ia2_name_to_role;

        public UiDomValue RoleAsValue
        {
            get
            {
                if (RoleKnown)
                {
                    if (Role >= 0 && Role < AccessibleProvider.msaa_role_to_enum.Length)
                        return AccessibleProvider.msaa_role_to_enum[Role];
                    else if (Role >= 0x401 && (Role - 0x401 < ia2_role_to_enum.Length))
                        return ia2_role_to_enum[Role - 0x401];
                    return new UiDomInt(Role);
                }
                return UiDomUndefined.Instance;
            }
        }

        public override void DumpProperties(UiDomElement element)
        {
            if (RoleKnown)
                Utils.DebugWriteLine($"  ia2_role: {RoleAsValue}");
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_accessible2_element":
                    return UiDomBoolean.True;
                case "ia2_role":
                    depends_on.Add((Element, new IdentifierExpression("ia2_role")));
                    return RoleAsValue;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "role":
                case "control_type":
                    depends_on.Add((Element, new IdentifierExpression("ia2_role")));
                    if (RoleKnown && Role != ROLE_SYSTEM_CLIENT)
                        return RoleAsValue;
                    break;
            }
            if (ia2_name_to_role.TryGetValue(identifier, out var ia2_role))
            {
                depends_on.Add((Element, new IdentifierExpression("ia2_role")));
                if (RoleKnown)
                {
                    return UiDomBoolean.FromBool(Role == 0x401 + ia2_role);
                }
            }
            if (AccessibleProvider.msaa_name_to_role.TryGetValue(identifier, out var role))
            {
                depends_on.Add((Element, new IdentifierExpression("ia2_role")));
                if (RoleKnown)
                {
                    return UiDomBoolean.FromBool(Role == role);
                }
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "ia2_role":
                        {
                            if (!RoleKnown && !_fetchingRole)
                            {
                                _fetchingRole = true;
                                Utils.RunTask(FetchRole());
                            }
                            return true;
                        }
                }
            }
            return base.WatchProperty(element, expression);
        }

        private async Task FetchRole()
        {
            long role;
            try
            {
                role = await CommandThread.OnBackgroundThread(() =>
                {
                    return Accessible2.role;
                }, CommandThreadPriority.Query);
            }
            catch (Exception e)
            {
                if (!AccessibleProvider.IsExpectedException(e))
                    throw;
                return;
            }
            Role = role;
            RoleKnown = true;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"{Element}.ia2_role: {RoleAsValue}");
            Element.PropertyChanged("ia2_role");
        }
    }
}