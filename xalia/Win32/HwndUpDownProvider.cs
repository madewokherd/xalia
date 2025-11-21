using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal class HwndUpDownProvider : UiDomProviderBase, IWin32Styles
    {
        public HwndUpDownProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;

        static UiDomEnum role = new UiDomEnum(new string[] { "spinner", "spin_button", "spinbutton" });

        static string[] style_names =
        {
            "wrap",
            "setbuddyint",
            "alignright",
            "alignleft",
            "autobuddy",
            "arrowkeys",
            "horz",
            "nothousands",
            "hottrack"
        };

        static Dictionary<string,int> style_flags;

        static HwndUpDownProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 1 << i;
            }
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_up_down":
                case "is_hwnd_updown":
                    return UiDomBoolean.True;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "updown":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "spinner":
                case "spin_button":
                case "spinbutton":
                    return UiDomBoolean.True;
            }
            if (style_flags.TryGetValue(identifier, out int style))
            {
                return UiDomBoolean.FromBool((HwndProvider.Style & style) != 0);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public void GetStyleNames(int style, List<string> names)
        {
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                if ((HwndProvider.Style & (1 << i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }
    }
}