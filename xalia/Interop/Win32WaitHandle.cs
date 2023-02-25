using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xalia.Interop
{
    internal class Win32WaitHandle : WaitHandle
    {
        public Win32WaitHandle(SafeWaitHandle handle, bool ownHandle)
        {
            SafeWaitHandle = handle;
            OwnHandle = ownHandle;
        }

        public bool OwnHandle { get; }

        protected override void Dispose(bool explicitDisposing)
        {
            if (OwnHandle)
            {
                base.Dispose(explicitDisposing);
            }
        }
    }
}
