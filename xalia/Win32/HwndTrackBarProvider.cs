using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndTrackBarProvider : UiDomProviderBase, IWin32Styles
    {
        public HwndTrackBarProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;

        static UiDomEnum role = new UiDomEnum(new string[] { "slider" });

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "line_size", "win32_track_bar_line_size" },
            { "minimum_increment", "win32_track_bar_line_size" },
            { "small_change", "win32_track_bar_line_size" },
        };

        static string[] style_names =
        {
            "transparentbkgnd",
            "notifybeforeremove",
            "downisleft",
            "reversed",
            "tooltips",
            "nothumb",
            "fixedlength",
            "enableselrange",
            "noticks",
            "both"
        };

        static Dictionary<string,int> style_flags;

        static HwndTrackBarProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x1000 >> i;
            }
        }

        public int LineSize { get; private set; }
        public bool LineSizeKnown { get; private set; }

        public override void DumpProperties(UiDomElement element)
        {
            if (LineSizeKnown)
                Utils.DebugWriteLine($"  win32_track_bar_line_size: {LineSize}");
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_track_bar":
                case "is_hwnd_trackbar":
                    return UiDomBoolean.True;
                case "win32_track_bar_line_size":
                    depends_on.Add((element, new IdentifierExpression("win32_track_bar_line_size")));
                    if (LineSizeKnown)
                        return new UiDomInt(LineSize);
                    return UiDomUndefined.Instance;
            }
            return UiDomUndefined.Instance;
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "slider":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "autoticks":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & TBS_AUTOTICKS) != 0);
                case "vertical":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & TBS_VERT) != 0);
                case "horizontal":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & TBS_VERT) == 0);
                case "left":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (TBS_VERT|TBS_LEFT|TBS_BOTH)) == (TBS_VERT|TBS_LEFT));
                case "right":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (TBS_VERT|TBS_LEFT|TBS_BOTH)) == TBS_VERT);
                case "top":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (TBS_VERT|TBS_TOP|TBS_BOTH)) == TBS_TOP);
                case "bottom":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (TBS_VERT|TBS_TOP|TBS_BOTH)) == 0);
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
            return UiDomUndefined.Instance;
        }

        public void GetStyleNames(int style, List<string> names)
        {
            if ((HwndProvider.Style & TBS_AUTOTICKS) != 0)
                names.Add("autoticks");
            if ((HwndProvider.Style & TBS_VERT) != 0)
                names.Add("vertical");
            else
                names.Add("horizontal");
            switch (HwndProvider.Style & (TBS_VERT|TBS_TOP|TBS_BOTH))
            {
                case TBS_VERT | TBS_LEFT:
                    names.Add("left");
                    break;
                case TBS_VERT:
                    names.Add("right");
                    break;
                case TBS_TOP:
                    names.Add("top");
                    break;
                case 0:
                    names.Add("bottom");
                    break;
            }
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                if ((HwndProvider.Style & (0x1000 >> i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_track_bar_line_size":
                        Element.EndPollProperty(expression);
                        return true;
                }
            }
            return false;
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_track_bar_line_size":
                        Element.PollProperty(expression, RefreshLineSize, 2000);
                        return true;
                }
            }
            return false;
        }

        private async Task RefreshLineSize()
        {
            int result;
            try
            {
                result = (int)await SendMessageAsync(Hwnd, TBM_GETLINESIZE, IntPtr.Zero, IntPtr.Zero);
                if (result == 0)
                    result = 1;
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
                return;
            }
            if (!LineSizeKnown || LineSize != result)
            {
                LineSizeKnown = true;
                LineSize = result;

                if (Element.MatchesDebugCondition())
                    Utils.DebugWriteLine($"{Element}.win32_track_bar_line_size: {LineSize}");

                Element.PropertyChanged("win32_track_bar_line_size");
            }
        }
    }
}