using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListViewProvider : HwndItemListProvider, IWin32Styles, IWin32Scrollable
    {
        internal HwndListViewProvider(HwndProvider hwndProvider) : base(hwndProvider) { }

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "view", "win32_view" },
            { "control_type", "win32_view" },
            { "role", "win32_view" },
        };

        static string[] style_names =
        {
            "nosortheader",
            "nocolumnheader",
            "noscroll",
            "ownerdata",
            "alignleft",
            "ownerdrawfixed",
            "editlabels",
            "autoarrange",
            "nolabelwrap",
            "shareimagelists",
            "sortdescending",
            "sortascending",
            "showselalways",
            "singlesel"
        };

        static Dictionary<string,int> style_flags;

        static HwndListViewProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x8000 >> i;
            }
        }

        bool CheckingComCtl6;
        bool IsComCtl6;
        bool IsComCtl6Known;

        // TODO: Watch for notification of view change?
        bool fetching_view;
        int ViewInt;
        bool ViewKnown;

        private static UiDomValue ViewFromInt(int view)
        {
            switch (view)
            {
                // layered_pane is used by GTK icon views in AT-SPI2
                case LV_VIEW_ICON:
                    return new UiDomEnum(new string[] { "icon_view", "iconview", "layered_pane", "layeredpane" });
                case LV_VIEW_DETAILS:
                    return new UiDomEnum(new string[] { "report", "details", "table" });
                case LV_VIEW_SMALLICON:
                    return new UiDomEnum(new string[] { "small_icon", "smallicon", "layered_pane", "layeredpane"  });
                case LV_VIEW_LIST:
                    return new UiDomEnum(new string[] { "list" });
                case LV_VIEW_TILE:
                    return new UiDomEnum(new string[] { "tile", "layered_pane", "layeredpane"  });
            }
            return UiDomUndefined.Instance;
        }

        private static UiDomValue ViewFromStyle(int style)
        {
            return ViewFromInt(style & LVS_TYPEMASK);
        }

        public void GetStyleNames(int style, List<string> names)
        {
            switch (style & LVS_TYPEMASK)
            {
                case LVS_ICON:
                    names.Add("icon_view");
                    break;
                case LVS_REPORT:
                    names.Add("report");
                    break;
                case LVS_SMALLICON:
                    names.Add("smallicon");
                    break;
                case LVS_LIST:
                    names.Add("list");
                    break;
            }
            if ((style & LVS_ALIGNMASK) == LVS_ALIGNTOP)
                names.Add("aligntop");
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                if ((HwndProvider.Style & (0x8000 >> i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }

        public override void DumpProperties(UiDomElement element)
        {
            if (IsComCtl6Known)
            {
                Utils.DebugWriteLine($"  win32_is_comctl6: {IsComCtl6}");
                if (IsComCtl6)
                {
                    if (ViewKnown)
                        Utils.DebugWriteLine($"  win32_view: {ViewFromInt(ViewInt)}");
                }
                else
                {
                    Utils.DebugWriteLine($"  win32_view: {ViewFromStyle(HwndProvider.Style)}");
                }
            }
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_listview":
                case "is_hwnd_list_view":
                    return UiDomBoolean.True;
                case "win32_is_comctl6":
                    if (IsComCtl6Known)
                        return UiDomBoolean.FromBool(IsComCtl6);
                    return UiDomUndefined.Instance;
                case "win32_view":
                    if (!IsComCtl6Known)
                    {
                        depends_on.Add((element, new IdentifierExpression("win32_is_comctl6")));
                        return UiDomUndefined.Instance;
                    }
                    if (!IsComCtl6)
                    {
                        depends_on.Add((element, new IdentifierExpression("win32_style")));
                        return ViewFromStyle(HwndProvider.Style);
                    }
                    depends_on.Add((element, new IdentifierExpression("win32_view")));
                    if (ViewKnown)
                    {
                        return ViewFromInt(ViewInt);
                    }
                    return UiDomUndefined.Instance;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "aligntop":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & LVS_ALIGNMASK) == LVS_ALIGNTOP);
                case "icon_view":
                case "iconview":
                case "report":
                case "details":
                case "table":
                case "smallicon":
                case "small_icon":
                case "list":
                case "tile":
                case "layered_pane":
                case "layeredpane":
                    return element.EvaluateIdentifier("win32_view", element.Root, depends_on).
                        EvaluateIdentifier(identifier, element.Root, depends_on);
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
            {
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            }
            if (style_flags.TryGetValue(identifier, out var flag))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((HwndProvider.Style & flag) != 0);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        protected override async Task<int> FetchItemCount()
        {
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return 0;
            }
            return Utils.TruncatePtr(result);
        }
        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_is_comctl6":
                        if (!CheckingComCtl6)
                        {
                            Utils.RunTask(CheckComCtl6());
                            CheckingComCtl6 = true;
                        }
                        return true;
                    case "win32_view":
                        if (!fetching_view)
                        {
                            Utils.RunTask(FetchView());
                            fetching_view = true;
                        }
                        return true;
                }
            }
            return false;
        }

        private async Task CheckComCtl6()
        {
            // comctl6 will return -1 to indicate error, earlier versions should not recognize the message and return 0
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, LVM_SETVIEW, new IntPtr(-1), IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }

            if (result == IntPtr.Zero)
            {
                IsComCtl6 = false;
                IsComCtl6Known = true;
                Element.PropertyChanged("win32_is_comctl6", "false");
            }
            else
            {
                IsComCtl6 = true;
                IsComCtl6Known = true;
                Element.PropertyChanged("win32_is_comctl6", "true");
            }
        }

        private async Task FetchView()
        {
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, LVM_GETVIEW, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }

            ViewKnown = true;
            ViewInt = Utils.TruncatePtr(result);
            Element.PropertyChanged("win32_view", ViewFromInt(ViewInt));
        }

        public IUiDomProvider GetScrollBarProvider(NonclientScrollProvider nonclient)
        {
            return new HwndListViewScrollProvider(this, nonclient);
        }
    }
}
