using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Uia
{
    internal class MsaaElement : UiDomElement
    {
        private static readonly Dictionary<string, string> property_aliases;

        static MsaaElement()
        {
            string[] aliases = {
                "hwnd", "msaa_hwnd",
                "pid", "msaa_pid",
                "child_id", "msaa_child_id",
                "application_name", "msaa_process_name",
                "process_name", "msaa_process_name",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i=0; i<aliases.Length; i+=2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
        }
        
        public MsaaElement(MsaaElementWrapper wrapper, UiaConnection root) : base(root)
        {
            ElementWrapper = wrapper;
        }

        public override string DebugId => ElementWrapper.UniqueId;

        public MsaaElementWrapper ElementWrapper { get; }

        private string _processName;

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (property_aliases.TryGetValue(id, out string aliased))
            {
                var value = base.EvaluateIdentifierCore(id, root, depends_on);
                if (!value.Equals(UiDomUndefined.Instance))
                    return value;
                id = aliased;
            }

            switch (id)
            {
                case "is_msaa_element":
                    return UiDomBoolean.True;
                case "msaa_hwnd":
                    return new UiDomInt((int)ElementWrapper.Hwnd);
                case "msaa_child_id":
                    return new UiDomInt(ElementWrapper.ChildId);
                case "msaa_pid":
                    return new UiDomInt(ElementWrapper.Pid);
                case "msaa_process_name":
                    try
                    {
                        if (_processName is null)
                        {
                            using (var process = Process.GetProcessById(ElementWrapper.Pid))
                                _processName = process.ProcessName;
                        }
                    }
                    catch (ArgumentException)
                    {
                        return UiDomUndefined.Instance;
                    }
                    return new UiDomString(_processName);
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override void DumpProperties()
        {
            if (ElementWrapper.Hwnd != IntPtr.Zero)
                Utils.DebugWriteLine($"  msaa_hwnd: {ElementWrapper.Hwnd}");
            if (ElementWrapper.ChildId != 0)
                Utils.DebugWriteLine($"  msaa_child_id: {ElementWrapper.ChildId}");
            Utils.DebugWriteLine($"  msaa_pid: {ElementWrapper.Pid}");
            if (!(_processName is null))
                Utils.DebugWriteLine($"  msaa_process_name: {_processName}");
            base.DumpProperties();
        }
    }
}
