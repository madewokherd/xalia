using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class Win32Connection : IUiDomProvider
    {
        public Win32Connection(UiDomRoot root)
        {
            Root = root;
            root.AddGlobalProvider(this);
            Utils.RunIdle(UpdateToplevels);
        }

        private HashSet<IntPtr> toplevel_hwnds = new HashSet<IntPtr>();

        private Dictionary<string, UiDomElement> elements_by_id = new Dictionary<string, UiDomElement>();

        public UiDomRoot Root { get; }

        public CommandThread CommandThread { get; } = new CommandThread();

        private void UpdateToplevels()
        {
            HashSet<IntPtr> toplevels_to_remove = new HashSet<IntPtr>(toplevel_hwnds);

            foreach (var hwnd in EnumWindows())
            {
                if (toplevels_to_remove.Contains(hwnd))
                {
                    // already known
                    toplevels_to_remove.Remove(hwnd);
                    continue;
                }

                var element = CreateElementFromHwnd(hwnd);
                Root.AddChild(Root.Children.Count, element);
                toplevel_hwnds.Add(hwnd);
            }

            foreach (var hwnd in toplevels_to_remove)
            {
                toplevel_hwnds.Remove(hwnd);
                var element = elements_by_id[$"hwnd-{hwnd}"];
                Root.RemoveChild(Root.Children.IndexOf(element));
            }
        }

        public UiDomElement LookupElement(string element_name)
        {
            if (elements_by_id.TryGetValue(element_name, out var element))
                return element;
            return null;
        }

        internal UiDomElement CreateElementFromHwnd(IntPtr hwnd)
        {
            string element_name = $"hwnd-{hwnd}";

            var element = new UiDomElement(element_name, Root);

            element.AddProvider(new HwndProvider(hwnd, element, this));

            elements_by_id.Add(element_name, element);

            return element;
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
            elements_by_id.Remove(element.DebugId);
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return UiDomUndefined.Instance;
        }

        public UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return UiDomUndefined.Instance;
        }

        public void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
        }

        public void DumpProperties(UiDomElement element)
        {
        }

        public Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public string[] GetTrackedProperties()
        {
            return null;
        }
    }
}
