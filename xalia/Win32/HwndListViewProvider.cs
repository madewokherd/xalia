using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListViewProvider : HwndItemListProvider, IWin32Styles
    {
        internal HwndListViewProvider(HwndProvider hwndProvider) : base(hwndProvider) { }

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

        public void GetStyleNames(int style, List<string> names)
        {
            switch (style & LVS_TYPEMASK)
            {
                case LVS_ICON:
                    names.Add("icon");
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

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_listview":
                case "is_hwnd_list_view":
                    return UiDomBoolean.True;
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
    }
}
