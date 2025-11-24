using System;
using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

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
        public Win32Connection Connection => HwndProvider.Connection;
        public UiDomRoot Root => Element.Root;
        public bool Horizontal => (HwndProvider.Style & UDS_HORZ) == UDS_HORZ;

        private bool watching_children;

        static UiDomEnum role = new UiDomEnum(new string[] { "spinner", "spin_button", "spinbutton" });

        static readonly string[] tracked_properties = new string[] { "recurse_method" };

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
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                    {
                        return new UiDomString("win32_updown_button");
                    }
                    break;
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

        public override string[] GetTrackedProperties()
        {
            return tracked_properties;
        }

        public override void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            if (name == "recurse_method")
            {
                string string_value = new_value is UiDomString id ? id.Value : string.Empty;
                if (string_value == "win32_updown_button")
                {
                    if (!watching_children)
                    {
                        watching_children = true;
                        WatchChildren();
                    }
                }
                else
                {
                    if (watching_children)
                    {
                        watching_children = false;
                        UnwatchChildren();
                    }
                }
            }
            base.TrackedPropertyChanged(element, name, new_value);
        }

        private void UnwatchChildren()
        {
            Element.UnsetRecurseMethodProvider(this);
        }

        private void WatchChildren()
        {
            Element.SetRecurseMethodProvider(this);

            int[] child_ids = new int[] { 1, 2 };

            Element.SyncRecurseMethodChildren(child_ids, ChildIdToId, ChildIdToElement);
        }

        private UiDomElement ChildIdToElement(int child_id)
        {
            var element = new UiDomElement(Connection.GetElementName(Hwnd, OBJID_CLIENT, child_id), Root);
            element.AddProvider(new HwndUpDownButtonProvider(this, child_id));
            return element;
        }

        private string ChildIdToId(int child_id)
        {
            return Connection.GetElementName(Hwnd, OBJID_CLIENT, child_id);
        }
    }
}