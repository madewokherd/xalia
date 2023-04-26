using Accessibility;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    internal class HwndProvider : IUiDomProvider
    {
        public HwndProvider(IntPtr hwnd, UiDomElement element, Win32Connection connection)
        {
            Hwnd = hwnd;
            Element = element;
            Connection = connection;
            ClassName = RealGetWindowClass(hwnd);
            Tid = GetWindowThreadProcessId(hwnd, out var pid);
            Pid = pid;

            Utils.RunTask(DiscoverProviders());
        }

        public IntPtr Hwnd { get; }
        public UiDomElement Element { get; private set; }
        public Win32Connection Connection { get; }
        public string ClassName { get; }
        public int Pid { get; }
        public int Tid { get; }

        private bool _watchingChildren;
        private int _childCount;

        private static string[] tracked_properties = new string[] { "recurse_method" };

        private static Dictionary<string, string> property_aliases = new Dictionary<string, string>()
        {
            { "hwnd", "win32_hwnd" },
            { "class_name", "win32_class_name" },
            { "pid", "win32_pid" },
            { "tid", "win32_tid" },
        };

        private async Task DiscoverProviders()
        {
            // TODO: Check if there's a UIA provider

            var lr = await SendMessageAsync(Hwnd, WM_GETOBJECT, IntPtr.Zero, (IntPtr)OBJID_CLIENT);
            if ((int)lr > 0)
            {
                try
                {
                    IAccessible acc = await Connection.CommandThread.OnBackgroundThread(() =>
                    {
                        int hr = ObjectFromLresult(lr, IID_IAccessible, IntPtr.Zero, out var obj);
                        Marshal.ThrowExceptionForHR(hr);
                        return obj;
                    }, Tid + 1);
                    Element.AddProvider(new AccessibleProvider(this, Element, acc, 0), 0);
                    return;
                }
                catch (Exception e)
                {
                    if (!AccessibleProvider.IsExpectedException(e))
                        throw;
                }
            }
            
            switch (ClassName)
            {
                case "#32770":
                    Element.AddProvider(new HwndDialogProvider(this), 0);
                    return;
            }

            // TODO: Check for OBJID_QUERYCLASSNAMEIDX?
        }

        public void DumpProperties(UiDomElement element)
        {
            Utils.DebugWriteLine($"  win32_hwnd: {Hwnd}");
            Utils.DebugWriteLine($"  win32_class_name: \"{ClassName}\"");
            Utils.DebugWriteLine($"  win32_pid: {Pid}");
            Utils.DebugWriteLine($"  win32_tid: {Tid}");
        }

        public UiDomValue EvaluateIdentifier(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "is_hwnd_element":
                    return UiDomBoolean.True;
                case "win32_hwnd":
                    return new UiDomInt((int)Hwnd);
                case "win32_class_name":
                    return new UiDomString(ClassName);
                case "win32_pid":
                    return new UiDomInt(Pid);
                case "win32_tid":
                    return new UiDomInt(Tid);
            }
            return UiDomUndefined.Instance;
        }

        public UiDomValue EvaluateIdentifierLate(UiDomElement element, string identifier, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (identifier)
            {
                case "recurse_method":
                    if (element.EvaluateIdentifier("recurse", element.Root, depends_on).ToBool())
                        return new UiDomString("win32");
                    break;
            }
            if (property_aliases.TryGetValue(identifier, out var aliased))
                return element.EvaluateIdentifier(aliased, element.Root, depends_on);
            return UiDomUndefined.Instance;
        }

        public Task<(bool, int, int)> GetClickablePointAsync(UiDomElement element)
        {
            return Task.FromResult((false, 0, 0));
        }

        public string[] GetTrackedProperties()
        {
            return tracked_properties;
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
            Element = null;
        }

        private void WatchChildren()
        {
            if (_watchingChildren)
                return;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"WatchChildren for {Element} (win32)");
            _watchingChildren = true;
            Utils.RunIdle(PollChildren); // need to wait for any other providers to remove their children
        }

        private void PollChildren()
        {
            if (!_watchingChildren)
                return;

            var child_hwnds = EnumImmediateChildWindows(Hwnd);

            // First remove any existing children that are missing or out of order
            int i = 0;
            foreach (var new_child in child_hwnds)
            {
                var child_name = $"hwnd-{new_child}";
                if (!Element.Children.Exists((UiDomElement element) => element.DebugId == child_name))
                    continue;
                while (Element.Children[i].DebugId != child_name)
                {
                    Element.RemoveChild(i);
                    _childCount--;
                }
                i++;
            }

            // Remove any remaining missing children
            while (i < _childCount)
            {
                Element.RemoveChild(i);
                _childCount--;
            }

            // Add any new children
            i = 0;
            foreach (var new_child in child_hwnds)
            {
                var child_name = $"hwnd-{new_child}";
                if (_childCount <= i || Element.Children[i].DebugId != child_name)
                {
                    if (!(Connection.LookupElement(child_name) is null))
                    {
                        // Child element is a duplicate of another element somewhere in the tree.
                        continue;
                    }
                    Element.AddChild(i, Connection.CreateElementFromHwnd(new_child));
                    _childCount++;
                }
                i += 1;
            }
        }

        private void UnwatchChildren()
        {
            if (!_watchingChildren)
                return;
            if (Element.MatchesDebugCondition())
                Utils.DebugWriteLine($"UnwatchChildren for {Element} (win32)");
            _watchingChildren = false;
            for (int i = _childCount - 1; i >= 0; i--)
                Element.RemoveChild(i);
            _childCount = 0;
        }

        public void TrackedPropertyChanged(UiDomElement element, string name, UiDomValue new_value)
        {
            switch (name)
            {
                case "recurse_method":
                    if (new_value is UiDomString st && st.Value == "win32")
                        WatchChildren();
                    else
                        UnwatchChildren();
                    break;
            }
        }

        public bool UnwatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }

        public bool WatchProperty(UiDomElement element, GudlExpression expression)
        {
            return false;
        }
    }
}
