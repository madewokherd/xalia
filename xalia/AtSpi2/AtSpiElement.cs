using System;
using System.Collections;
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
        public AtSpiElement(AtSpiConnection root, string peer, string path): base($"{peer}:{path}", root)
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
            name_to_state = new Dictionary<string, int>();
            for (int i=0; i<state_names.Length; i++)
            {
                name_to_state[state_names[i]] = i;
            }
            string[] aliases = {
                "role", "spi_role",
                "control_type", "spi_role",
                "state", "spi_state",
                "x", "spi_abs_x",
                "y", "spi_abs_y",
                "width", "spi_abs_width",
                "height", "spi_abs_height",
                "abs_x", "spi_abs_x",
                "abs_y", "spi_abs_y",
                "abs_width", "spi_abs_width",
                "abs_height", "spi_abs_height",
                "action", "spi_action",
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

        public bool RoleKnown { get; private set; }
        public int Role { get; private set; }
        private bool fetching_role;

        public bool StateKnown { get; private set; }
        public uint[] State { get; private set; }
        private bool fetching_state;

        private bool watching_children;
        private bool children_known;

        public bool AbsPosKnown { get; private set; }
        public int AbsX { get; private set; }
        public int AbsY { get; private set; }
        public int AbsWidth { get; private set; }
        public int AbsHeight { get; private set; }
        private bool watching_abs_pos;
        private int abs_pos_change_count;

        public string[] Actions { get; private set; }
        private bool fetching_actions;

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

        internal static readonly string[] state_names =
        {
            "invalid",
            "active",
            "armed",
            "busy",
            "checked",
            "collapsed",
            "defunct",
            "editable",
            "enabled",
            "expandable",
            "expanded",
            "focusable",
            "focused",
            "has_tooltip",
            "horizontal",
            "iconified",
            "modal",
            "multi_line",
            "multiselectable",
            "opaque",
            "pressed",
            "resizable",
            "selectable",
            "selected",
            "sensitive",
            "showing",
            "single_line",
            "stale",
            "transient",
            "vertical",
            "visible",
            "manages_descendants",
            "indeterminate",
            "required",
            "truncated",
            "animated",
            "invalid_entry",
            "supports_autocompletion",
            "selectable_text",
            "is_default",
            "visited",
            "checkable",
            "has_popup",
            "read_only",
        };

        internal static Dictionary<string, string> name_mapping;

        private static readonly Dictionary<string, int> name_to_role;
        private static readonly UiDomEnum[] role_to_enum;

        internal static Dictionary<string, int> name_to_state;

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
                watching_abs_pos = false;
                AbsPosKnown = false;
                Root.NotifyElementDestroyed(this);
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
                case "is_uia_element":
                    return UiDomBoolean.False;
                case "is_spi_element":
                case "is_atspi_element":
                case "is_at_spi_element":
                    return UiDomBoolean.True;
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
                case "spi_state":
                    depends_on.Add((this, new IdentifierExpression("spi_state")));
                    if (StateKnown)
                        return new AtSpiState(State);
                    return UiDomUndefined.Instance;
                case "spi_abs_x":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsX);
                    return UiDomUndefined.Instance;
                case "spi_abs_y":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsY);
                    return UiDomUndefined.Instance;
                case "spi_abs_width":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsWidth);
                    return UiDomUndefined.Instance;
                case "spi_abs_height":
                    depends_on.Add((this, new IdentifierExpression("spi_abs_pos")));
                    if (AbsPosKnown)
                        return new UiDomInt(AbsHeight);
                    return UiDomUndefined.Instance;
                case "spi_action":
                    depends_on.Add((this, new IdentifierExpression("spi_action")));
                    if (!(Actions is null))
                        return new AtSpiActionList(this);
                    return UiDomUndefined.Instance;
            }

            value = base.EvaluateIdentifierCore(id, root, depends_on);
            if (!value.Equals(UiDomUndefined.Instance))
                return value;

            if (name_to_role.TryGetValue(id, out var expected_role))
            {
                depends_on.Add((this, new IdentifierExpression("spi_role")));
                if (RoleKnown)
                    return UiDomBoolean.FromBool(Role == expected_role);
            }

            if (name_to_state.TryGetValue(id, out var expected_state))
            {
                depends_on.Add((this, new IdentifierExpression("spi_state")));
                if (StateKnown)
                    return UiDomBoolean.FromBool(AtSpiState.IsStateSet(State, expected_state));
            }

            return UiDomUndefined.Instance;
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
            if (StateKnown)
                Utils.DebugWriteLine($"  spi_state: {new AtSpiState(State)}");
            if (AbsPosKnown)
            {
                Utils.DebugWriteLine($"  spi_abs_x: {AbsX}");
                Utils.DebugWriteLine($"  spi_abs_y: {AbsY}");
                Utils.DebugWriteLine($"  spi_abs_width: {AbsWidth}");
                Utils.DebugWriteLine($"  spi_abs_height: {AbsHeight}");
            }
            if (!(Actions is null))
                Utils.DebugWriteLine($"  spi_action: [{String.Join(",", Actions)}]");
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
                    case "spi_state":
                        if (!fetching_state)
                        {
                            fetching_state = true;
                            Utils.RunTask(FetchState());
                        }
                        break;
                    case "spi_abs_pos":
                        if (!watching_abs_pos)
                        {
                            watching_abs_pos = true;
                            PollProperty(expression, FetchAbsPos, 2000);
                        }
                        break;
                    case "spi_action":
                        if (!fetching_actions)
                        {
                            fetching_actions = true;
                            Utils.RunTask(FetchActions());
                        }
                        break;
                }
            }
            base.WatchProperty(expression);
        }

        private async Task FetchActions()
        {
            string[] result;
            try
            {
                int count = (int)await GetProperty(Root.Connection, Peer, Path, IFACE_ACTION, "NActions");
                result = new string[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = await CallMethod(Root.Connection, Peer, Path, IFACE_ACTION,
                        "GetName", i, ReadMessageString);
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

        protected override void UnwatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "spi_abs_pos":
                        EndPollProperty(expression);
                        watching_abs_pos = false;
                        AbsPosKnown = false;
                        break;
                }
            }
            base.UnwatchProperty(expression);
        }

        private Task FetchAbsPos()
        {
            return FetchAbsPos(false);
        }

        private async Task FetchAbsPos(bool from_event)
        {
            (int, int, int, int) result;
            int old_change_count = abs_pos_change_count;
            using (var poll = await LimitPolling(AbsPosKnown && !from_event))
            {
                if (!watching_abs_pos)
                    return;
                if (old_change_count != abs_pos_change_count)
                    return;
                try
                {
                    await Root.RegisterEvent("object:bounds-changed");

                    result = await CallMethod(Root.Connection, Peer, Path,
                        IFACE_COMPONENT, "GetExtents", (uint)0, ReadMessageExtents);
                }
                catch (DBusException e)
                {
                    if (!IsExpectedException(e))
                        throw;
                    return;
                }
            }
            if (old_change_count != abs_pos_change_count)
                return;
            if (watching_abs_pos && (!AbsPosKnown || result != (AbsX, AbsY, AbsWidth, AbsHeight)))
            {
                AbsPosKnown = true;
                AbsX = result.Item1;
                AbsY = result.Item2;
                AbsWidth = result.Item3;
                AbsHeight = result.Item4;
                if (MatchesDebugCondition())
                    Utils.DebugWriteLine($"{this}.spi_abs_(x,y,width,height): {result}");
                PropertyChanged("spi_abs_pos");
            }
        }

        private Task<IDisposable> LimitPolling(bool value_known)
        {
            return Root.LimitPolling(Peer, value_known);
        }

        private async Task FetchRole()
        {
            int result;
            try
            {
                await Root.RegisterEvent("object:property-change:accessible-role");

                result = await CallMethod(Root.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "GetRole", ReadMessageInt32);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            AtSpiPropertyChange("accessible-role", result);
        }

        private async Task FetchState()
        {
            uint[] result;
            try
            {
                await Root.RegisterEvent("object:state-changed");

                result = await CallMethod(Root.Connection, Peer, Path,
                    IFACE_ACCESSIBLE, "GetState", ReadMessageUint32Array);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (StateKnown)
            {
                if (StructuralComparisons.StructuralEqualityComparer.Equals(State, result))
                    return;
            }
            StateKnown = true;
            State = result;
            if (MatchesDebugCondition())
                Utils.DebugWriteLine($"{this}.spi_state: {new AtSpiState(State)}");
            PropertyChanged("spi_state");
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

            await Root.RegisterEvent("object:children-changed");

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

        internal void AtSpiChildrenChanged(AtSpiSignal signal)
        {
            if (!children_known)
                return;
            var index = signal.detail1;
            var child = ((string, ObjectPath))signal.value;
            var child_element = Root.LookupElement(child);
            switch (signal.detail)
            {
                case "add":
                    {
                        if (!(child_element is null))
                        {
                            Console.WriteLine($"WARNING: {child_element} added to {this} but is already a child of {child_element.Parent}, ignoring.");
                            return;
                        }
                        if (index > Children.Count || index < 0)
                        {
                            Console.WriteLine($"WARNING: {child.Item1}:{child.Item2} added to {this} at index {index}, but there are only {Children.Count} known children");
                            index = Children.Count;
                        }
                        AddChild(index, new AtSpiElement(Root, child.Item1, child.Item2));
                        break;
                    }
                case "remove":
                    {
                        if (child_element is null)
                        {
                            Console.WriteLine($"WARNING: {child.Item1}:{child.Item2} removed from {this}, but the element is unknown");
                            return;
                        }
                        if (child_element.Parent != this)
                        {
                            Console.WriteLine($"WARNING: {child.Item1}:{child.Item2} removed from {this}, but is a child of {child_element.Parent}");
                            return;
                        }
                        if (index >= Children.Count || index < 0 || Children[index] != child_element)
                        {
                            var real_index = Children.IndexOf(child_element);
                            Console.WriteLine($"WARNING: {child.Item1}:{child.Item2} remove event has wrong index - got {index}, should be {real_index}");
                            index = real_index;
                        }
                        RemoveChild(index);
                        break;
                    }
                default:
                    Console.WriteLine($"WARNING: unknown detail on ChildrenChanged event: {signal.detail}");
                    break;
            }
        }

        internal void AtSpiPropertyChange(string detail, object value)
        {
            switch (detail)
            {
                case "accessible-role":
                    {
                        if (value is uint uval)
                            value = (int)uval;
                        if (value is int ival && (!RoleKnown || ival != Role))
                        {
                            RoleKnown = true;
                            Role = ival;
                            if (MatchesDebugCondition())
                            {
                                if (Role > 0 && Role < role_names.Length)
                                    Utils.DebugWriteLine($"{this}.spi_role: {role_names[Role]}");
                                else
                                    Utils.DebugWriteLine($"{this}.spi_role: {Role}");
                            }
                            PropertyChanged("spi_role");
                        }
                        else if (value is null)
                        {
                            if (fetching_role || RoleKnown)
                                Utils.RunTask(FetchRole());
                        }
                        else
                        {
                            Console.WriteLine($"WARNING: unexpected type for accessible-role: {value.GetType()}");
                        }
                        break;
                    }
                default:
                    break;
            }
        }

        internal void AtSpiStateChanged(AtSpiSignal signal)
        {
            if (!StateKnown)
                return;
            var new_state = AtSpiState.SetState(State, signal.detail, signal.detail1 != 0);
            if (new_state is null)
                return;
            if (StructuralComparisons.StructuralEqualityComparer.Equals(State, new_state))
                return;
            State = new_state;
            if (MatchesDebugCondition())
            {
                var action = (signal.detail1 != 0) ? "added" : "removed";
                Utils.DebugWriteLine($"{this}.spi_state: {new AtSpiState(State)} ({signal.detail} {action})");
            }
            PropertyChanged("spi_state");
        }

        private void AncestorBoundsChanged()
        {
            abs_pos_change_count++;
            if (watching_abs_pos)
            {
                Utils.RunTask(FetchAbsPos(true));
            }
            foreach (var child in Children)
            {
                if (child is AtSpiElement ch)
                {
                    ch.AncestorBoundsChanged();
                }
            }
        }

        internal void AtSpiBoundsChanged(AtSpiSignal signal)
        {
            AncestorBoundsChanged();
        }

        public async override Task<(bool, int, int)> GetClickablePoint()
        {
            var result = await base.GetClickablePoint();
            if (result.Item1)
                return result;

            try
            {
                var bounds = await CallMethod(Root.Connection, Peer, Path,
                    IFACE_COMPONENT, "GetExtents", (uint)0, ReadMessageExtents);
                return (true, bounds.Item1 + bounds.Item3 / 2, bounds.Item2 + bounds.Item4 / 2);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return (false, 0, 0);
            }
        }

        public async Task DoAction(int index)
        {
            bool success;
            try
            {
                success = await CallMethod(Root.Connection, Peer, Path,
                    IFACE_ACTION, "DoAction", index, ReadMessageBoolean);
            }
            catch (DBusException e)
            {
                if (!IsExpectedException(e))
                    throw;
                return;
            }
            if (!success)
            {
                Utils.DebugWriteLine($"WARNING: {this}.spi_action({index}) failed");
            }
        }
    }
}
