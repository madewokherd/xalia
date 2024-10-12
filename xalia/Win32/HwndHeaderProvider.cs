using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndHeaderProvider : UiDomProviderBase, IWin32Styles, IWin32Container
    {
        public HwndHeaderProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }

        public IntPtr Hwnd => HwndProvider.Hwnd;
        public Win32Connection Connection => HwndProvider.Connection;
        public UiDomElement Element => HwndProvider.Element;
        public int Tid => HwndProvider.Tid;
        public UiDomRoot Root => Element.Root;

        public CommandThread CommandThread => HwndProvider.CommandThread;

        public int ItemCount;
        public bool ItemCountKnown;
        private bool fetching_item_count;
        private bool watching_item_count;

        private int uniqueid;

        private bool watching_children;

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "item_count", "win32_item_count" },
        };

        static string[] tracked_properties = { "recurse_method" };

        static UiDomEnum role = new UiDomEnum(new string[] { "header" });

        static string[] style_names =
        {
            null,
            "buttons",
            "hottrack",
            "hds_hidden", // this doesn't actually hide the window so don't use it to indicate that
            null,
            null,
            "dragdrop",
            "fulldrag",
            "filterbar",
            "flat",
            "checkboxes",
            "nosizing",
            "overflow"
        };

        static Dictionary<string,int> style_flags;

        static HwndHeaderProvider()
        {
            style_flags = new Dictionary<string, int>();
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                style_flags[style_names[i]] = 0x1 << i;
            }
        }

        public override void DumpProperties(UiDomElement element)
        {
            if (ItemCountKnown)
                Utils.DebugWriteLine($"  win32_item_count: {ItemCount}");
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_header":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "win32_item_count":
                    depends_on.Add((element, new IdentifierExpression(identifier)));
                    if (ItemCountKnown)
                        return new UiDomInt(ItemCount);
                    break;
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "header":
                    return UiDomBoolean.True;
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                    {
                        return new UiDomString("win32_header");
                    }
                    break;
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
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public void GetStyleNames(int style, List<string> names)
        {
            for (int i=0; i<style_names.Length; i++)
            {
                if (style_names[i] is null)
                    continue;
                if ((HwndProvider.Style & (0x1 << i)) != 0)
                {
                    names.Add(style_names[i]);
                }
            }
        }

        private async Task FetchItemCount()
        {
            IntPtr result;
            try
            {
                result = await SendMessageAsync(Hwnd, HDM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Win32Exception ex)
            {
                if (!HwndProvider.IsExpectedException(ex))
                    throw;
                return;
            }

            ItemCount = Utils.TruncatePtr(result);
            ItemCountKnown = true;
            fetching_item_count = false;
            Element.PropertyChanged("win32_item_count", ItemCount);

            RefreshChildren();
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
                switch (string_value)
                {
                    case "win32_header":
                        if (watching_children)
                            break;
                        watching_children = true;
                        WatchChildren();
                        break;
                    default:
                        watching_children = false;
                        UnwatchChildren();
                        break;
                }
            }
            base.TrackedPropertyChanged(element, name, new_value);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_item_count":
                        watching_item_count = true;
                        if (!ItemCountKnown && !fetching_item_count)
                        {
                            fetching_item_count = true;
                            Utils.RunTask(FetchItemCount());
                        }
                        return true;
                }
            }
            return base.WatchProperty(element, expression);
        }

        public override bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_item_count":
                        watching_item_count = false;
                        return true;
                }
            }
            return base.UnwatchProperty(element, expression);
        }

        private void WatchChildren()
        {
            Element.SetRecurseMethodProvider(this);
            if (ItemCountKnown)
                RefreshChildren();
            else if (!fetching_item_count)
            {
                fetching_item_count = true;
                Utils.RunTask(FetchItemCount());
            }
        }

        private void UnwatchChildren()
        {
            Element.UnsetRecurseMethodProvider(this);
        }

        private void RefreshChildren()
        {
            List<string> keys = new List<string>(ItemCount);
            for (int i = 1; i <= ItemCount; i++)
            {
                keys.Add(GetChildKey(i));
            }
            Element.SyncRecurseMethodChildren(keys, (string key) => key,
                CreateChildItem);
        }

        private UiDomElement CreateChildItem(string key)
        {
            var element = new UiDomElement(key, Root);
            element.AddProvider(new HwndHeaderItemProvider(this, element), 0);
            return element;
        }

        private string GetUniqueKey()
        {
            return $"header-{Hwnd.ToInt64().ToString("x", CultureInfo.InvariantCulture)}-{(++uniqueid).ToString(CultureInfo.InvariantCulture)}";
        }

        private string GetChildKey(int ChildId)
        {
            var child = GetMsaaChild(ChildId);
            if (child is null)
                return GetUniqueKey();
            return child.DebugId;
        }

        public void MsaaChildCreated(int ChildId)
        {
            if (ItemCountKnown)
            {
                if (ChildId < 1 || ChildId > ItemCount + 1)
                {
                    Utils.DebugWriteLine($"got EVENT_OBJECT_CREATE for {Element} with child id {ChildId} with ItemCount={ItemCount}");
                    return;
                }

                ItemCount++;
                Element.PropertyChanged("win32_item_count", ItemCount);

                if (!watching_children)
                    return;

                var child = CreateChildItem(GetUniqueKey());
                Element.AddChild(ChildId - 1, child, true);
            }
        }

        public void MsaaChildDestroyed(int ChildId)
        {
            if (ItemCountKnown)
            {
                if (ChildId < 1 || ChildId >= ItemCount + 1)
                {
                    Utils.DebugWriteLine($"got EVENT_OBJECT_DESTROY for {Element} with child id {ChildId} with ItemCount={ItemCount}");
                    return;
                }

                ItemCount--;
                Element.PropertyChanged("win32_item_count", ItemCount);

                if (!watching_children)
                    return;

                Element.RemoveChild(ChildId - 1, true);
            }
        }
        private async Task DoChildrenReordered()
        {
            fetching_item_count = true;
            await FetchItemCount();
        }

        public void MsaaChildrenReordered()
        {
            ItemCountKnown = false;
            if (watching_children || watching_item_count)
                Utils.RunTask(DoChildrenReordered());
        }

        public UiDomElement GetMsaaChild(int ChildId)
        {
            if (ChildId >= 1)
            {
                int index = ChildId - 1;
                if (index < Element.RecurseMethodChildCount)
                    return Element.Children[index];
            }
            return null;
        }
    }
}
