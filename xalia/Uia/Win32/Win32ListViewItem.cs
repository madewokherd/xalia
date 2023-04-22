using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Interop;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Uia.Win32
{
    internal class Win32ListViewItem : UiDomElement
    {
        struct rect_info
        {
            public bool Known;
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        private rect_info[] rect_infos = new rect_info[4];

        private Win32RemoteProcessMemory remote_process_memory;

        public Win32ListViewItem(Win32ListView parent, int index) : base($"Win32ListViewItem-{parent.Hwnd}-{index}", parent.Root)

        {
            Parent = parent;
            Hwnd = parent.Hwnd;
            Index = index;
        }

        protected override void SetAlive(bool value)
        {
            if (!value)
            {
                if (!(remote_process_memory is null))
                {
                    remote_process_memory.Unref();
                    remote_process_memory = null;
                }
            }
            base.SetAlive(value);
        }

        public new Win32ListView Parent { get; }
        public IntPtr Hwnd { get; }
        public int Index { get; }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "is_win32_subelement":
                case "is_win32_listview_item":
                case "item":
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
                case "list_item":
                    return UiDomBoolean.FromBool(!Parent.EvaluateIdentifier("details", root, depends_on).ToBool());
                case "table_row":
                    return UiDomBoolean.FromBool(Parent.EvaluateIdentifier("details", root, depends_on).ToBool());
                case "win32_bounds_x":
                case "win32_bounds_y":
                case "win32_bounds_width":
                case "win32_bounds_height":
                case "win32_icon_x":
                case "win32_icon_y":
                case "win32_icon_width":
                case "win32_icon_height":
                case "win32_label_x":
                case "win32_label_y":
                case "win32_label_width":
                case "win32_label_height":
                case "win32_selectbounds_x":
                case "win32_selectbounds_y":
                case "win32_selectbounds_width":
                case "win32_selectbounds_height":
                    string bounds_name = id.Substring(0, id.LastIndexOf('_'));
                    int bounds_type = BoundsTypeFromName(bounds_name);
                    string component_name = id.Substring(id.LastIndexOf('_') + 1);
                    depends_on.Add((this, new IdentifierExpression(bounds_name)));
                    if (rect_infos[bounds_type].Known)
                    {
                        switch (component_name)
                        {
                            case "x":
                                return new UiDomInt(rect_infos[bounds_type].X);
                            case "y":
                                return new UiDomInt(rect_infos[bounds_type].Y);
                            case "width":
                                return new UiDomInt(rect_infos[bounds_type].Width);
                            case "height":
                                return new UiDomInt(rect_infos[bounds_type].Height);
                        }
                    }
                    return UiDomUndefined.Instance;
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        private int BoundsTypeFromName(string bounds_name)
        {
            switch (bounds_name)
            {
                case "win32_bounds":
                    return LVIR_BOUNDS;
                case "win32_icon":
                    return LVIR_ICON;
                case "win32_label":
                    return LVIR_LABEL;
                case "win32_selectbounds":
                    return LVIR_SELECTBOUNDS;
            }
            return - 1;
        }

        protected override void DumpProperties()
        {
            foreach (string bounds_name in new string[] { "win32_bounds", "win32_icon", "win32_label", "win32_selectbounds" })
            {
                int bounds_type = BoundsTypeFromName(bounds_name);
                if (rect_infos[bounds_type].Known)
                {
                    Utils.DebugWriteLine($"  {this}.{bounds_name}_x: {rect_infos[bounds_type].X}");
                    Utils.DebugWriteLine($"  {this}.{bounds_name}_y: {rect_infos[bounds_type].Y}");
                    Utils.DebugWriteLine($"  {this}.{bounds_name}_width: {rect_infos[bounds_type].Width}");
                    Utils.DebugWriteLine($"  {this}.{bounds_name}_height: {rect_infos[bounds_type].Height}");
                }
            }
            base.DumpProperties();
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_bounds":
                        PollProperty(expression, RefreshBounds, 2000);
                        break;
                    case "win32_icon":
                        PollProperty(expression, RefreshIconRect, 2000);
                        break;
                    case "win32_label":
                        PollProperty(expression, RefreshLabelRect, 2000);
                        break;
                    case "win32_selectbounds":
                        PollProperty(expression, RefreshSelectBounds, 2000);
                        break;
                }
            }
            base.WatchProperty(expression);
        }

        protected override void UnwatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                int bounds_type = BoundsTypeFromName(id.Name);
                if (bounds_type != -1)
                {
                    EndPollProperty(expression);
                    rect_infos[bounds_type].Known = false;
                }
            }
            base.UnwatchProperty(expression);
        }

        private async Task RefreshRect(int bounds_type, string prop_name)
        {
            if (remote_process_memory is null)
            {
                GetWindowThreadProcessId(Hwnd, out var pid);
                remote_process_memory = Win32RemoteProcessMemory.FromPid(pid);
            }
            RECT rc = new RECT();
            rc.left = bounds_type;
            IntPtr result;
            using (var memory = remote_process_memory.WriteAlloc(rc))
            {
                result = await SendMessageAsync(Hwnd, LVM_GETITEMRECT, (IntPtr)Index, new IntPtr((long)memory.Address));
                rc = memory.Read<RECT>();
            }
            if (result == IntPtr.Zero)
            {
                if (rect_infos[bounds_type].Known)
                {
                    rect_infos[bounds_type].Known = false;
                    PropertyChanged(prop_name, "undefined");
                }
            }
            else
            {
              
                if (!rect_infos[bounds_type].Known || rc.left != rect_infos[bounds_type].X ||
                    rc.top != rect_infos[bounds_type].Y ||
                    rc.right - rc.left != rect_infos[bounds_type].Width || rc.bottom - rc.top != rect_infos[bounds_type].Height)
                {
                    rect_infos[bounds_type].Known = true;
                    rect_infos[bounds_type].X = rc.left;
                    rect_infos[bounds_type].Y = rc.top;
                    rect_infos[bounds_type].Width = rc.right - rc.left;
                    rect_infos[bounds_type].Height = rc.bottom - rc.top;
                    PropertyChanged(prop_name, $"{rect_infos[bounds_type].X},{rect_infos[bounds_type].Y} {rect_infos[bounds_type].Width}x{rect_infos[bounds_type].Height}");
                }
            }
        }

        private Task RefreshBounds()
        {
            return RefreshRect(LVIR_BOUNDS, "win32_bounds");
        }

        private Task RefreshIconRect()
        {
            return RefreshRect(LVIR_ICON, "win32_icon");
        }

        private Task RefreshLabelRect()
        {
            return RefreshRect(LVIR_LABEL, "win32_label");
        }

        private Task RefreshSelectBounds()
        {
            return RefreshRect(LVIR_SELECTBOUNDS, "win32_selectbounds");
        }
    }
}
