using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using Xalia.AtSpi.DBus;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.AtSpi
{
    internal class AtSpiElement : UiDomElement
    {
        internal readonly AtSpiConnection Connection;
        internal readonly string Service;
        internal readonly string Path;
        public override string DebugId => string.Format("{0}:{1}", Service, Path);

        private bool watching_children;
        private bool children_known;
        private bool polling_children;
        private CancellationTokenSource children_poll_token;
        private IDisposable children_changed_event;
        private IDisposable property_change_event;
        private IDisposable state_changed_event;
        private IDisposable window_activate_event;
        private IDisposable window_deactivate_event;
        private IDisposable bounds_changed_event;
        private IDisposable text_changed_event;

        private bool watching_states;
        private AtSpiState state;

        private bool watching_bounds;
        public bool BoundsKnown { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private bool watching_text;
        public bool TextKnown { get; private set; }
        public string Text { get; private set; }

        public bool AttributesKnown { get; private set; }
        public IDictionary<string, string> Attributes { get; private set; }

        private bool watching_abs_position;
        private CancellationTokenSource abs_position_refresh_token;
        public bool AbsPositionKnown;
        public int AbsX { get; private set; }
        public int AbsY { get; private set; }
        public int AbsWidth { get; private set; }
        public int AbsHeight { get; private set; }

        public bool MinimumValueKnown { get; private set; }
        public double MinimumValue { get; private set; }
        private bool watching_minimum_value;
        private CancellationTokenSource minimum_value_refresh_token;

        public bool MaximumValueKnown { get; private set; }
        public double MaximumValue { get; private set; }
        private bool watching_maximum_value;
        private CancellationTokenSource maximum_value_refresh_token;

        private bool fetching_actions;
        public string[] Actions { get; private set; }

        internal IAccessible acc;
        internal IAction action;
        internal IApplication app;
        internal IComponent component;
        internal IText text_iface;
        internal ISelection selection;
        internal IValue value_iface;

        internal IObject object_events;
        internal IWindow window_events;

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

        public string Name { get; private set; }
        public bool NameKnown { get; private set; }
        private bool fetching_name;

        public string[] SupportedInterfaces { get; private set; }
        private bool fetching_supported;

        public string ToolkitName { get; private set; }
        private bool fetching_toolkit_name;
        private bool fetching_attributes;

        public double MinimumIncrement { get; private set; }
        public bool MinimumIncrementKnown { get; private set; }
        private bool fetching_minimum_increment;

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
        }

        internal AtSpiElement(AtSpiConnection connection, string service, string path) : base(connection)
        {
            Path = path;
            Service = service;
            Connection = connection;
            state = new AtSpiState(this);
            acc = connection.connection.CreateProxy<IAccessible>(service, path);
            action = connection.connection.CreateProxy<IAction>(service, path);
            app = connection.connection.CreateProxy<IApplication>(service, path);
            component = connection.connection.CreateProxy<IComponent>(service, path);
            text_iface = connection.connection.CreateProxy<IText>(service, path);
            selection = connection.connection.CreateProxy<ISelection>(service, path);
            value_iface = connection.connection.CreateProxy<IValue>(service, path);
            object_events = connection.connection.CreateProxy<IObject>(service, path);
            window_events = connection.connection.CreateProxy<IWindow>(service, path);
        }

        internal AtSpiElement(AtSpiConnection connection, string service, ObjectPath path) :
            this(connection, service, path.ToString())
        { }

        private async Task WatchProperties()
        {
            IDisposable property_change_event;
            try
            {
                property_change_event = await object_events.WatchPropertyChangeAsync(OnPropertyChange, Utils.OnError);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
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
                    if (value is int role)
                    {
                        Role = role;
                        RoleKnown = true;
                        if (MatchesDebugCondition())
                        {
                            if (Role < role_to_enum.Length)
                                Utils.DebugWriteLine($"{this}.spi_role: {role_to_enum[Role]}");
                            else
                                Utils.DebugWriteLine($"{this}.spi_role: {Role}");
                        }
                        PropertyChanged("spi_role");
                    }
                    break;
                case "accessible-name":
                    if (value is string name)
                    {
                        Name = name;
                        NameKnown = true;
                        if (MatchesDebugCondition())
                            Utils.DebugWriteLine($"{this}.spi_name: {Name}");
                        PropertyChanged("spi_name");
                    }
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
                NameKnown = false;
                fetching_name = false;
                MinimumIncrementKnown = false;
                fetching_minimum_increment = false;
                if (state_changed_event != null)
                {
                    state_changed_event.Dispose();
                    state_changed_event = null;
                }
                if (window_activate_event != null)
                {
                    window_activate_event.Dispose();
                    window_activate_event = null;
                }
                if (window_deactivate_event != null)
                {
                    window_deactivate_event.Dispose();
                    window_deactivate_event = null;
                }
                watching_states = false;
                if (bounds_changed_event != null)
                {
                    bounds_changed_event.Dispose();
                    bounds_changed_event = null;
                }
                watching_bounds = false;
                if (text_changed_event != null)
                {
                    text_changed_event.Dispose();
                    text_changed_event = null;
                }
                watching_text = false;
                if (abs_position_refresh_token != null)
                {
                    abs_position_refresh_token.Cancel();
                    abs_position_refresh_token = null;
                }
                watching_abs_position = false;
                if (minimum_value_refresh_token != null)
                {
                    minimum_value_refresh_token.Cancel();
                    minimum_value_refresh_token = null;
                }
                watching_minimum_value = false;
                if (maximum_value_refresh_token != null)
                {
                    maximum_value_refresh_token.Cancel();
                    maximum_value_refresh_token = null;
                }
                watching_maximum_value = false;
                if (children_poll_token != null)
                {
                    children_poll_token.Cancel();
                    children_poll_token = null;
                }
                polling_children = false;
            }
            base.SetAlive(value);
        }

        private async Task<(string, ObjectPath)[]> GetChildList()
        {
            try
            {
                var children = await acc.GetChildrenAsync();

                if (children.Length == 0)
                {
                    var child_count = await acc.GetChildCountAsync();
                    if (child_count != 0)
                    {
                        // This happens for AtkSocket/AtkPlug
                        // https://gitlab.gnome.org/GNOME/at-spi2-core/-/issues/98

                        children = new (string, ObjectPath)[child_count];

                        for (int i = 0; i < child_count; i++)
                        {
                            children[i] = await acc.GetChildAtIndexAsync(i);
                        }
                    }
                }

                return children;
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return new (string, ObjectPath)[0];
            }
        }

        static bool DebugExceptions = Environment.GetEnvironmentVariable("XALIA_DEBUG_EXCEPTIONS") != "0";

        public static bool IsExpectedException(DBusException e, params string[] extra_errors)
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

        private async Task WatchChildrenTask()
        {
            IDisposable children_changed_event;
            try
            {
                children_changed_event = await object_events.WatchChildrenChangedAsync(OnChildrenChanged, Utils.OnError);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }

            if (this.children_changed_event != null)
                this.children_changed_event.Dispose();

            this.children_changed_event = children_changed_event;

            (string, ObjectPath)[] children = await GetChildList();
            
            if (children_known)
                return;

            for (int i=0; i<children.Length; i++)
            {
                string service = children[i].Item1;
                ObjectPath path = children[i].Item2;
                AddChild(i, new AtSpiElement(Connection, service, path));
            }
            children_known = true;
        }

        private static bool ElementIdMatches(UiDomElement element, (string, ObjectPath) id)
        {
            if (element is AtSpiElement atspi)
            {
                return atspi.Service == id.Item1 && atspi.Path == id.Item2.ToString();
            }
            return false;
        }

        private async Task PollChildren()
        {
            if (!polling_children)
                return;

            var children = await GetChildList();

            if (!polling_children)
                return;

            // First remove any existing children that are missing or out of order
            int i = 0;
            foreach (var new_child in children)
            {
                if (!Children.Exists((UiDomElement element) => ElementIdMatches(element, new_child)))
                    continue;
                while (!ElementIdMatches(Children[i], new_child))
                {
                    RemoveChild(i);
                }
                i++;
            }

            while (i < Children.Count)
                RemoveChild(i);

            // Add any new children
            i = 0;
            foreach (var new_child in children)
            {
                if (Children.Count <= i || !ElementIdMatches(Children[i], children[i]))
                {
                    // We accept duplicate elements in AT-SPI2 under the theory that we won't get infinite recursion
                    string service = children[i].Item1;
                    ObjectPath path = children[i].Item2;
                    AddChild(i, new AtSpiElement(Connection, service, path));
                }
                i += 1;
            }

            children_known = true;

            children_poll_token = new CancellationTokenSource();

            try
            {
                await Task.Delay(2000, children_poll_token.Token);
            }
            catch (TaskCanceledException)
            {
                children_poll_token = null;
                return;
            }

            children_poll_token = null;
            Utils.RunTask(PollChildren());
        }
        internal void WatchChildren()
        {
            if (watching_children)
                return;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"WatchChildren for {this}");
            watching_children = true;
            children_known = false;
            Utils.RunTask(WatchChildrenTask());
        }

        internal void UnwatchChildren()
        {
            if (!watching_children)
                return;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {this}");
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
                if (index > Children.Count)
                {
                    Utils.DebugWriteLine($"WARNING: Index {index} for new child of {service}:{path} is out of range");
                    index = (uint)Children.Count;
                }

                AddChild((int)index, new AtSpiElement(Connection, id.Item1, id.Item2));
            }
            else if (detail == "remove")
            {
                // Don't assume the index matches our internal view, we don't always get "reorder" notificaions
                bool found = false;
                for (int i=0; i<Children.Count; i++)
                {
                    var child = (AtSpiElement)Children[i];
                    if (child.Service == service && child.Path == path)
                    {
                        if (index != i)
                        {
                            if (MatchesDebugCondition())
                                Utils.DebugWriteLine($"Got remove notification for {child} with index {index}, but we have it at index {i}");
                        }
                        found = true;
                        RemoveChild(i);
                        break;
                    }
                }
                if (!found && MatchesDebugCondition())
                    Utils.DebugWriteLine($"Got remove notification from {this} for {service}:{path}, but we don't have it as a child");
            }
        }

        protected override void DeclarationsChanged(Dictionary<string, (GudlDeclaration, UiDomValue)> all_declarations, HashSet<(UiDomElement, GudlExpression)> dependencies)
        {
            if (all_declarations.TryGetValue("recurse", out var recurse) && recurse.Item2.ToBool())
                WatchChildren();
            else
                UnwatchChildren();

            if (watching_children &&
                all_declarations.TryGetValue("poll_children", out var poll_children) && poll_children.Item2.ToBool())
            {
                if (!polling_children)
                {
                    polling_children = true;
                    Utils.RunTask(PollChildren());
                }
            }
            else
            {
                if (polling_children)
                {
                    polling_children = false;
                    if (children_poll_token != null)
                    {
                        children_poll_token.Cancel();
                        children_poll_token = null;
                    }
                }
            }

            base.DeclarationsChanged(all_declarations, dependencies);
        }

        internal void StatesChanged(HashSet<string> changed_states)
        {
            if (changed_states.Count == 0)
                return;
            var changed_properties = new HashSet<GudlExpression>();
            foreach (string state in changed_states)
            {
                changed_properties.Add(new BinaryExpression(
                    new IdentifierExpression("spi_state"),
                    new IdentifierExpression(state),
                    GudlToken.Dot
                ));
            }
            base.PropertiesChanged(changed_properties);
        }

        private async Task FetchMinimumIncrement()
        {
            double val;
            try
            {
                val = (int)await value_iface.GetMinimumIncrementAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            MinimumIncrement = val;
            MinimumIncrementKnown = true;
            PropertyChanged("spi_minimum_increment");
        }

        private async Task FetchRole()
        {
            int role;
            try
            {
                role = (int)await acc.GetRoleAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            Role = role;
            RoleKnown = true;
            if (MatchesDebugCondition())
            {
                if (role < role_to_enum.Length)
                    Utils.DebugWriteLine($"{this}.spi_role: {role_to_enum[role]}");
                else
                    Utils.DebugWriteLine($"{this}.spi_role: {role}");
            }
            PropertyChanged("spi_role");
        }

        private async Task FetchName()
        {
            string name;
            try
            {
                name = await acc.GetNameAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            Name = name;
            NameKnown = true;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_name: {Name}");
            PropertyChanged("spi_name");
        }

        private async Task WatchBounds()
        {
            IDisposable bounds_changed_event;
            try
            {
                bounds_changed_event = await object_events.WatchBoundsChangedAsync(OnBoundsChanged, Utils.OnError);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (this.bounds_changed_event != null)
                this.bounds_changed_event.Dispose();
            this.bounds_changed_event = bounds_changed_event;
            try
            {
                (X, Y, Width, Height) = await component.GetExtentsAsync(1); // 1 = ATSPI_COORD_TYPE_WINDOW
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            BoundsKnown = true;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_bounds: {X},{Y}: {Width}x{Height}");
            PropertyChanged("spi_bounds");
       }

        private void OnBoundsChanged((string, uint, uint, object) obj)
        {
            var bounds = (ValueTuple<int, int, int, int>)obj.Item4;
            (var x, var y, var width, var height) = bounds;
            if (x == X && y == Y && width == Width && height == Height)
                return;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            BoundsKnown = true;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_bounds: {X},{Y}: {Width}x{Height}");
            PropertyChanged("spi_bounds");
        }

        private async Task WatchText()
        {
            IDisposable text_changed_event;
            try
            {
                text_changed_event = await object_events.WatchTextChangedAsync(OnTextChanged, Utils.OnError);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (this.text_changed_event != null)
                this.text_changed_event.Dispose();
            this.text_changed_event = text_changed_event;
            try
            {
                Text = await text_iface.GetTextAsync(0, -1);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            TextKnown = true;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_text: {Text}");
            PropertyChanged("spi_text");
        }

        private void OnTextChanged((string, uint, uint, object) obj)
        {
            if (!TextKnown)
            {
                return;
            }
            var detail = obj.Item1;
            var start_ofs = (int)obj.Item2;
            var length = (int)obj.Item3;
            var data = (string)obj.Item4;
            if (detail == "insert")
            {
                if (MatchesDebugCondition() && data.Length != length)
                {
                    Utils.DebugWriteLine($"WARNING: Got object:text-changed:insert event with mismatched length - length={length}, data={data}");
                }
                Text = string.Format("{0}{1}{2}",
                    Text.Substring(0, start_ofs),
                    data,
                    Text.Substring(start_ofs));
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.spi_text: {Text}");
                PropertyChanged("spi_text");
            }
            else if (detail == "delete")
            {
                if (MatchesDebugCondition() && Text.Substring(start_ofs, length) != data)
                {
                    Utils.DebugWriteLine("WARNING: Got object:text-changed:delete event with wrong data");
                    Utils.DebugWriteLine($"  expected {Text.Substring(start_ofs, length)}");
                    Utils.DebugWriteLine($"  got {data}");
                }
                Text = Text.Substring(0, start_ofs) + Text.Substring(start_ofs + length);
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.spi_text: {Text}");
                PropertyChanged("spi_text");
            }
        }

        private async Task FetchAttributes()
        {
            try
            {
                Attributes = await acc.GetAttributesAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            AttributesKnown = true;
            if (MatchesDebugCondition())
                foreach (var kvp in Attributes)
                    Utils.DebugWriteLine($"{this}.spi_attributes.{kvp.Key}: {kvp.Value}");
            PropertyChanged("spi_attributes");
            // FIXME: There is an AttributesChanged event, but no toolkit implements it so there's no info on how it works.
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
                    case "spi_name":
                        if (!NameKnown && !fetching_name)
                        {
                            fetching_name = true;
                            Utils.RunTask(FetchName());
                        }
                        break;
                    case "spi_bounds":
                        if (!BoundsKnown && !watching_bounds)
                        {
                            watching_bounds = true;
                            Utils.RunTask(WatchBounds());
                        }
                        break;
                    case "spi_text":
                        if (!TextKnown && !watching_text)
                        {
                            watching_text = true;
                            Utils.RunTask(WatchText());
                        }
                        break;
                    case "spi_abs_pos":
                        if (!watching_abs_position)
                        {
                            watching_abs_position = true;
                            Utils.RunTask(RefreshAbsPos());
                        }
                        break;
                    case "spi_minimum_value":
                        if (!watching_minimum_value)
                        {
                            watching_minimum_value = true;
                            Utils.RunTask(RefreshMinimumValue());
                        }
                        break;
                    case "spi_maximum_value":
                        if (!watching_maximum_value)
                        {
                            watching_maximum_value = true;
                            Utils.RunTask(RefreshMaximumValue());
                        }
                        break;
                    case "spi_minimum_increment":
                        if (!fetching_minimum_increment)
                        {
                            fetching_minimum_increment = true;
                            Utils.RunTask(FetchMinimumIncrement());
                        }
                        break;
                    case "spi_action":
                        if (!fetching_actions)
                        {
                            fetching_actions = true;
                            Utils.RunTask(FetchActions());
                        }
                        break;
                    case "spi_supported":
                        if (!fetching_supported)
                        {
                            fetching_supported = true;
                            Utils.RunTask(FetchSupported());
                        }
                        break;
                    case "spi_toolkit_name":
                        if (!fetching_toolkit_name)
                        {
                            fetching_toolkit_name = true;
                            Utils.RunTask(FetchToolkitName());
                        }
                        break;
                    case "spi_attributes":
                        if (!fetching_attributes)
                        {
                            fetching_attributes = true;
                            Utils.RunTask(FetchAttributes());
                        }
                        break;
                }
            }

            if (expression is BinaryExpression bin &&
                bin.Kind == GudlToken.Dot &&
                bin.Left is IdentifierExpression prop &&
                prop.Name == "spi_state")
            {
                if (!watching_states)
                {
                    watching_states = true;
                    Utils.RunTask(WatchStates());
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
                    case "spi_text":
                        if (watching_text)
                        {
                            watching_text = false;
                            if (text_changed_event != null)
                            {
                                text_changed_event.Dispose();
                                text_changed_event = null;
                            }
                            TextKnown = false;
                        }
                        break;
                    case "spi_abs_pos":
                        if (watching_abs_position)
                        {
                            watching_abs_position = false;
                            if (abs_position_refresh_token != null)
                            {
                                abs_position_refresh_token.Cancel();
                                abs_position_refresh_token = null;
                            }
                            AbsPositionKnown = false;
                        }
                        break;
                    case "spi_minimum_value":
                        if (watching_minimum_value)
                        {
                            watching_minimum_value = false;
                            if (minimum_value_refresh_token != null)
                            {
                                minimum_value_refresh_token.Cancel();
                                minimum_value_refresh_token = null;
                            }
                            MinimumValueKnown = false;
                        }
                        break;
                    case "spi_maximum_value":
                        if (watching_maximum_value)
                        {
                            watching_maximum_value = false;
                            if (maximum_value_refresh_token != null)
                            {
                                maximum_value_refresh_token.Cancel();
                                maximum_value_refresh_token = null;
                            }
                            MaximumValueKnown = false;
                        }
                        break;
                }
            }

            base.UnwatchProperty(expression);
        }

        private async Task FetchActions()
        {
            // FIXME: For some reason calling GetActions through Tmds.DBus crashes the target process
            string[] result;
            try
            {
                int count = await action.GetNActionsAsync();
                result = new string[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = await action.GetNameAsync(i);
                }
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e, "org.freedesktop.DBus.Error.Failed"))
                    throw;
                return;
            }
            Actions = result;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_action: ({string.Join(",", Actions)})");
            PropertyChanged("spi_action");
        }

        private async Task FetchSupported()
        {
            try
            {
                SupportedInterfaces = await acc.GetInterfacesAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_supported: ({string.Join(",", SupportedInterfaces)})");
            PropertyChanged("spi_supported");
        }

        private async Task FetchToolkitName()
        {
            try
            {
                ToolkitName = await app.GetToolkitNameAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_toolkit_name: {ToolkitName}");
            PropertyChanged("spi_toolkit_name");
        }

        private async Task RefreshAbsPos()
        {
            if (!watching_abs_position)
                return;

            int x=0, y=0, width=0, height=0;
            bool known = false;
            try
            {
                (x, y, width, height) = await component.GetExtentsAsync(0); // ATSPI_COORD_TYPE_SCREEN
                known = x != int.MinValue && y != int.MinValue; // GTK uses this for controls outside a scrollable pane
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                known = false;
            }

            if (!watching_abs_position)
                return;

            if (known)
            {
                if (!AbsPositionKnown || x != AbsX || y != AbsY || width != AbsWidth || height != AbsHeight)
                {
                    AbsPositionKnown = true;
                    AbsX = x;
                    AbsY = y;
                    AbsWidth = width;
                    AbsHeight = height;
                    if (MatchesDebugCondition())
                        Utils.DebugWriteLine($"{this}.spi_abs_pos: ({x},{y},{width},{height})");
                    PropertyChanged("spi_abs_pos");
                }
            }
            else
            {
                if (AbsPositionKnown)
                {
                    AbsPositionKnown = false;
                    if (MatchesDebugCondition())
                        Utils.DebugWriteLine($"{this}.spi_abs_pos: undefined");
                    PropertyChanged("spi_abs_pos");
                }
            }

            if (!watching_abs_position)
                return;

            abs_position_refresh_token = new CancellationTokenSource();

            try
            {
                await Task.Delay(500, abs_position_refresh_token.Token);
            }
            catch (TaskCanceledException)
            {
                abs_position_refresh_token = null;
                return;
            }

            abs_position_refresh_token = null;
            Utils.RunTask(RefreshAbsPos());
        }

        private async Task RefreshMinimumValue()
        {
            if (!watching_minimum_value)
                return;

            double result;
            try
            {
                result = await value_iface.GetMinimumValueAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }

            if (!watching_minimum_value)
                return;

            if (!MinimumValueKnown || result != MinimumValue)
            {
                MinimumValueKnown = true;
                MinimumValue = result;
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.spi_minimum_value: ({result})");
                PropertyChanged("spi_minimum_value");
            }

            if (!watching_minimum_value)
                return;

            minimum_value_refresh_token = new CancellationTokenSource();

            try
            {
                await Task.Delay(500, minimum_value_refresh_token.Token);
            }
            catch (TaskCanceledException)
            {
                minimum_value_refresh_token = null;
                return;
            }

            minimum_value_refresh_token = null;
            Utils.RunTask(RefreshMinimumValue());
        }

        private async Task RefreshMaximumValue()
        {
            if (!watching_maximum_value)
                return;

            double result;
            try
            {
                result = await value_iface.GetMaximumValueAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }

            if (!watching_maximum_value)
                return;

            if (!MaximumValueKnown || result != MaximumValue)
            {
                MaximumValueKnown = true;
                MaximumValue = result;
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.spi_maximum_value: ({result})");
                PropertyChanged("spi_maximum_value");
            }

            if (!watching_maximum_value)
                return;

            maximum_value_refresh_token = new CancellationTokenSource();

            try
            {
                await Task.Delay(500, maximum_value_refresh_token.Token);
            }
            catch (TaskCanceledException)
            {
                maximum_value_refresh_token = null;
                return;
            }

            maximum_value_refresh_token = null;
            Utils.RunTask(RefreshMaximumValue());
        }

        private async Task WatchStates()
        {
            IDisposable state_changed_event=null;
            IDisposable window_activate_event=null;
            IDisposable window_deactivate_event;
            try
            {
                state_changed_event = await object_events.WatchStateChangedAsync(OnStateChanged, Utils.OnError);
                // We have to watch for these because Firefox doesn't send StateChanged for "active" state
                window_activate_event = await window_events.WatchActivateAsync(OnWindowActivate, Utils.OnError);
                window_deactivate_event = await window_events.WatchDeactivateAsync(OnWindowDeactivate, Utils.OnError);
            }
            catch (DBusException e)
            {
                if (!(state_changed_event is null))
                    state_changed_event.Dispose();
                if (!(window_activate_event is null))
                    window_activate_event.Dispose();
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (this.state_changed_event != null)
                this.state_changed_event.Dispose();
            if (this.window_activate_event != null)
                this.window_activate_event.Dispose();
            if (this.window_deactivate_event != null)
                this.window_deactivate_event.Dispose();
            this.state_changed_event = state_changed_event;
            this.window_activate_event = window_activate_event;
            this.window_deactivate_event = window_deactivate_event;
            uint[] states;
            try
            {
                states = await acc.GetStateAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            state.SetStates(states);
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_state: {state}");
        }

        private void OnWindowActivate((string, uint, uint, object) obj)
        {
            OnStateChanged(("active", 1, 0, null));
        }

        private void OnWindowDeactivate((string, uint, uint, object) obj)
        {
            OnStateChanged(("active", 0, 0, null));
        }

        private void OnStateChanged((string, uint, uint, object) obj)
        {
            string name = obj.Item1;
            bool value = obj.Item2 != 0;
            if (!AtSpiState.name_mapping.TryGetValue(name, out var actual_name) || actual_name != name)
            {
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"unrecognized state {name} changed to {value} on {this}");
                return;
            }
            if (value)
            {
                if (!state.states.Add(name))
                    return;
            }
            else
            {
                if (!state.states.Remove(name))
                    return;
            }
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_state.{name}: {value}");
            PropertyChanged(new BinaryExpression(
                new IdentifierExpression("spi_state"),
                new IdentifierExpression(name),
                GudlToken.Dot
            ));
        }

        private async Task Select(UiDomRoutineAsync routine)
        {
            if (Parent is AtSpiElement p)
            {
                try
                {
                    await p.selection.SelectChildAsync(p.Children.IndexOf(this));
                }
                catch (DBusException e)
                {
                    if (!IsExpectedException(e))
                        throw;
                    return;
                }
            }
        }

        private async Task Deselect(UiDomRoutineAsync routine)
        {
            if (Parent is AtSpiElement p)
            {
                try
                {
                    await p.selection.DeselectChildAsync(p.Children.IndexOf(this));
                }
                catch (DBusException e)
                {
                    if (!IsExpectedException(e))
                        throw;
                    return;
                }
            }
        }

        private async Task ClearSelection(UiDomRoutineAsync routine)
        {
            try
            {
                await selection.ClearSelectionAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
        }

        private async Task SetFocus(UiDomRoutineAsync routine)
        {
            try
            {
                await component.GrabFocusAsync();
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "spi_service":
                    // depends_on.Add((this, new IdentifierExpression(id))); // not needed because this property is known and can't change
                    return new UiDomString(Service);
                case "spi_path":
                    // depends_on.Add((this, new IdentifierExpression(id))); // not needed because this property is known and can't change
                    return new UiDomString(Path);
                case "role":
                case "control_type":
                case "controltype":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_role";
                case "spi_role":
                    depends_on.Add((this, new IdentifierExpression("spi_role")));
                    if (Role > 0 && Role < role_to_enum.Length)
                        return role_to_enum[Role];
                    // TODO: return unknown values as numbers?
                    return UiDomUndefined.Instance;
                case "name":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_name";
                case "spi_name":
                    depends_on.Add((this, new IdentifierExpression("spi_name")));
                    if (NameKnown)
                        return new UiDomString(Name);
                    return UiDomUndefined.Instance;
                case "rel_x":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_rel_x";
                case "spi_rel_x":
                    depends_on.Add((this, new IdentifierExpression("spi_bounds")));
                    if (BoundsKnown)
                        return new UiDomInt(X);
                    return UiDomUndefined.Instance;
                case "rel_y":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_rel_y";
                case "spi_rel_y":
                    depends_on.Add((this, new IdentifierExpression("spi_bounds")));
                    if (BoundsKnown)
                        return new UiDomInt(Y);
                    return UiDomUndefined.Instance;
                case "spi_width":
                    depends_on.Add((this, new IdentifierExpression("spi_bounds")));
                    if (BoundsKnown)
                        return new UiDomInt(Width);
                    return UiDomUndefined.Instance;
                case "spi_height":
                    depends_on.Add((this, new IdentifierExpression("spi_bounds")));
                    if (BoundsKnown)
                        return new UiDomInt(Height);
                    return UiDomUndefined.Instance;
                case "bounds":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_bounds";
                case "spi_bounds":
                    depends_on.Add((this, new IdentifierExpression("spi_bounds")));
                    if (BoundsKnown)
                        return new UiDomString($"({X},{Y}: {Width}x{Height})");
                    return UiDomUndefined.Instance;
                case "text_contents":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_text";
                case "spi_text":
                case "spi_text_contents":
                    depends_on.Add((this, new IdentifierExpression("spi_text")));
                    if (TextKnown)
                        return new UiDomString(Text);
                    return UiDomUndefined.Instance;
                case "spi_state":
                    return state;
                case "x":
                case "abs_x":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_abs_x";
                case "spi_abs_x":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPositionKnown)
                        return new UiDomInt(AbsX);
                    return UiDomUndefined.Instance;
                case "y":
                case "abs_y":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_abs_y";
                case "spi_abs_y":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPositionKnown)
                        return new UiDomInt(AbsY);
                    return UiDomUndefined.Instance;
                case "width":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_abs_width";
                case "spi_abs_width":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPositionKnown)
                        return new UiDomInt(AbsWidth);
                    return UiDomUndefined.Instance;
                case "height":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_abs_height";
                case "spi_abs_height":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPositionKnown)
                        return new UiDomInt(AbsHeight);
                    return UiDomUndefined.Instance;
                case "spi_abs_pos":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPositionKnown)
                        return new UiDomString($"({AbsX}, {AbsY}, {AbsWidth}, {AbsHeight})");
                    return UiDomUndefined.Instance;
                case "spi_action":
                    depends_on.Add((this, new IdentifierExpression("spi_action")));
                    if (Actions != null)
                    {
                        return new AtSpiActionList(this);
                    }
                    return UiDomUndefined.Instance;
                case "click":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    depends_on.Add((this, new IdentifierExpression("spi_action")));
                    if (Actions != null)
                    {
                        return new AtSpiActionList(this).EvaluateIdentifier("click", root, depends_on);
                    }
                    return UiDomUndefined.Instance;
                case "select":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_select";
                case "spi_select":
                    // FIXME: check whether parent supports ISelection?
                    return new UiDomRoutineAsync(this, "spi_select", Select);
                case "deselect":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_deselect";
                case "spi_deselect":
                    // FIXME: check whether parent supports ISelection?
                    return new UiDomRoutineAsync(this, "spi_deselect", Deselect);
                case "clear_selection":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_clear_selection";
                case "spi_clear_selection":
                    // FIXME: check whether this supports ISelection?
                    return new UiDomRoutineAsync(this, "spi_clear_selection", ClearSelection);
                case "set_focus":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_set_focus";
                case "spi_set_focus":
                case "spi_grab_focus":
                    return new UiDomRoutineAsync(this, "spi_grab_focus", SetFocus);
                case "spi_supported":
                    depends_on.Add((this, new IdentifierExpression("spi_supported")));
                    if (!(SupportedInterfaces is null))
                    {
                        return new AtSpiSupported(this, SupportedInterfaces);
                    }
                    return UiDomUndefined.Instance;
                case "toolkit":
                case "toolkit_name":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_toolkit_name";
                case "spi_toolkit":
                case "spi_toolkit_name":
                    depends_on.Add((this, new IdentifierExpression("spi_toolkit_name")));
                    if (!(ToolkitName is null))
                    {
                        return new UiDomString(ToolkitName);
                    }
                    return UiDomUndefined.Instance;
                case "spi_application":
                    {
                        foreach (var application in Connection.DesktopFrame.Children)
                        {
                            if (application is AtSpiElement atspi && atspi.Service == Service)
                            {
                                return application;
                            }
                        }
                    }
                    return UiDomUndefined.Instance;
                case "spi_attributes":
                    depends_on.Add((this, new IdentifierExpression("spi_attributes")));
                    if (AttributesKnown)
                        return new AtSpiAttributes(this);
                    return UiDomUndefined.Instance;
                case "is_uia_element":
                    return UiDomBoolean.False;
                case "is_spi_element":
                case "is_atspi_element":
                case "is_at_spi_element":
                    return UiDomBoolean.True;
                case "minimum_value":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_minimum_value";
                case "spi_minimum_value":
                    {
                        depends_on.Add((this, new IdentifierExpression("spi_minimum_value")));
                        if (MinimumValueKnown)
                        {
                            return new UiDomDouble(MinimumValue);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "maximum_value":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_maximum_value";
                case "spi_maximum_value":
                    {
                        depends_on.Add((this, new IdentifierExpression("spi_maximum_value")));
                        if (MaximumValueKnown)
                        {
                            return new UiDomDouble(MaximumValue);
                        }
                        return UiDomUndefined.Instance;
                    }
                case "minimum_increment":
                    {
                        var value = base.EvaluateIdentifierCore(id, root, depends_on);
                        if (!value.Equals(UiDomUndefined.Instance))
                            return value;
                    }
                    goto case "spi_minimum_increment";
                case "spi_minimum_increment":
                    depends_on.Add((this, new IdentifierExpression("spi_minimum_increment")));
                    if (MinimumIncrementKnown)
                        return new UiDomDouble(MinimumIncrement);
                    return UiDomUndefined.Instance;
                case "adjust_value":
                    return new UiDomMethod(this, "adjust_value", AdjustValueMethod);
            }

            {
                var value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;
            }

            if (name_to_role.TryGetValue(id, out var expected_role))
            {
                depends_on.Add((this, new IdentifierExpression("spi_role")));
                return UiDomBoolean.FromBool(Role == expected_role);
            }

            if (state.TryEvaluateIdentifier(id, depends_on, out var result))
            {
                return result;
            }

            return UiDomUndefined.Instance;
        }

        protected override void DumpProperties()
        {
            if (Role > 0 && Role < role_to_enum.Length)
                Utils.DebugWriteLine($"  spi_role: {role_to_enum[Role]}");
            if (NameKnown)
                Utils.DebugWriteLine($"  spi_name: {Name}");
            if (BoundsKnown)
            {
                Utils.DebugWriteLine($"  spi_rel_x: {X}");
                Utils.DebugWriteLine($"  spi_rel_y: {Y}");
                Utils.DebugWriteLine($"  spi_width: {Width}");
                Utils.DebugWriteLine($"  spi_height: {Height}");
            }
            if (TextKnown)
                Utils.DebugWriteLine($"  spi_text: {Text}");
            Utils.DebugWriteLine($"  spi_state: {state}");
            if (AbsPositionKnown)
            {
                Utils.DebugWriteLine($"  spi_abs_x: {AbsX}");
                Utils.DebugWriteLine($"  spi_abs_y: {AbsY}");
                Utils.DebugWriteLine($"  spi_abs_width: {AbsWidth}");
                Utils.DebugWriteLine($"  spi_abs_height: {AbsHeight}");
            }
            if (!(Actions is null))
                Utils.DebugWriteLine($"  spi_action: [{String.Join(",", Actions)}]");
            if (!(SupportedInterfaces is null))
                Utils.DebugWriteLine($"  spi_supported: [{String.Join(",", SupportedInterfaces)}]");
            if (!(ToolkitName is null))
                Utils.DebugWriteLine($"  spi_toolkit_name: {ToolkitName}");
            if (AttributesKnown)
            {
                foreach (var kvp in Attributes)
                {
                    Utils.DebugWriteLine($"  spi_attributes.{kvp.Key}: {kvp.Value}");
                }
            }
            if (MinimumValueKnown)
                Utils.DebugWriteLine($"  spi_minimum_value: {MinimumValue}");
            if (MaximumValueKnown)
                Utils.DebugWriteLine($"  spi_maximum_value: {MaximumValue}");
            if (MinimumIncrementKnown)
                Utils.DebugWriteLine($"  spi_minimum_increment: {MinimumIncrement}");
            base.DumpProperties();
        }

        private static UiDomValue AdjustValueMethod(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 1)
                return UiDomUndefined.Instance;

            var increment_value = context.Evaluate(arglist[0], root, depends_on);

            if (!increment_value.TryToDouble(out var unused))
                return UiDomUndefined.Instance;

            return new UiDomRoutineAsync(method.Element, "adjust_value", new UiDomValue[] { increment_value }, AdjustValueRoutine);
        }

        private static async Task AdjustValueRoutine(UiDomRoutineAsync obj)
        {
            var element = obj.Element as AtSpiElement;

            var value_iface = element.value_iface;

            // AdjustValueMethod already verified that this succeeds
            obj.Arglist[0].TryToDouble(out var increment);

            try
            {
                var current_value = await value_iface.GetCurrentValueAsync();

                var new_value = current_value + increment;

                if (increment < 0)
                {
                    var minimum = await value_iface.GetMinimumValueAsync();

                    if (new_value < minimum)
                        new_value = minimum;
                }
                else
                {
                    var maximum = await value_iface.GetMaximumValueAsync();

                    if (new_value > maximum)
                        new_value = maximum;
                }

                await value_iface.SetCurrentValueAsync(new_value);
            }
            catch (DBusException e) {
                if (!IsExpectedException(e))
                    throw;
            }
        }

        public override async Task<(bool,int,int)> GetClickablePoint()
        {
            var result = await base.GetClickablePoint();
            if (result.Item1)
                return result;

            try
            {
                int cx, cy, width, height;
                try
                {
                    (cx, cy, width, height) = await component.GetExtentsAsync(0); // ATSPI_COORD_TYPE_SCREEN
                }
                catch (DBusException e)
                {
                    if (!IsExpectedException(e))
                        throw;
                    return (true, 0, 0);
                }

                int x = cx + width / 2;
                int y = cy + height / 2;
                return (true, x, y);
            }
            catch (DBusException) { }

            return (false, 0, 0);
        }

        public override async Task<double> GetMinimumIncrement()
        {
            try
            {
                var result = await value_iface.GetMinimumIncrementAsync();
                if (result != 0)
                    return result;
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
            return await base.GetMinimumIncrement();
        }

        public override async Task OffsetValue(double ofs)
        {
            if (ofs == 0)
                return;

            try
            {
                var current_value = await value_iface.GetCurrentValueAsync();

                var new_value = current_value + ofs;

                if (ofs > 0)
                {
                    var maximum_value = await value_iface.GetMaximumValueAsync();

                    if (new_value > maximum_value)
                        new_value = maximum_value;
                }
                else
                {
                    var minimum_value = await value_iface.GetMinimumValueAsync();

                    if (new_value < minimum_value)
                        new_value = minimum_value;
                }

                if (new_value != current_value)
                    await value_iface.SetCurrentValueAsync(new_value);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
            }
        }
    }
}
