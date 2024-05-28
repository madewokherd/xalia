
using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndEditProvider : UiDomProviderBase, IWin32Styles
    {
        public HwndEditProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }
        public HwndProvider HwndProvider { get; }

        public IntPtr Hwnd => HwndProvider.Hwnd;
        public Win32Connection Connection => HwndProvider.Connection;
        public UiDomElement Element => HwndProvider.Element;
        public int Tid => HwndProvider.Tid;

        public CommandThread CommandThread => HwndProvider.CommandThread;

        static UiDomEnum role = new UiDomEnum(new string[] { "edit", "text_box", "textbox" });

        static string[] style_names =
        {
            "center",
            "right",
            "multiline",
            "uppercase",
            "lowercase",
            "password",
            "autovscroll",
            "autohscroll",
            "nohidesel",
            "combo",
            "oemconvert",
            "readonly",
            "wantreturn",
            "number"
        };

        static Dictionary<string,int> style_flags;

        static HwndEditProvider()
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
                case "is_hwnd_edit":
                case "is_hwndedit":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "edit":
                case "text_box":
                case "textbox":
                    return UiDomBoolean.True;
                case "left":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & (ES_CENTER | ES_RIGHT)) == 0);
            }
            if (style_flags.TryGetValue(identifier, out var flag))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((HwndProvider.Style & flag) != 0);
            }
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public void GetStyleNames(int style, List<string> names)
        {
            if ((style & (ES_CENTER | ES_RIGHT)) == 0)
                names.Add("left");
            for (int i=0; i<style_names.Length; i++)
            {
                if ((style & (1 << i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }
    }
}
