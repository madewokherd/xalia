using FlaUI.Core.AutomationElements;
using System;
using System.Runtime.InteropServices;

namespace Xalia.Uia
{
    public struct UiaElementWrapper
    {
        // We need to store more information than the AutomationElement itself to use it.

        // The constructor should generally only be used on background threads because it can block.

        internal UiaElementWrapper(AutomationElement ae, UiaConnection connection)
        {
            AutomationElement = ae;
            Connection = connection;
            UniqueId = connection.BlockingGetElementId(ae, out var hwnd);
            Hwnd = hwnd;
            try
            {
                if (ae.FrameworkAutomationElement.TryGetPropertyValue(
                    Connection.Automation.PropertyLibrary.Element.ProcessId, out var pid) &&
                    pid is int i)
                {
                    Pid = i;
                }
                else
                {
                    Pid = 0;
                }
            }
            catch (COMException)
            {
                Pid = 0;
            }
        }

        public AutomationElement AutomationElement { get; }

        public UiaConnection Connection { get; }

        public string UniqueId { get; }

        public int Pid { get; }

        public IntPtr Hwnd { get; }

        public bool IsValid
        {
            get
            {
                return !(AutomationElement is null);
            }
        }

        public static UiaElementWrapper InvalidElement
        {
            get
            {
                return default(UiaElementWrapper);
            }
        }

        public bool Equals(UiaElementWrapper other)
        {
            return UniqueId == other.UniqueId && Connection == other.Connection;
        }

        public UiaElement LookupElement()
        {
            return Connection.LookupAutomationElement(this);
        }

        // In the future, we may want to store the processid here so we can ensure only 1
        // background thread works on a single process's request at a time.
    }
}
