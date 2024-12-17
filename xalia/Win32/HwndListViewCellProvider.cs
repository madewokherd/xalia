using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndListViewCellProvider : UiDomProviderBase, IUiDomScrollToProvider
    {
        public HwndListViewCellProvider(UiDomElement element, HwndListViewItemProvider row, int column)
        {
            Element = element;
            Row = row;
            Column = column;
        }

        public UiDomElement Element { get; }
        public HwndListViewItemProvider Row { get; }
        public int Column { get; }

        public UiDomRoot Root => Element.Root;

        private HwndHeaderProvider _header;

        public HwndHeaderProvider HeaderProvider
        {
            get
            {
                if (_header is null)
                {
                    // We can assume that the header control is already known if any cells exist, so
                    // we don't have to track changes to this. We may have to account for null, though.
                    var hwnd_element = HwndProvider.Element;
                    // hwnds will generally be at the end of the list 
                    for (int i = hwnd_element.RecurseMethodChildCount - 1; i >= 0; i--)
                    {
                        var header = hwnd_element.Children[i].ProviderByType<HwndHeaderProvider>();
                        if (!(header is null))
                        {
                            _header = header;
                            break;
                        }
                    }
                }
                return _header;
            }
        }

        public UiDomElement ColumnHeader
        {
            get
            {
                if (!(HeaderProvider is null))
                {
                    if (HeaderControl.RecurseMethodChildCount > Column)
                    {
                        return HeaderControl.Children[Column];
                    }
                }
                return null;
            }
        }

        public UiDomElement HeaderControl => HeaderProvider.Element;

        public HwndProvider HwndProvider => Row.Parent.HwndProvider;

        static readonly UiDomEnum role = new UiDomEnum(new string[] { "cell", "table_cell", "tablecell" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "x", "win32_x" },
            { "y", "win32_y" },
            { "width", "win32_width" },
            { "height", "win32_height" },
        };

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_list_view_cell":
                case "is_hwnd_listview_cell":
                case "is_hwnd_subelement":
                    return UiDomBoolean.True;
                case "win32_x":
                case "win32_width":
                    {
                        if (Column == 0 && identifier == "win32_x")
                        {
                            return Row.Element.EvaluateIdentifier("win32_selectbounds_x", Root, depends_on);
                        }
                        UiDomValue result = UiDomUndefined.Instance;
                        if (!(HeaderProvider is null))
                        {
                            depends_on.Add((HeaderControl, new IdentifierExpression("children")));
                            var header = ColumnHeader;
                            if (!(header is null))
                            {
                                if (identifier == "win32_x")
                                    result = header.EvaluateIdentifier("x", Root, depends_on);
                                else
                                    result = header.EvaluateIdentifier("width", Root, depends_on);
                            }
                        }
                        if (result is UiDomInt && Column == 0) // identifier == "win32_width"
                        {
                            // Sometimes selectbounds spans multiple columns (wine bug?)
                            Row.Element.EvaluateIdentifier("win32_selectbounds_x", Root, depends_on).TryToInt(out var sb_x);
                            Row.Element.EvaluateIdentifier("win32_selectbounds_width", Root, depends_on).TryToInt(out var sb_width);
                            ColumnHeader.EvaluateIdentifier("x", Root, depends_on).TryToInt(out var header_x);
                            ColumnHeader.EvaluateIdentifier("width", Root, depends_on).TryToInt(out var header_width);
                            return new UiDomInt(Math.Min(header_x + header_width - sb_x, sb_width));
                        }
                        return result;
                    }
                case "win32_y":
                    return Row.Element.EvaluateIdentifier("win32_selectbounds_y", Root, depends_on);
                case "win32_height":
                    return Row.Element.EvaluateIdentifier("win32_selectbounds_height", Root, depends_on);
            }
            return HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "role":
                case "control_type":
                    return role;
                case "cell":
                case "table_cell":
                case "tablecell":
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return HwndProvider.ChildEvaluateIdentifierLate(identifier, depends_on);
        }

        public override async Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            if (Column == 0)
            {
                return await Row.Element.GetClickablePoint();
            }
            var header = ColumnHeader;
            if (header is null)
                return (false, 0, 0);
            var column_point = await header.GetClickablePoint();
            if (!column_point.Item1)
                return (false, 0, 0);
            var row_point = await Row.Element.GetClickablePoint();
            if (!row_point.Item1)
                return (false, 0, 0);
            return (true, column_point.Item2, row_point.Item3);
        }

        public async Task<bool> ScrollToAsync()
        {
            await Row.ScrollToAsync();

            var header = HeaderControl;
            if (header is null)
                return true;

            // Check first whether column is in view
            var column_header = ColumnHeader?.ProviderByType<HwndHeaderItemProvider>();
            if (column_header is null)
                return true;

            var client_location = await column_header.QueryClientLocationAsync();

            if (!client_location.Item1)
                return true;

            // convert to listview client coordinates
            var column_rc = client_location.Item2;
            var listview = Row.Parent.Element;
            POINT header_location = default;
            MapWindowPoints(HeaderProvider.Hwnd, HwndProvider.Hwnd, ref header_location, 1);
            column_rc = column_rc.Offset(header_location);

            int xofs = 0;

            if (column_rc.left < 0)
            {
                xofs = column_rc.left;
            }
            else
            {
                GetClientRect(HwndProvider.Hwnd, out var client_rect);
                if (column_rc.right > client_rect.width)
                {
                    xofs = column_rc.right - client_rect.width;

                    if (xofs > column_rc.left)
                        xofs = column_rc.left;
                }
            }

            if (xofs == 0)
                return true;

            UiDomElement hscroll_element = null;
            HwndListViewScrollProvider hscroll = null;
            for (int i = listview.RecurseMethodChildCount; i < listview.Children.Count; i++)
            {
                var child = listview.Children[i];
                var scroll = child.ProviderByType<HwndListViewScrollProvider>();
                if (scroll is null)
                    continue;
                if (!scroll.Vertical)
                {
                    hscroll_element = child;
                    hscroll = scroll;
                    break;
                }
            }

            await hscroll.OffsetValueAsync(hscroll_element, xofs);

            return true;
        }
    }
}