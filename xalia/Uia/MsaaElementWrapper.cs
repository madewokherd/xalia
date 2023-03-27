using Accessibility;
using System;

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
    }
}
