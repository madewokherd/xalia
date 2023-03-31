using Accessibility;
using System;
using System.Threading;
using static Xalia.Interop.Win32;

namespace Xalia.Uia
{
    public struct MsaaElementWrapper
    {
        private MsaaElementWrapper(IAccessible acc, int childId, string uniqueId, int pid, IntPtr hwnd)
        {
            // object should be created via static methods
            Accessible = acc;
            ChildId = childId;
            UniqueId = uniqueId;
            Pid = pid;
            Hwnd = hwnd;
        }

        public IAccessible Accessible { get; }
        public int ChildId { get; }
        public string UniqueId { get; }
        public int Pid { get; }
        public IntPtr Hwnd { get; }

        public bool IsValid => Accessible != null;

        private static int MonotonicId;

        private static bool UniqueIdFromAccessibleBackground(IAccessible acc, IntPtr hwnd, int child_id, out string id)
        {
            id = null;
            if (hwnd == IntPtr.Zero)
                return false;
            if (child_id != 0)
                id = $"msaa-hwnd-{hwnd}-{child_id}";
            else
                id = $"msaa-hwnd-{hwnd}";
            return true;
        }

        public static MsaaElementWrapper FromUiaElementBackground(UiaElementWrapper wrapper)
        {
            var acc = wrapper.Connection.GetIAccessibleBackground(wrapper.AutomationElement, out int child_id);
            if (!UniqueIdFromAccessibleBackground(acc, wrapper.Hwnd, child_id, out var unique_id))
            {
                throw new NotImplementedException("cannot generate unique id for MsaaElementWrapper");
            }
            return new MsaaElementWrapper(acc, child_id, unique_id, wrapper.Pid, wrapper.Hwnd);
        }

        private string GenerateUniqueId()
        {
            var id = Interlocked.Increment(ref MonotonicId);
            return $"msaa-{id}";
        }

        public bool FromVariantBackground(object child, bool assumeUnique, out MsaaElementWrapper child_wrapper)
        {
            child_wrapper = default;
            if (child is int child_id)
            {
                if (!UniqueIdFromAccessibleBackground(Accessible, Hwnd, child_id, out var unique_id))
                    unique_id = GenerateUniqueId();
                child_wrapper = new MsaaElementWrapper(Accessible, child_id, unique_id, Pid, Hwnd);
                return true;
            }
            else if (child is IAccessible acc)
            {
                if (!UniqueIdFromAccessibleBackground(acc, IntPtr.Zero, CHILDID_SELF, out var unique_id))
                    unique_id = GenerateUniqueId();
                child_wrapper = new MsaaElementWrapper(acc, CHILDID_SELF, unique_id, Pid, IntPtr.Zero);
                return true;
            }
            else if (child is null)
            {
                Utils.DebugWriteLine($"WARNING: accChild on {UniqueId} returned NULL");
                return false;
            }
            else
            {
                Utils.DebugWriteLine($"WARNING: accChild on {UniqueId} returned {child.GetType()}");
                return false;
            }
        }
    }
}
