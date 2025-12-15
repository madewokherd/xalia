using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    class HwndTreeViewItemProvider : UiDomProviderBase
    {
        public HwndTreeViewItemProvider(HwndTreeViewProvider view, UiDomElement element, IntPtr hItem)
        {
            if (view == null)
                view = this as HwndTreeViewProvider;
            View = view;
            Element = element;
            HItem = hItem;
        }

        public HwndTreeViewProvider View { get; }
        public IntPtr HItem { get; }
        public UiDomElement Element { get; }
        public HwndProvider HwndProvider => View.HwndProvider;
        public IntPtr Hwnd => View.Hwnd;
        public UiDomRoot Root => View.Root;

        static UiDomEnum role = new UiDomEnum(new string[] { "tree_item", "treeitem", "outlineitem" });

        bool watching_children;
        bool watching_children_visible;

        public override void DumpProperties(UiDomElement element)
        {
            if (!(this is HwndTreeViewProvider))
                HwndProvider.ChildDumpProperties();
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_treeview_item":
                case "is_hwnd_tree_view_item":
                case "is_hwnd_subelement":
                    return UiDomBoolean.FromBool(!(this is HwndTreeViewProvider));
            }
            if (!(this is HwndTreeViewProvider))
                return HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "tree_item":
                case "treeitem":
                case "outlineitem":
                case "enabled":
                case "visible":
                    if (!(this is HwndTreeViewProvider))
                        return UiDomBoolean.True;
                    break;
                case "role":
                case "control_type":
                    return role;
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                    { 
                        if (element.EvaluateIdentifier("recurse_full", element.Root, depends_on).ToBool())
                            return new UiDomString("win32_tree");
                        else
                            return new UiDomString("win32_tree_visible");
                    }
                    break;
            }
            if (!(this is HwndTreeViewProvider))
                return HwndProvider.ChildEvaluateIdentifier(identifier, depends_on);
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        static string[] tracked_properties =
        {
            "recurse_method"
        };

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
                    case "win32_tree":
                        if (watching_children && !watching_children_visible)
                            break;
                        watching_children = true;
                        watching_children_visible = false;
                        WatchChildren();
                        break;
                    case "win32_tree_visible":
                        if (watching_children && watching_children_visible)
                            break;
                        watching_children = true;
                        watching_children_visible = true;
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

        private void WatchChildren()
        {
            Element.SetRecurseMethodProvider(this);
            RefreshChildren();
        }

        private void UnwatchChildren()
        {
            Element.UnsetRecurseMethodProvider(this);
        }

        private void RefreshChildren()
        {
            Utils.RunTask(RefreshChildrenAsync());
        }

        private async Task RefreshChildrenAsync()
        {
            if (!watching_children)
                return;

            var child = await SendMessageAsync(Hwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_CHILD), HItem);
            var children = new List<IntPtr>();

            if (!watching_children)
                return;

            while (child != IntPtr.Zero)
            {
                children.Add(child);
                child = await SendMessageAsync(Hwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_NEXT), child);

                if (!watching_children)
                    return;
            }

            Element.SyncRecurseMethodChildren(children, GetChildId, CreateChild);
        }

        private UiDomElement CreateChild(IntPtr hitem)
        {
            var result = new UiDomElement(GetChildId(hitem), Root);

            result.AddProvider(new HwndTreeViewItemProvider(View, result, hitem));

            return result;
        }

        private string GetChildId(IntPtr hitem)
        {
            return $"treeitem-{hitem:X}";
        }
    }
}