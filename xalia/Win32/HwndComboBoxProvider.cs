using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndComboBoxProvider : IUiDomProvider, IWin32Styles
    {
        public HwndComboBoxProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }

        public IntPtr Hwnd => HwndProvider.Hwnd;
        public Win32Connection Connection => HwndProvider.Connection;
        public UiDomElement Element => HwndProvider.Element;
        public int Tid => HwndProvider.Tid;

        public IntPtr HwndList { get; private set; }
        private bool _fetchingComboBoxInfo;

        public void DumpProperties(UiDomElement element)
        {
            if (HwndList != IntPtr.Zero)
                Utils.DebugWriteLine($"  win32_combo_box_list_element: hwnd-{HwndList}");
        }

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "do_default_action", "win32_combo_box_show_drop_down" },
            { "expand", "win32_combo_box_show_drop_down" },
            { "show_drop_down", "win32_combo_box_show_drop_down" },
            { "collapse", "win32_combo_box_hide_drop_down" },
            { "hide_drop_down", "win32_combo_box_hide_drop_down" },
            { "list_element", "win32_combo_box_list_element" },
            { "expanded", "win32_combo_box_dropped_state" },
            { "dropped_state", "win32_combo_box_dropped_state" },
        };

        static UiDomEnum role = new UiDomEnum(new string[] { "combo_box", "combobox" });

        static string[] style_names =
        {
            "ownerdrawfixed",
            "ownerdrawvariable",
            "autohscroll",
            "oemconvert",
            "sort",
            "hasstrings",
            "nointegralheight",
            "disablenoscroll",
            null,
            "uppercase",
            "lowercase"
        };

        static Dictionary<string,int> style_flags;

        static HwndComboBoxProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x10 << i;
            }
        }

        public bool CanShowDropDown => (HwndProvider.Style & CBS_TYPEMASK) != CBS_SIMPLE;

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_combo_box":
                case "is_hwnd_combobox":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "win32_combo_box_show_drop_down":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    if (CanShowDropDown)
                    {
                        return new UiDomRoutineAsync(element, "win32_combo_box_show_drop_down", ShowDropDown);
                    }
                    break;
                case "win32_combo_box_hide_drop_down":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    if (CanShowDropDown)
                    {
                        return new UiDomRoutineAsync(element, "win32_combo_box_hide_drop_down", HideDropDown);
                    }
                    break;
                case "win32_combo_box_list_element":
                    {
                        depends_on.Add((element, new IdentifierExpression("win32_combo_box_info_static")));
                        var list_element = Connection.LookupElement(HwndList);
                        if (!(list_element is null))
                            return list_element;
                        break;
                    }
                case "win32_combo_box_dropped_state":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    if (CanShowDropDown)
                    {
                        // In theory, CB_GETDROPPEDSTATE would tell us this, but we don't get a notification
                        // when it changes. So instead, we just watch the list to see if it's visible.
                        depends_on.Add((element, new IdentifierExpression("win32_combo_box_info_static")));
                        var list_element = Connection.LookupElement(HwndList);
                        if (!(list_element is null))
                        {
                            depends_on.Add((list_element, new IdentifierExpression("win32_style")));
                            var provider = list_element.ProviderByType<HwndProvider>();
                            if (!(provider is null))
                                return UiDomBoolean.FromBool((provider.Style & WS_VISIBLE) != 0);
                        }
                    }
                    break;
            }
            return UiDomUndefined.Instance;
        }

        private async Task HideDropDown(UiDomRoutineAsync obj)
        {
            try
            {
                await SendMessageAsync(Hwnd, CB_SHOWDROPDOWN, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
            }
        }

        private async Task ShowDropDown(UiDomRoutineAsync obj)
        {
            try
            {
                await SendMessageAsync(Hwnd, CB_SHOWDROPDOWN, (IntPtr)1, IntPtr.Zero);
            }
            catch (Win32Exception e)
            {
                if (!HwndProvider.IsExpectedException(e))
                    throw;
            }
        }

        public UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "combo_box":
                case "combobox":
                    return UiDomBoolean.True;
                case "simple":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & CBS_TYPEMASK) == CBS_SIMPLE);
                case "dropdown":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & CBS_TYPEMASK) == CBS_DROPDOWN);
                case "dropdownlist":
                    depends_on.Add((element, new IdentifierExpression("win32_style")));
                    return UiDomBoolean.FromBool((HwndProvider.Style & CBS_TYPEMASK) == CBS_DROPDOWNLIST);
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

        public Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public void GetStyleNames(int style, List<string> names)
        {
            switch (HwndProvider.Style & CBS_TYPEMASK)
            {
                case CBS_SIMPLE:
                    names.Add("simple");
                    break;
                case CBS_DROPDOWN:
                    names.Add("dropdown");
                    break;
                case CBS_DROPDOWNLIST:
                    names.Add("dropdownlist");
                    break;
            }
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                if ((HwndProvider.Style & (0x10 << i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
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

        private unsafe COMBOBOXINFO FetchComboBoxInfoBackground()
        {
            COMBOBOXINFO info = new COMBOBOXINFO();
            info.cbSize = Marshal.SizeOf<COMBOBOXINFO>();
            IntPtr ptr = Marshal.AllocCoTaskMem(info.cbSize);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                var ret = SendMessageW(Hwnd, CB_GETCOMBOBOXINFO, IntPtr.Zero, ptr);
                if (ret == IntPtr.Zero)
                    return default;
                return Marshal.PtrToStructure<COMBOBOXINFO>(ptr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }

        private async Task FetchComboBoxInfo()
        {
            COMBOBOXINFO info;
            info = await Connection.CommandThread.OnBackgroundThread(() =>
            {
                return FetchComboBoxInfoBackground();
            }, Tid + 1);
            if (info.cbSize == 0)
                // message failed
                return;

            HwndList = info.hwndList;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"{Element}.win32_combo_box_list_element: hwnd-{HwndList}");
            Element.PropertyChanged("win32_combo_box_info_static");
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_combo_box_info_static":
                        if (!_fetchingComboBoxInfo)
                        {
                            _fetchingComboBoxInfo = true;
                            Utils.RunTask(FetchComboBoxInfo());
                        }
                        return true;
                }
            }
            return false;
        }
    }
}