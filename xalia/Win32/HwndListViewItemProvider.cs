using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListViewItemProvider : UiDomProviderBase, IWin32LocationChange
    {
        public HwndListViewItemProvider(HwndListViewProvider parent, UiDomElement element)
        {
            Parent = parent;
            Element = element;
        }

        public HwndListViewProvider Parent { get; }

        public UiDomElement Element { get; }

        public int ChildId
        {
            get
            {
                return Element.IndexInParent + Parent.FirstChildId;
            }
        }

        static readonly UiDomEnum item_role = new UiDomEnum(new string[] { "list_item", "listitem" });
        static readonly UiDomEnum icon_role = new UiDomEnum(new string[] { "icon" });
        static readonly UiDomEnum row_role = new UiDomEnum(new string[] { "row", "table_row", "tablerow" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "bounds_x", "win32_bounds_x" },
            { "bounds_y", "win32_bounds_y" },
            { "bounds_width", "win32_bounds_width" },
            { "bounds_height", "win32_bounds_height" },
            { "icon_x", "win32_icon_x" },
            { "icon_y", "win32_icon_y" },
            { "icon_width", "win32_icon_width" },
            { "icon_height", "win32_icon_height" },
            { "label_x", "win32_label_x" },
            { "label_y", "win32_label_y" },
            { "label_width", "win32_label_width" },
            { "label_height", "win32_label_height" },
            { "selectbounds_x", "win32_selectbounds_x" },
            { "selectbounds_y", "win32_selectbounds_y" },
            { "selectbounds_width", "win32_selectbounds_width" },
            { "selectbounds_height", "win32_selectbounds_height" },
            { "x", "win32_selectbounds_x" },
            { "y", "win32_selectbounds_y" },
            { "width", "win32_selectbounds_width" },
            { "height", "win32_selectbounds_height" },
        };

        static readonly string[] bounds_names = new string[] { "win32_bounds", "win32_icon", "win32_label", "win32_selectbounds" };
        enum Coord { X, Y, Width, Height }
        private RECT[] bounds_rects = new RECT[4];
        private bool[] watching_bounds = new bool[4];
        private bool[] bounds_known = new bool[4];

        private Win32RemoteProcessMemory remote_process_memory;

        public override void DumpProperties(UiDomElement element)
        {
            Utils.DebugWriteLine($"  msaa_child_id: {ChildId}");
            Parent.HwndProvider.ChildDumpProperties();
            for (int i = 0; i < 4; i++)
            {
                if (bounds_known[i])
                {
                    var bounds_name = bounds_names[i];
                    Utils.DebugWriteLine($"  {bounds_name}_x: {bounds_rects[i].left}");
                    Utils.DebugWriteLine($"  {bounds_name}_y: {bounds_rects[i].top}");
                    Utils.DebugWriteLine($"  {bounds_name}_width: {bounds_rects[i].width}");
                    Utils.DebugWriteLine($"  {bounds_name}_height: {bounds_rects[i].height}");
                }
            }
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_list_view_item":
                case "is_hwnd_listview_item":
                case "is_hwnd_subelement":
                    return UiDomBoolean.True;
                case "win32_bounds_x":
                    return EvaluateBounds(LVIR_BOUNDS, Coord.X, depends_on);
                case "win32_bounds_y":
                    return EvaluateBounds(LVIR_BOUNDS, Coord.Y, depends_on);
                case "win32_bounds_width":
                    return EvaluateBounds(LVIR_BOUNDS, Coord.Width, depends_on);
                case "win32_bounds_height":
                    return EvaluateBounds(LVIR_BOUNDS, Coord.Height, depends_on);
                case "win32_icon_x":
                    return EvaluateBounds(LVIR_ICON, Coord.X, depends_on);
                case "win32_icon_y":
                    return EvaluateBounds(LVIR_ICON, Coord.Y, depends_on);
                case "win32_icon_width":
                    return EvaluateBounds(LVIR_ICON, Coord.Width, depends_on);
                case "win32_icon_height":
                    return EvaluateBounds(LVIR_ICON, Coord.Height, depends_on);
                case "win32_label_x":
                    return EvaluateBounds(LVIR_LABEL, Coord.X, depends_on);
                case "win32_label_y":
                    return EvaluateBounds(LVIR_LABEL, Coord.Y, depends_on);
                case "win32_label_width":
                    return EvaluateBounds(LVIR_LABEL, Coord.Width, depends_on);
                case "win32_label_height":
                    return EvaluateBounds(LVIR_LABEL, Coord.Height, depends_on);
                case "win32_selectbounds_x":
                    return EvaluateBounds(LVIR_SELECTBOUNDS, Coord.X, depends_on);
                case "win32_selectbounds_y":
                    return EvaluateBounds(LVIR_SELECTBOUNDS, Coord.Y, depends_on);
                case "win32_selectbounds_width":
                    return EvaluateBounds(LVIR_SELECTBOUNDS, Coord.Width, depends_on);
                case "win32_selectbounds_height":
                    return EvaluateBounds(LVIR_SELECTBOUNDS, Coord.Height, depends_on);
            }
            return Parent.HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
        }

        private UiDomValue EvaluateBounds(int bounds_type, Coord coordinate_type, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            depends_on.Add((Parent.Element, new IdentifierExpression("win32_pos")));
            depends_on.Add((Element, new IdentifierExpression(bounds_names[bounds_type])));

            if (bounds_known[bounds_type])
            {
                var adjusted_rect = Parent.HwndProvider.ClientRectToScreen(bounds_rects[bounds_type]);
                switch (coordinate_type)
                {
                    case Coord.X:
                        return new UiDomInt(adjusted_rect.left);
                    case Coord.Y:
                        return new UiDomInt(adjusted_rect.top);
                    case Coord.Width:
                        return new UiDomInt(adjusted_rect.width);
                    case Coord.Height:
                        return new UiDomInt(adjusted_rect.height);
                }
            }

            return UiDomUndefined.Instance;
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "role":
                case "control_type":
                    switch (Parent.EvaluateView(depends_on))
                    {
                        case LV_VIEW_DETAILS:
                            return row_role;
                        case LV_VIEW_ICON:
                        case LV_VIEW_SMALLICON:
                            return icon_role;
                        case -1: // view unknown
                            break;
                        default:
                            return item_role;
                    }
                    break;
                case "row":
                case "table_row":
                case "tablerow":
                    switch (Parent.EvaluateView(depends_on))
                    {
                        case LV_VIEW_DETAILS:
                            return UiDomBoolean.True;
                        case -1: // view unknown
                            break;
                        default:
                            return UiDomBoolean.False;
                    }
                    break;
                case "icon":
                    switch (Parent.EvaluateView(depends_on))
                    {
                        case LV_VIEW_ICON:
                        case LV_VIEW_SMALLICON:
                            return UiDomBoolean.True;
                        case -1: // view unknown
                            break;
                        default:
                            return UiDomBoolean.False;
                    }
                    break;
                case "index":
                    return new UiDomInt(ChildId - 1);
                case "listitem":
                case "list_item":
                    switch (Parent.EvaluateView(depends_on))
                    {
                        case LV_VIEW_ICON:
                        case LV_VIEW_SMALLICON:
                        case LV_VIEW_DETAILS:
                            return UiDomBoolean.False;
                        case -1: // view unknown
                            break;
                        default:
                            return UiDomBoolean.True;
                    }
                    break;
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return Parent.HwndProvider.ChildEvaluateIdentifierLate(identifier, depends_on);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_bounds":
                        return WatchProperty(LVIR_BOUNDS);
                    case "win32_icon":
                        return WatchProperty(LVIR_ICON);
                    case "win32_label":
                        return WatchProperty(LVIR_LABEL);
                    case "win32_selectbounds":
                        return WatchProperty(LVIR_SELECTBOUNDS);
                }
            }
            return base.WatchProperty(element, expression);
        }

        private async Task RefreshBounds(int bounds_type)
        {
            if (remote_process_memory is null)
                remote_process_memory = Win32RemoteProcessMemory.FromPid(Parent.Pid);
            RECT rc = new RECT();
            rc.left = bounds_type;
            IntPtr result;
            using (var memory = remote_process_memory.WriteAlloc(rc))
            {
                result = await SendMessageAsync(Parent.Hwnd, LVM_GETITEMRECT, (IntPtr)(ChildId - 1), new IntPtr((long)memory.Address));
                rc = memory.Read<RECT>();
            }

            var prop_name = bounds_names[bounds_type];
            if (result == IntPtr.Zero)
            {
                if (bounds_known[bounds_type])
                {
                    bounds_known[bounds_type] = false;
                    Element.PropertyChanged(prop_name, "undefined");
                }
            }
            else
            {
                if (!bounds_known[bounds_type] || !rc.Equals(bounds_rects[bounds_type]))
                {
                    bounds_known[bounds_type] = true;
                    bounds_rects[bounds_type] = rc;
                    Element.PropertyChanged(prop_name, $"{rc.left},{rc.top} {rc.width}x{rc.height}");
                }
            }
        }

        private bool WatchProperty(int bounds_type)
        {
            watching_bounds[bounds_type] = true;
            if (!bounds_known[bounds_type])
                Utils.RunTask(RefreshBounds(bounds_type));
            return true;
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_bounds":
                        return UnwatchProperty(LVIR_BOUNDS);
                    case "win32_icon":
                        return UnwatchProperty(LVIR_ICON);
                    case "win32_label":
                        return UnwatchProperty(LVIR_LABEL);
                    case "win32_selectbounds":
                        return UnwatchProperty(LVIR_SELECTBOUNDS);
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        private bool UnwatchProperty(int bounds_type)
        {
            watching_bounds[bounds_type] = false;
            return true;
        }

        public override void NotifyElementRemoved(UiDomElement element)
        {
            if (!(remote_process_memory is null))
            {
                remote_process_memory.Unref();
                remote_process_memory = null;
            }
            base.NotifyElementRemoved(element);
        }

        public void InvalidateBounds()
        {
            for (int i = 0; i < 4; i++)
            {
                if (bounds_known[i])
                {
                    if (watching_bounds[i])
                        Utils.RunTask(RefreshBounds(i));
                    else
                        bounds_known[i] = false;
                }
            }
        }

        public void MsaaLocationChange()
        {
            InvalidateBounds();
        }
    }
}