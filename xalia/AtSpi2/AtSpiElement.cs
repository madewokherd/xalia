using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.AtSpi2.DBusUtils;

namespace Xalia.AtSpi2
{
    internal class AtSpiElement : UiDomElement
    {
        public AtSpiElement(AtSpiConnection root, string peer, string path): base(root)
        {
            Root = root;
            Peer = peer;
            Path = path;
            RegisterTrackedProperties(tracked_properties);
        }

        private static string[] tracked_properties = { "recurse" };
        private static readonly Dictionary<string, string> property_aliases;

        static AtSpiElement()
        {
            name_to_role = new Dictionary<string, int>();
            role_to_enum = new UiDomEnum[role_names.Length];
            for (int i=0; i<role_names.Length; i++)
            {
                string name = role_names[i];
                string[] names;
                if (name == "push_button")
                    names = new[] { "push_button", "pushbutton", "button" };
                else if (name == "page_tab")
                    names = new[] { "page_tab", "pagetab", "tab" };
                else if (name == "page_tab_list")
                    names = new[] { "page_tab_list", "pagetablist", "tab_item", "tabitem" };
                else if (name == "text")
                    names = new[] { "text", "text_box", "textbox", "edit" };
                else if (name.Contains("_"))
                    names = new[] { name, name.Replace("_", "") };
                else
                    names = new[] { name };
                role_to_enum[i] = new UiDomEnum(names);
                foreach (string rolename in names)
                    name_to_role[rolename] = i;
            }
            string[] aliases = {
                "role", "spi_role",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i=0; i<aliases.Length; i+=2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
        }

        public new AtSpiConnection Root { get; }
        public string Peer { get; }
        public string Path { get; }

        public override string DebugId => $"{Peer}:{Path}";

        public bool RoleKnown { get; private set; }
        public int Role { get; private set; }
        private bool fetching_role;

        private bool watching_children;
#pragma warning disable CS0414 // The field 'AtSpiElement.children_known' is assigned but its value is never used
        private bool children_known; // TODO: Use this when we get a notification of children changed
#pragma warning restore CS0414 // The field 'AtSpiElement.children_known' is assigned but its value is never used

        internal static readonly string[] role_names =
        {
            "invalid",
            "accelerator_label",
            "alert",
            "animation",
            "arrow",
            "calendar",
            "canvas",
            "check_box",
            "check_menu_item",
            "color_chooser",
            "column_header",
            "combo_box",
            "date_editor",
            "desktop_icon",
            "desktop_frame",
            "dial",
            "dialog",
            "directory_pane",
            "drawing_area",
            "file_chooser",
            "filler",
            "focus_traversable",
            "font_chooser",
            "frame",
            "glass_pane",
            "html_container",
            "icon",
            "image",
            "internal_frame",
            "label",
            "layered_pane",
            "list",
            "list_item",
            "menu",
            "menu_bar",
            "menu_item",
            "option_pane",
            "page_tab",
            "page_tab_list",
            "panel",
            "password_text",
            "popup_menu",
            "progress_bar",
            "push_button",
            "radio_button",
            "radio_menu_item",
            "root_pane",
            "row_header",
            "scroll_bar",
            "scroll_pane",
            "separator",
            "slider",
            "spin_button",
            "split_pane",
            "status_bar",
            "table",
            "table_cell",
            "table_column_header",
            "table_row_header",
            "tearoff_menu_item",
            "terminal",
            "text",
            "toggle_button",
            "tool_bar",
            "tool_tip",
            "tree",
            "tree_table",
            "unknown",
            "viewport",
            "window",
            "extended",
            "header",
            "footer",
            "paragraph",
            "ruler",
            "application",
            "autocomplete",
            "editbar",
            "embedded",
            "entry",
            "chart",
            "caption",
            "document_frame",
            "heading",
            "page",
            "section",
            "redundant_object",
            "form",
            "link",
            "input_method_window",
            "table_row",
            "tree_item",
            "document_spreadsheet",
            "document_presentation",
            "document_text",
            "document_web",
            "document_email",
            "comment",
            "list_box",
            "grouping",
            "image_map",
            "notification",
            "info_bar",
            "level_bar",
            "title_bar",
            "block_quote",
            "audio",
            "video",
            "definition",
            "article",
            "landmark",
            "log",
            "marquee",
            "math",
            "rating",
            "timer",
            "static",
            "math_fraction",
            "math_root",
            "subscript",
            "superscript",
            "description_list",
            "description_term",
            "description_value",
            "footnote",
            "content_deletion",
            "content_insertion",
            "mark",
            "suggestion",
        };

        private static readonly Dictionary<string, int> name_to_role;
        private static readonly UiDomEnum[] role_to_enum;

        protected override void SetAlive(bool value)
        {
            if (value)
            {
                Root.NotifyElementCreated(this);
            }
            else
            {
                watching_children = false;
                children_known = false;
                Root.NotifyElementDestroyed(this);
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

            switch (id)
            {
                case "spi_peer":
                    return new UiDomString(Peer);
                case "spi_path":
                    return new UiDomString(Path);
                case "spi_role":
                    depends_on.Add((this, new IdentifierExpression("spi_role")));
                    if (RoleKnown)
                    {
                        if (Role > 0 && Role < role_to_enum.Length)
                            return role_to_enum[Role];
                        return new UiDomInt(Role);
                    }
                    return UiDomUndefined.Instance;
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override void DumpProperties()
        {
            if (RoleKnown)
            {
                if (Role > 0 && Role < role_names.Length)
                    Utils.DebugWriteLine($"  spi_role: {role_names[Role]}");
                else
                    Utils.DebugWriteLine($"  spi_role: {Role}");
            }
            base.DumpProperties();
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_role":
                        if (!fetching_role)
                        {
                            fetching_role = true;
                            Utils.RunTask(FetchRole());
                        }
                        break;
                }
            }
            base.WatchProperty(expression);
        }

        private async Task FetchRole()
        {
            int result;
            try
            {
                result = await CallMethod(Root.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "GetRole", ReadMessageInt32);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            RoleKnown = true;
            Role = result;
            if (MatchesDebugCondition())
            {
                if (Role > 0 && Role < role_names.Length)
                    Utils.DebugWriteLine($"{this}.spi_role: {role_names[Role]}");
                else
                    Utils.DebugWriteLine($"{this}.spi_role: {Role}");
            }
            PropertyChanged("spi_role");
        }

        static bool DebugExceptions = Environment.GetEnvironmentVariable("XALIA_DEBUG_EXCEPTIONS") != "0";

        internal static bool IsExpectedException(DBusException e, params string[] extra_errors)
        {
#if DEBUG
            if (DebugExceptions)
            {
                Utils.DebugWriteLine("WARNING: DBus exception:");
                Utils.DebugWriteLine(e);
            }
#endif
            switch (e.ErrorName)
            {
                case "org.freedesktop.DBus.Error.NoReply":
                case "org.freedesktop.DBus.Error.UnknownObject":
                case "org.freedesktop.DBus.Error.UnknownInterface":
                case "org.freedesktop.DBus.Error.ServiceUnknown":
                    return true;
                default:
                    foreach (var err in extra_errors)
                    {
                        if (e.ErrorName == err)
                            return true;
                    }
#if DEBUG
                    return false;
#else
                    if (DebugExceptions)
                    {
                        Utils.DebugWriteLine("WARNING: DBus exception ignored:");
                        Utils.DebugWriteLine(e);
                    }
                    return true;
#endif
            }
        }

        private async Task<List<(string, string)>> GetChildList()
        {
            try
            {
                var children = await CallMethod(Root.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "GetChildren", ReadMessageElementList);

                if (children.Count == 0)
                {
                    var child_count = (int)await GetProperty(Root.Connection, Peer, Path,
                        IFACE_ACCESSIBLE, "ChildCount");
                    if (child_count != 0)
                    {
                        // This happens for AtkSocket/AtkPlug
                        // https://gitlab.gnome.org/GNOME/at-spi2-core/-/issues/98

                        children = new List<(string, string)>(child_count);

                        for (int i = 0; i < child_count; i++)
                        {
                            children.Add(await CallMethod(Root.Connection, Peer, Path,
                                IFACE_ACCESSIBLE, "GetChildAtIndex", i, ReadMessageElement));
                        }
                    }
                }

                return children;
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return new List<(string, string)>();
            }
            catch (InvalidCastException)
            {
                return new List<(string, string)>();
            }
        }

        private async Task PollChildrenTask()
        {
            if (!watching_children)
                return;

            List<(string, string)> children = await GetChildList();

            // Ignore any duplicate children
            HashSet<(string, string)> seen_children = new HashSet<(string, string)>();
            int i = 0;
            while (i < children.Count)
            {
                if (!seen_children.Add(children[i]))
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
            while (i < Children.Count && Children[i] is AtSpiElement)
                RemoveChild(i);

            // Add any new children
            i = 0;
            foreach (var new_child in children)
            {
                if (Children.Count <= i || !ElementMatches(Children[i], new_child))
                {
                    if (!(Root.LookupElement(new_child) is null))
                    {
                        // Child element is a duplicate of another element somewhere in the tree.
                        continue;
                    }
                    AddChild(i, new AtSpiElement(Root, new_child.Item1, new_child.Item2));
                }
                i += 1;
            }

            children_known = true;
        }

        private bool ElementMatches(UiDomElement element, (string, string) new_child)
        {
            return element is AtSpiElement e && e.Peer == new_child.Item1 && e.Path == new_child.Item2;
        }

        internal void WatchChildren()
        {
            if (watching_children)
                return;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"WatchChildren for {this}");
            watching_children = true;
            children_known = false;
            Utils.RunTask(PollChildrenTask());
        }

        internal void UnwatchChildren()
        {
            if (!watching_children)
                return;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {this}");
            watching_children = false;
            for (int i=Children.Count-1; i >= 0; i--)
            {
                if (Children[i] is AtSpiElement)
                    RemoveChild(i);
            }
        }

        protected override void TrackedPropertyChanged(string name, UiDomValue new_value)
        {
            switch (name)
            {
                case "recurse":
                    {
                        if (new_value.ToBool())
                            WatchChildren();
                        else
                            UnwatchChildren();
                        break;
                    }
            }
            base.TrackedPropertyChanged(name, new_value);
        }
    }
}
