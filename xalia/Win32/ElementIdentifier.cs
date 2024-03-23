using System;
using static Xalia.Interop.Win32;

namespace Xalia.Win32
{
    /* Represents a reference to a child element, generally so that a UIDomElement can be constructed. */
    internal struct ElementIdentifier
    {
        /* Win32: */
        public IntPtr root_hwnd;
        public bool is_root_hwnd;
        /* MSAA: */
        public IAccessible acc;
        public IAccessible2 acc2;
        public int acc2_uniqueId;
        public int child_id;
        /* UIA: */
        public IRawElementProviderSimple prov;
        public int[] runtime_id;
    }
}
