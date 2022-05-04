using System;
using System.Threading;
using System.Threading.Tasks;

using Tmds.DBus;

using Gazelle.UiDom;
using Gazelle.AtSpi.DBus;
using Gazelle.Gudl;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Gazelle.AtSpi
{
    internal class AtSpiObject : UiDomObject
    {
        internal readonly AtSpiConnection Connection;
        internal readonly string Service;
        internal readonly string Path;
        public override string DebugId => string.Format("{0}:{1}", Service, Path);

        private bool watching_children;
        private bool children_known;
        private IDisposable children_changed_event;
        private IDisposable property_change_event;

        internal IAccessible acc;

        internal IObject object_events;

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

        public int Role { get; private set; }
        public bool RoleKnown { get; private set; }
        private bool fetching_role;

        static AtSpiObject()
        {
            name_to_role = new Dictionary<string, int>();
            role_to_enum = new UiDomEnum[role_names.Length];
            for (int i=0; i<role_names.Length; i++)
            {
                string name = role_names[i];
                string[] names;
                if (name == "push_button")
                    names = new[] { "push_button", "pushbutton", "button" };
                else if (name.Contains("_"))
                    names = new[] { name, name.Replace("_", "") };
                else
                    names = new[] { name };
                role_to_enum[i] = new UiDomEnum(names);
                foreach (string rolename in names)
                    name_to_role[rolename] = i;
            }
        }

        internal AtSpiObject(AtSpiConnection connection, string service, string path) : base(connection)
        {
            Path = path;
            Service = service;
            Connection = connection;
            acc = connection.connection.CreateProxy<IAccessible>(service, path);
            object_events = connection.connection.CreateProxy<IObject>(service, path);
        }

        internal AtSpiObject(AtSpiConnection connection, string service, ObjectPath path) :
            this(connection, service, path.ToString())
        { }

        private async Task WatchProperties()
        {
            IDisposable property_change_event = await object_events.WatchPropertyChangeAsync(OnPropertyChange, Utils.OnError);
            if (this.property_change_event != null)
                this.property_change_event.Dispose();
            this.property_change_event = property_change_event;
        }

        private void OnPropertyChange((string, uint, uint, object) obj)
        {
            var propname = obj.Item1;
            var value = obj.Item4;
            switch (propname)
            {
                case "accessible-role":
                    Role = (int)value;
                    RoleKnown = true;
#if DEBUG
                    if (Role < role_to_enum.Length)
                        Console.WriteLine($"{this}.spi_role: {role_to_enum[Role]}");
                    else
                        Console.WriteLine($"{this}.spi_role: {Role}");
#endif
                    break;
            }
        }

        protected override void SetAlive(bool value)
        {
            if (value)
            {
                Utils.RunTask(WatchProperties());
            }
            else
            {
                if (children_changed_event != null)
                {
                    children_changed_event.Dispose();
                    children_changed_event = null;
                }
                watching_children = false;
                children_known = false;
                if (property_change_event != null)
                {
                    property_change_event.Dispose();
                    property_change_event = null;
                }
                RoleKnown = false;
                fetching_role = false;
            }
            base.SetAlive(value);
        }

        private async Task WatchChildrenTask()
        {
            IDisposable children_changed_event = await object_events.WatchChildrenChangedAsync(OnChildrenChanged, Utils.OnError);

            if (this.children_changed_event != null)
                this.children_changed_event.Dispose();

            this.children_changed_event = children_changed_event;

            (string, ObjectPath)[] children = await acc.GetChildrenAsync();

            if (children_known)
                return;

            for (int i=0; i<children.Length; i++)
            {
                string service = children[i].Item1;
                ObjectPath path = children[i].Item2;
                AddChild(i, new AtSpiObject(Connection, service, path));
            }
            children_known = true;
        }

        internal void WatchChildren()
        {
            if (watching_children)
                return;
#if DEBUG
            Console.WriteLine("WatchChildren for {0}", DebugId);
#endif
            watching_children = true;
            children_known = false;
            Utils.RunTask(WatchChildrenTask());
        }

        internal void UnwatchChildren()
        {
            if (!watching_children)
                return;
#if DEBUG
            Console.WriteLine("UnwatchChildren for {0}", DebugId);
#endif
            watching_children = false;
            if (children_changed_event != null)
            {
                children_changed_event.Dispose();
                children_changed_event = null;
            }
            for (int i=Children.Count-1; i >= 0; i--)
            {
                RemoveChild(i);
            }
        }

        private void OnChildrenChanged((string, uint, uint, object) obj)
        {
            if (!watching_children || !children_known)
                return;
            var detail = obj.Item1;
            var index = obj.Item2;
            var id = ((string, ObjectPath))(obj.Item4);
            var service = id.Item1;
            var path = id.Item2.ToString();
            if (detail == "add")
            {
                AddChild((int)index, new AtSpiObject(Connection, id.Item1, id.Item2));
            }
            else if (detail == "remove")
            {
                // Don't assume the index matches our internal view, we don't always get "reorder" notificaions
#if DEBUG
                bool found = false;
#endif
                for (int i=0; i<Children.Count; i++)
                {
                    var child = (AtSpiObject)Children[i];
                    if (child.Service == service && child.Path == path)
                    {
#if DEBUG
                        if (index != i)
                        {
                            Console.WriteLine("Got remove notification for {0} with index {1}, but we have it at index {2}", child.DebugId, index, i);
                        }
                        found = true;
#endif
                        RemoveChild(i);
                        break;
                    }
                }
#if DEBUG
                if (!found)
                    Console.WriteLine("Got remove notification from {0} for {1}:{2}, but we don't have it as a child",
                        DebugId, service, path);
#endif
            }
        }

        protected override void DeclarationsChanged(Dictionary<string, UiDomValue> all_declarations, HashSet<(UiDomObject, GudlExpression)> dependencies)
        {
            if (all_declarations.TryGetValue("recurse", out var recurse) && recurse.ToBool())
                WatchChildren();
            else
                UnwatchChildren();

            base.DeclarationsChanged(all_declarations, dependencies);
        }

        private async Task FetchRole()
        {
            int role = (int)await acc.GetRoleAsync();
            Role = role;
            RoleKnown = true;
#if DEBUG
            if (role < role_to_enum.Length)
                Console.WriteLine($"{this}.spi_role: {role_to_enum[role]}");
            else
                Console.WriteLine($"{this}.spi_role: {role}");
#endif
            PropertyChanged("spi_role");
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_role":
                        if (!RoleKnown && !fetching_role)
                        {
                            fetching_role = true;
                            Utils.RunTask(FetchRole());
                        }
                        break;
                }
            }

            base.WatchProperty(expression);
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "spi_service":
                    // depends_on.Add((this, new IdentifierExpression(id))); // not needed because this property is known and can't change
                    return new UiDomString(Service);
                case "spi_path":
                    // depends_on.Add((this, new IdentifierExpression(id))); // not needed because this property is known and can't change
                    return new UiDomString(Path);
                case "role:":
                case "control_type:":
                case "controltype:":
                case "spi_role":
                    depends_on.Add((this, new IdentifierExpression("spi_role")));
                    if (Role > 0 && Role < role_to_enum.Length)
                        return role_to_enum[Role];
                    // TODO: return unknown values as numbers?
                    return UiDomUndefined.Instance;
            }

            if (name_to_role.TryGetValue(id, out var expected_role))
            {
                depends_on.Add((this, new IdentifierExpression("spi_role")));
                return UiDomBoolean.FromBool(Role == expected_role);
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
