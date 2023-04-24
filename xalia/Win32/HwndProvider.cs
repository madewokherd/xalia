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
            
            // TODO: Check for standard class names and OBJID_QUERYCLASSNAMEIDX?
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
            return null;
        }

        public void NotifyElementRemoved(UiDomElement element)
        {
            Element = null;
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
    }
}
