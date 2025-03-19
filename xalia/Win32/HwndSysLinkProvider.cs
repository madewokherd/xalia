using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal class HwndSysLinkProvider : UiDomProviderBase, IWin32Styles, IWin32NameChange
    {
        public HwndSysLinkProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;
        public Win32Connection Connection => HwndProvider.Connection;
        public UiDomRoot Root => Element.Root;
        public int Pid => HwndProvider.Pid;

        static readonly UiDomEnum role = new UiDomEnum(new[] { "sys_link", "syslink", "html_container", "htmlcontainer" });

        static string[] style_names =
        {
            "transparent",
            "ignorereturn",
            "noprefix",
            "usevisualstyle",
            "usecustomtext",
            "right"
        };

        static Dictionary<string,int> style_flags;

        static HwndSysLinkProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x1 << i;
            }
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_sys_link":
                case "is_hwnd_syslink":
                    return UiDomBoolean.True;
            }

            return UiDomUndefined.Instance;
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "sys_link":
                case "syslink":
                case "html_container":
                case "htmlcontainer":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
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
            for (int i=0; i<style_names.Length; i++)
            {
                if ((HwndProvider.Style & (0x1 << i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }

        public void MsaaNameChange()
        {
            Element.ProviderByType<AccessibleProvider>()?.MsaaChildrenReordered();
            Win32Connection.RecursiveLocationChange(Element);
            foreach (var child in Element.Children)
            {
                child.ProviderByType<AccessibleProvider>()?.MsaaNameChange();
            }
        }
    }
}
