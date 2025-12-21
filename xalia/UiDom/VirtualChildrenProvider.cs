using System.Collections.Generic;
using Xalia.Gudl;
using Xalia.Util;

namespace Xalia.UiDom
{
    internal class VirtualChildrenProvider : UiDomProviderBase
    {
        public VirtualChildrenProvider()
        {
        }

        static string[] tracked_properties = { "recurse_method", "virtual_child_count" };

        public override string[] GetTrackedProperties()
        {
            return tracked_properties;
        }

        public override void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            HashSet<(UiDomElement, GudlExpression)> depends_on = new HashSet<(UiDomElement, GudlExpression)>();
            if (element.EvaluateIdentifier("recurse_method", element.Root, depends_on) is UiDomString s && s.Value == "virtual")
            {
                element.SetRecurseMethodProvider(this);
                element.EvaluateIdentifier("virtual_child_count", element.Root, depends_on).TryToInt(out int child_count);
                element.SyncRecurseMethodChildren(new RangeList(0, child_count), (int i) => ($"virtual-{i}-{element.DebugId}"),
                    (int i) => CreateElement(element, i));
            }
            else
            {
                element.UnsetRecurseMethodProvider(this);
            }
        }

        private UiDomElement CreateElement(UiDomElement parent, int index)
        {
            UiDomElement result = new UiDomElement($"virtual-{index}-{parent.DebugId}", parent.Root);
            result.AddProvider(new VirtualChildProvider(index));
            return result;
        }
    }
}
