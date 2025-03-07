using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndButtonProvider : UiDomProviderBase, IWin32Styles
    {
        public HwndButtonProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }

        public IntPtr Hwnd => HwndProvider.Hwnd;

        public UiDomElement Element => HwndProvider.Element;

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
            new UiDomEnum(new string[] { "ownerdraw" }),
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
            { "default", "win32_button_default" },
        };

        public int ButtonState { get; private set; }
        public bool ButtonStateKnown { get; private set; }
        private bool _watchingButtonState;
        private int _buttonStateChangeCount;

        public bool CanBeChecked()
        {
            switch (HwndProvider.Style & BS_TYPEMASK)
            {
                case BS_AUTOCHECKBOX:
                case BS_AUTORADIOBUTTON:
                case BS_AUTO3STATE:
                case BS_CHECKBOX:
                case BS_RADIOBUTTON:
                case BS_3STATE:
                    return true;
                default:
                    return false; 
            }
        }

        public List<string> ButtonStateAsStringList()
        {
            var result = new List<string>();
            if ((ButtonState & BST_CHECKED) != 0)
                result.Add("checked");
            if ((ButtonState & BST_INDETERMINATE) != 0)
                result.Add("indeterminate");
            if ((ButtonState & (BST_CHECKED|BST_INDETERMINATE)) == 0 && CanBeChecked())
                result.Add("unchecked");
            if ((ButtonState & BST_PUSHED) != 0)
                result.Add("pushed");
            if ((ButtonState & BST_FOCUS) != 0)
                result.Add("focus");
            if ((ButtonState & BST_HOT) != 0)
                result.Add("hot");
            if ((ButtonState & BST_DROPDOWNPUSHED) != 0)
                result.Add("dropdownpushed");
            return result;
        }

        public string ButtonStateAsString()
        {
            return $"0x{ButtonState:x} [{string.Join("|", ButtonStateAsStringList())}]";
        }

        public override void DumpProperties(UiDomElement element)
        {
            var dialog = element.Parent?.ProviderByType<HwndDialogProvider>();
            if (!(dialog is null) && dialog.DefIdKnown && dialog.DefId == HwndProvider.ControlId)
            {
                Utils.DebugWriteLine("  win32_button_default: true");
            }
            if (ButtonStateKnown)
                Utils.DebugWriteLine($"  win32_button_state: {ButtonStateAsString()}");
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
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
                case "win32_button_default":
                    if (!(element.Parent is null))
                    {
                        depends_on.Add((element.Parent, new IdentifierExpression("win32_dialog_defid")));
                        var dialog = element.Parent.ProviderByType<HwndDialogProvider>();
                        if (!(dialog is null) && dialog.DefIdKnown)
                        {
                            return UiDomBoolean.FromBool(HwndProvider.ControlId == dialog.DefId);
                        }
                    }
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    switch (HwndProvider.Style & BS_TYPEMASK)
                    {
                        case BS_DEFPUSHBUTTON:
                        case BS_DEFSPLITBUTTON:
                        case BS_DEFCOMMANDLINK:
                            return UiDomBoolean.True;
                        default:
                            return UiDomBoolean.False;
                    }
                case "win32_button_state":
                    depends_on.Add((element, new IdentifierExpression("win32_button_state")));
                    if (ButtonStateKnown)
                        return new UiDomInt(ButtonState);
                    break;
            }
            return UiDomUndefined.Instance;
        }

        private static async Task SendClick(UiDomRoutineAsync obj)
        {
            var provider = obj.Element.ProviderByType<HwndProvider>();
            try
            {
                await SendMessageAsync(provider.Hwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
            }
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
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
                case "unchecked":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    if (CanBeChecked())
                    {
                        depends_on.Add((element, new IdentifierExpression("win32_button_state")));
                        if (ButtonStateKnown)
                            return UiDomBoolean.FromBool((ButtonState & (BST_CHECKED | BST_INDETERMINATE)) == 0);
                    }
                    break;
                case "checked":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    if (CanBeChecked())
                    {
                        depends_on.Add((element, new IdentifierExpression("win32_button_state")));
                        if (ButtonStateKnown)
                            return UiDomBoolean.FromBool((ButtonState & BST_CHECKED) != 0);
                    }
                    break;
                case "indeterminate":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    if (CanBeChecked())
                    {
                        depends_on.Add((element, new IdentifierExpression("win32_button_state")));
                        if (ButtonStateKnown)
                            return UiDomBoolean.FromBool((ButtonState & BST_INDETERMINATE) != 0);
                    }
                    break;
                case "pushed":
                    depends_on.Add((element, new IdentifierExpression("win32_button_state")));
                    if (ButtonStateKnown)
                        return UiDomBoolean.FromBool((ButtonState & BST_PUSHED) != 0);
                    break;
                case "focus":
                    depends_on.Add((element, new IdentifierExpression("win32_button_state")));
                    if (ButtonStateKnown)
                        return UiDomBoolean.FromBool((ButtonState & BST_FOCUS) != 0);
                    break;
                case "hot":
                    depends_on.Add((element, new IdentifierExpression("win32_button_state")));
                    if (ButtonStateKnown)
                        return UiDomBoolean.FromBool((ButtonState & BST_HOT) != 0);
                    break;
                case "dropdownpushed":
                    depends_on.Add((element, new IdentifierExpression("win32_button_state")));
                    if (ButtonStateKnown)
                        return UiDomBoolean.FromBool((ButtonState & BST_DROPDOWNPUSHED) != 0);
                    break;
            }
            return UiDomUndefined.Instance;
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_button_state":
                        _watchingButtonState = false;
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
                    case "win32_button_state":
                        _watchingButtonState = true;
                        if (!ButtonStateKnown)
                            Utils.RunTask(FetchButtonState());
                        return true;
                }
            }
            return false;
        }

        private async Task FetchButtonState()
        {
            var old_count = _buttonStateChangeCount;
            int result;
            try
            {
                result = unchecked((int)(long)await SendMessageAsync(Hwnd, BM_GETSTATE, IntPtr.Zero, IntPtr.Zero));
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
                return;
            }
            if (_buttonStateChangeCount != old_count)
                return;
            if (!ButtonStateKnown || ButtonState != result)
            {
                ButtonStateKnown = true;
                ButtonState = result;
                if (Element.MatchesDebugCondition())
                    Utils.DebugWriteLine($"{Element}.win32_button_state: {ButtonStateAsString()}");
                Element.PropertyChanged("win32_button_state");
            }
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

        public void MsaaStateChange()
        {
            _buttonStateChangeCount++;
            if (_watchingButtonState)
                Utils.RunTask(FetchButtonState());
            else
                ButtonStateKnown = false;
        }
    }
}
