using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

namespace Xalia.Win32
{
    internal abstract class HwndItemListProvider : UiDomProviderBase
    {
        public HwndItemListProvider(HwndProvider hwndProvider)
        {
            HwndProvider = hwndProvider;
        }

        public HwndProvider HwndProvider { get; }
        public IntPtr Hwnd => HwndProvider.Hwnd;
        public UiDomElement Element => HwndProvider.Element;
        public Win32Connection Connection => HwndProvider.Connection;
        public int Pid => HwndProvider.Pid;

        public bool ItemCountKnown;
        public int ItemCount;
        private bool fetching_item_count;

        protected abstract Task<int> FetchItemCount();

        public override void DumpProperties(UiDomElement element)
        {
            if (ItemCountKnown)
                Utils.DebugWriteLine($"  win32_item_count: {ItemCount}");
            base.DumpProperties(element);
        }

        public override UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (identifier == "win32_item_count")
            {
                depends_on.Add((element, new IdentifierExpression(identifier)));
                if (ItemCountKnown)
                    return new UiDomInt(ItemCount);
            }
            return base.EvaluateIdentifier(element, identifier, depends_on);
        }

        public override UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (identifier == "item_count")
                return EvaluateIdentifier(element, "win32_item_count", depends_on);
            return base.EvaluateIdentifierLate(element, identifier, depends_on);
        }

        public override bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            if (expression is IdentifierExpression id && id.Name == "win32_item_count" &&
                !ItemCountKnown && !fetching_item_count)
            {
                fetching_item_count = true;
                Utils.RunTask(DoFetchItemCount());
                return true;
            }
            return base.WatchProperty(element, expression);
        }

        public async Task DoFetchItemCount()
        {
            int result = await FetchItemCount();
            if (ItemCountKnown)
                // Got an MSAA event while waiting for this, assume event is more up to date
                return;

            SetCurrentItemCount(result);
        }

        protected virtual void ItemCountChanged(int newCount) { }

        private void SetCurrentItemCount(int result)
        {
            ItemCount = result;
            ItemCountKnown = true;
            Element.PropertyChanged("win32_item_count", result);
            ItemCountChanged(result);
        }

        internal void MsaaChildAdded(int idChild)
        {
            SetCurrentItemCount(idChild);
        }

        internal void MsaaChildDestroyed(int idChild)
        {
            SetCurrentItemCount(idChild - 1);
        }
    }
}
