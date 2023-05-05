using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndButtonProvider : IUiDomProvider, IWin32Styles
    {
        public HwndButtonProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }

        static UiDomEnum[] button_roles =
        {
            new UiDomEnum(new string[] { "pushbutton", "push_button", "button" }),
            new UiDomEnum(new string[] { "defpushbutton", "pushbutton", "push_button", "button" }),
            new UiDomEnum(new string[] { "checkbox", "check_box" }),
            new UiDomEnum(new string[] { "autocheckbox", "checkbox", "check_box" }),
            new UiDomEnum(new string[] { "radiobutton", "radio_button" }),
            new UiDomEnum(new string[] { "3state", "checkbox", "check_box" }),
            new UiDomEnum(new string[] { "auto3state", "checkbox", "check_box" }),
            new UiDomEnum(new string[] { "groupbox", "group_box", "frame" }),
            new UiDomEnum(new string[] { "userbutton", "button" }),
            new UiDomEnum(new string[] { "autoradiobutton", "radiobutton", "radio_button" }),
            new UiDomEnum(new string[] { "pushbox", "pushbutton", "push_button", "button" }),
            new UiDomEnum(new string[] { "ownerdraw", "button" }),
            new UiDomEnum(new string[] { "splitbutton", "pushbutton", "push_button", "button" }),
            new UiDomEnum(new string[] { "defsplitbutton", "splitbutton", "pushbutton", "push_button", "button" }),
            new UiDomEnum(new string[] { "commandlink", "pushbutton", "push_button", "button" }),
            new UiDomEnum(new string[] { "defcommandlink", "pushbutton", "push_button", "button" }),
        };

        static string[] style_names =
        {
            "flat",
            "notify",
            "multiline",
            "pushlike",
            null, // bottom
            null, // top
            null, // right
            null, // left
            "has_bitmap",
            "has_icon",
            "has_text",
            "lefttext",
        };

        static Dictionary<string, int> style_flags;
        static HashSet<string> role_names;

        static HwndButtonProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x8000 >> i;
            }

            role_names = new HashSet<string>();
            foreach (var role in button_roles)
            {
                foreach (var name in role.Names)
                    role_names.Add(name);
            }
        }

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "role", "win32_button_role" },
            { "control_type", "win32_button_role" },
            { "do_default_action", "win32_button_click" },
            { "click", "win32_button_click" },
        };

        public void DumpProperties(UiDomElement element)
        {
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_button":
                    return UiDomBoolean.True;
                case "win32_button_role":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return button_roles[HwndProvider.Style & BS_TYPEMASK];
                case "win32_button_click":
                    return new UiDomRoutineAsync(element, "win32_button_click", SendClick);
            }
            return UiDomUndefined.Instance;
        }

        private static Task SendClick(UiDomRoutineAsync obj)
        {
            var provider = obj.Element.ProviderByType<HwndProvider>();
            return SendMessageAsync(provider.Hwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }

        public UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (property_aliases.TryGetValue(identifier, out var aliased))
            {
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            }
            if (role_names.Contains(identifier))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool(button_roles[HwndProvider.Style & BS_TYPEMASK].Names.Contains(identifier));
            }
            if (style_flags.TryGetValue(identifier, out var flag))
            {
                depends_on.Add((element, new IdentifierExpression("win32_style")));
                return UiDomBoolean.FromBool((HwndProvider.Style & flag) != 0);
            }
            switch (identifier)
            {
                case "left":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & BS_CENTER) == BS_LEFT);
                case "right":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & BS_CENTER) == BS_RIGHT);
                case "center":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & BS_CENTER) == BS_CENTER);
                case "top":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & BS_VCENTER) == BS_TOP);
                case "bottom":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & BS_VCENTER) == BS_BOTTOM);
                case "vcenter":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & BS_VCENTER) == BS_VCENTER);
            }
            return UiDomUndefined.Instance;
        }

        public Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public string[] GetTrackedProperties()
        {
            return null;
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
        }

        public void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
        }

        public bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public void GetStyleNames(int style, List<string> names)
        {
            names.Add(button_roles[HwndProvider.Style & BS_TYPEMASK].Names[0]);
            switch (HwndProvider.Style & BS_CENTER)
            {
                case BS_LEFT:
                    names.Add("left");
                    break;
                case BS_RIGHT:
                    names.Add("right");
                    break;
                case BS_CENTER:
                    names.Add("center");
                    break;
            }
            switch (HwndProvider.Style & BS_VCENTER)
            {
                case BS_TOP:
                    names.Add("top");
                    break;
                case BS_BOTTOM:
                    names.Add("bottom");
                    break;
                case BS_VCENTER:
                    names.Add("vcenter");
                    break;
            }
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
    }
}