using System;
using System.Collections.Generic;
using System.Threading;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Viewer
{
    internal class UiDomViewerProvider : UiDomProviderBase
    {
        public UiDomViewerProvider(UiDomViewer uiDomViewer, UiDomRoot root, SynchronizationContext viewer_context)
        {
            UiDomViewer = uiDomViewer;
            Root = root;
            ViewerContext = viewer_context;
            ElementDescExpression = "element_identifier + (\" \" + role.name or \"\") + (\" \" + id or \"\") + (\" \" + (name or class_name or attributes.tag) or \"\")";
        }

        public UiDomViewer UiDomViewer { get; }
        public UiDomRoot Root { get; }
        public SynchronizationContext ViewerContext { get; }

        private struct ElementInfo
        {
            public UiDomElement element;
            public string[] children;
            public IDisposable children_notifier;
            public ExpressionWatcher desc_notifier;
        }

        private Dictionary<string, ElementInfo> elements = new Dictionary<string, ElementInfo>();
        private bool poke_queued;

        private string element_desc_expression;
        private string ElementDescExpression
        {
            get => element_desc_expression;
            set
            {
                element_desc_expression = value;
                element_desc_compiled = GudlParser.ParseExpression(value);
            }
        }

        private GudlExpression element_desc_compiled;

        private List<string> property_list = new List<string>
        {
            // common identifying attributes
            "element_identifier",
            "spi_accessible_id",
            "role",
            "name",
            "application_name",
            "toolkit_name",
            "win32_class_name",
            "win32_control_id",

            // element types
            "is_hwnd_element",
            "is_msaa_element",
            "is_accessible2_element",
            "is_uia_element",
            "is_uia_fragment",
            "is_win32_dialog",
            "is_hwnd_button",
            "is_hwnd_combo_box",
            "is_hwnd_header",
            "is_hwnd_track_bar",
            "is_hwnd_tab_control",
            "is_hwnd_tab_item",
            "is_hwnd_list_view",
            "is_hwnd_list_view_item",
            "is_hwnd_list_view_cell",
            "is_hwnd_static",
            "is_hwnd_tree_view",
            "is_nonclient_scrollbar",
            "is_hwnd_edit",
            "is_hwnd_richedit",
            "is_spi_element",
            "spi_supported",

            // common declarations
            "interactable",
            "valid_target",
            "targetable",
            "targeted",
            "x",
            "y",
            "width",
            "height",
            "primary_action",
            "supported",
            "recurse_method",
            "do_default_action",
            "target_left_candidate",
            "target_right_candidate",
            "target_up_candidate",
            "target_down_candidate",
            "index_in_parent",

            // win32 info
            "hex(win32_hwnd)",
            "hex(win32_pid)",
            "hex(win32_tid)",
            "win32_style_names",
            "win32_window_text",
            "win32_real_class_name",

            // ia2 info
            "ia2_role",

            // msaa info
            "msaa_role",
            "msaa_state_names",
            "msaa_name",
            "msaa_default_action",
            "msaa_child_id",

            // uia info
            "uia_control_type",
            "uia_class_name",
            "uia_automation_id",
            "uia_framework_id",
            "uia_is_enabled",
            "uia_is_offscreen",

            // win32 specialized info
            "win32_dialog_defid",
            "win32_button_role",
            "win32_button_default",
            "win32_button_state",
            "win32_combo_box_list_element",
            "win32_combo_box_dropped_state",
            "win32_track_bar_line_size",
            "win32_selection_index",
            "win32_item_height",
            "win32_top_index",
            // "win32_item_rects",
            "win32_is_comctl6",
            "win32_view",
            "win32_extended_listview_style",
            // "win32_bounds_x", etc.
            "win32_vertical",
            "win32_horizontal",
            "win32_state",
            "win32_minimum_value",
            "win32_maximum_value",
            "win32_page",
            "win32_value",
            "win32_item_count",

            // spi info
            "spi_role",
            "spi_state",
            "spi_name",
            "spi_description",
            "spi_attributes",
            "spi_action",
            "spi_toolkit_name",
            "spi_minimum_value",
            "spi_maximum_value",
            "spi_minimum_increment",

            // location info
            "win32_x",
            "win32_y",
            "win32_width",
            "win32_height",
            "win32_client_x",
            "win32_client_y",
            "win32_client_width",
            "win32_client_height",
            "win32_bounds_x",
            "win32_bounds_y",
            "win32_bounds_width",
            "win32_bounds_height",
            "win32_icon_x",
            "win32_icon_y",
            "win32_icon_width",
            "win32_icon_height",
            "win32_label_x",
            "win32_label_y",
            "win32_label_width",
            "win32_label_height",
            "win32_selectbounds_x",
            "win32_selectbounds_y",
            "win32_selectbounds_width",
            "win32_selectbounds_height",
            "msaa_x",
            "msaa_y",
            "msaa_width",
            "msaa_height",
            "uia_x",
            "uia_y",
            "uia_width",
            "uia_height",
            "spi_abs_x",
            "spi_abs_y",
            "spi_abs_width",
            "spi_abs_height",

            // routines
            "win32_button_click",
            "win32_combo_box_show_drop_down",
            "win32_combo_box_hide_drop_down",
            "win32_toggle_checked",
            "msaa_do_default_action",
            "spi_select",
            "spi_deselect",
            "spi_toggle_selected",
            "spi_grab_focus",
            "spi_clear_selection",
            "win32_enable_window",
            "win32_disable_window",
            "win32_set_focus",

            // flags
            "win32_gui_active",
            "win32_gui_focus",
            "win32_gui_capture",
            "win32_gui_menuowner",
            "win32_gui_movesize",
        };

        private List<string> root_property_list = new List<string>
        {
            "win32_gui_inmovesize",
            "win32_gui_inmenumode",
            "win32_gui_popupmenumode",
            "win32_gui_systemmenumode",
            "hex(win32_gui_hwndactive)",
            "hex(win32_gui_hwndfocus)",
            "hex(win32_gui_hwndcapture)",
            "hex(win32_gui_hwndmenuowner)",
            "hex(win32_gui_hwndmovesize)",
            "targeted_element",
            "target_move_up",
            "target_move_down",
            "target_move_left",
            "target_move_right",
            "target_next",
            "target_previous",
            "show_keyboard",
        };

        List<ExpressionWatcher> property_watchers;

        internal void DoMainThreadSetup()
        {
            Root.AddGlobalProvider(this);
            Utils.RunIdle(SendFullTree);
        }

        private void SendFullTree()
        {
            SendElementChildren(null, new UiDomElement[] { Root });
        }

        private void QueuePoke()
        {
            if (!poke_queued)
            {
                Utils.RunIdle(PokeViewerThread);
                poke_queued = true;
            }
        }

        private void PokeViewerThread()
        {
            ViewerContext.Post(UiDomViewer.QueuesUpdated, null);
            poke_queued = false;
        }

        private string GetElementDesc(UiDomElement element)
        {
            var depends_on = new HashSet<(UiDomElement, GudlExpression)>();

            var result = element.Evaluate(element_desc_compiled, depends_on);

            if (result is UiDomString s)
                return s.Value;

            return $"{element.DebugId} <ERROR>";
        }

        private ElementInfo CreateElementInfo(UiDomElement element)
        {
            if (element is null)
            {
                // parent of root element, should only be used once, from SendFullTree
                var result = new ElementInfo();
                result.children = new string[0];
                return result;
            }
            if (!elements.TryGetValue(element.DebugId, out var info))
            {
                info = new ElementInfo();
                info.element = element;
                info.children = new string[0];
                info.children_notifier = element.NotifyPropertyChanged(
                    new IdentifierExpression("children"), OnElementChildrenChanged);
                info.desc_notifier = new ExpressionWatcher(element, Root, element_desc_compiled);
                info.desc_notifier.ValueChanged += OnElementDescChanged;
                elements.Add(element.DebugId, info);
            }
            return info;
        }

        private void OnElementDescChanged(object sender, EventArgs e)
        {
            var watcher = (ExpressionWatcher)sender;
            var element = (UiDomElement)watcher.Context;

            if (!elements.ContainsKey(element.DebugId))
                return;

            if (watcher.CurrentValue is UiDomString s)
                SendElementDesc(element.DebugId, s.Value);
            else
                SendElementDesc(element.DebugId, $"{element.DebugId} <ERROR>");
        }

        private void SendElementDesc(string debug_id, string desc)
        {
            UiDomViewer.TreeUpdates.Enqueue(
                new UiDomViewer.DescUpdate { element = debug_id, desc = desc }
                );
            QueuePoke();
        }

        private void OnElementChildrenChanged(UiDomElement element, GudlExpression property)
        {
            SendElementChildren(element);
        }

        private bool DeleteElementInfo(string element)
        {
            if (elements.TryGetValue(element, out var info))
            {
                info.children_notifier.Dispose();
                info.desc_notifier.Dispose();
                foreach (var child in info.children)
                {
                    DeleteElementInfo(child);
                }
                elements.Remove(element);
                return true;
            }
            return false;
        }

        private void SendElementChildren(UiDomElement element)
        {
            SendElementChildren(element, element.Children);
        }

        private void SendElementChildren(UiDomElement element, IList<UiDomElement> children)
        {
            if (!(element is null) && !elements.ContainsKey(element.DebugId))
                return;
            var info = CreateElementInfo(element);
            var prev_children = new HashSet<string>(info.children.Length);
            List<UiDomElement> new_children = new List<UiDomElement>(children.Count);
            var child_descs = new (string, string)[children.Count];
            for (int i = 0; i < child_descs.Length; i++)
            {
                var child = children[i];
                if (!prev_children.Remove(child.DebugId))
                {
                    new_children.Add(child);
                }
                child_descs[i] = (child.DebugId, GetElementDesc(child));
            }
            UiDomViewer.TreeUpdates.Enqueue(
                new UiDomViewer.ChildrenUpdate { parent = element?.DebugId, children = child_descs });
            foreach (var child in prev_children)
            {
                DeleteElementInfo(child);
            }
            foreach (var child in new_children)
            {
                CreateElementInfo(child);
                SendElementChildren(child);
            }
            QueuePoke();
        }

        internal void SetCurrentElement(string name)
        {
            if (!(property_watchers is null))
            {
                foreach (var watch in property_watchers)
                {
                    watch.Dispose();
                }
                property_watchers = null;
            }
            if (name is null)
                return;
            if (elements.TryGetValue(name, out var info))
            {
                List<string> property_expressions;
                if (info.element is UiDomRoot)
                    property_expressions = root_property_list;
                else
                    property_expressions = property_list;
                property_watchers = new List<ExpressionWatcher>(property_expressions.Count);
                foreach (var expr in property_expressions)
                {
                    var compiled = GudlParser.ParseExpression(expr);
                    var watcher = new ExpressionWatcher(info.element, Root, compiled);
                    SendPropertyValue(info.element.DebugId, compiled, watcher.CurrentValue);
                    watcher.ValueChanged += property_ValueChanged;
                    property_watchers.Add(watcher);
                }
            }
        }

        private void property_ValueChanged(object sender, EventArgs e)
        {
            var watch = (ExpressionWatcher)sender;
            var element = (UiDomElement)watch.Context;
            SendPropertyValue(element.DebugId, watch.Expression, watch.CurrentValue);
        }

        private void SendPropertyValue(string debugId, GudlExpression compiled, UiDomValue currentValue)
        {
            UiDomViewer.PropertyUpdates.Enqueue(new UiDomViewer.DataUpdate
            {
                element = debugId,
                expression = compiled,
                value = currentValue
            });
            QueuePoke();
        }
    }
}
