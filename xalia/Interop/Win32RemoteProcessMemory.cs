#if WINDOWS
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static Xalia.Interop.Win32;

namespace Xalia.Interop
{
    internal class Win32RemoteProcessMemory : IDisposable
    {
        public Win32RemoteProcessMemory(int pid)
        {
            Pid = pid;
            processHandle = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE,
                false, pid);
            if (processHandle.IsInvalid)
                throw new Win32Exception();
            refCount = 1;
            cache[pid] = this;
        }

        public void Ref()
        {
            refCount++;
        }

        public void Unref()
        {
            if (--refCount == 0)
            {
                Dispose();
            }
        }

        static Dictionary<int, Win32RemoteProcessMemory> cache = new Dictionary<int, Win32RemoteProcessMemory>();

        public static Win32RemoteProcessMemory FromPid(int pid)
        {
            if (cache.TryGetValue(pid, out var result) && result.refCount != 0)
            {
                result.Ref();
                return result;
            }
            return new Win32RemoteProcessMemory(pid);
        }

        public class MemoryAllocation : IDisposable
        {
            internal MemoryAllocation(Win32RemoteProcessMemory processMemory, ulong address, ulong size)
            {
                ProcessMemory = processMemory;
                Address = address;
                Size = size;
            }

            public ulong Size { get; }

            public ulong Address { get; }

            public Win32RemoteProcessMemory ProcessMemory { get;  }

            public void Write(byte[] data)
            {
                Write(data, 0);
            }

            public void Write(byte[] data, ulong offset)
            {
                ulong start = Address + offset;
                ulong size = (ulong)data.LongLength;
                ulong end = start + size;

                if (ProcessMemory.Disposed)
                    throw new ObjectDisposedException("Win32RemoteProcessMemory");

                if (disposedValue)
                    throw new ObjectDisposedException("MemoryAllocation");

                if (end < start || end > Address + Size)
                    throw new ArgumentOutOfRangeException("Write outside of MemoryHandle bounds");

                ProcessMemory.Write(data, start);
            }

            public void Write(ValueType val)
            {
                Write(val, 0);
            }

            public unsafe void Write(ValueType val, ulong offset)
            {
                ulong length = (ulong)Marshal.SizeOf(val);

                byte[] data = new byte[length];

                fixed (byte* ptr = data)
                {
                    Marshal.StructureToPtr(val, new IntPtr(ptr), false);
                }

                Write(data, offset);
            }

            public byte[] ReadBytes()
            {
                return ReadBytes(0, Size);
            }

            public byte[] ReadBytes(ulong offset)
            {
                return ReadBytes(offset, Size - offset);
            }

            public byte[] ReadBytes(ulong offset, ulong size)
            {
                ulong start = Address + offset;
                ulong end = start + size;

                if (ProcessMemory.Disposed)
                    throw new ObjectDisposedException("Win32RemoteProcessMemory");

                if (disposedValue)
                    throw new ObjectDisposedException("MemoryAllocation");

                if (end < start || end > Address + Size)
                    throw new ArgumentOutOfRangeException("Write outside of MemoryHandle bounds");

                return ProcessMemory.ReadBytes(Address + offset, size);
            }

            public bool Disposed => disposedValue;

            private bool disposedValue;

            public void Dispose()
            {
                if (!disposedValue)
                {
                    try
                    {
                        ProcessMemory.Unref(this);
                    }
                    finally
                    {
                        disposedValue = true;
                    }
                }
            }

            internal unsafe T Read<T>(ulong offset)
            {
                ulong size = (ulong)Marshal.SizeOf<T>();

                byte[] data = ReadBytes(offset, size);

                fixed (byte* ptr = data)
                {
                    return Marshal.PtrToStructure<T>(new IntPtr(ptr));
                }
            }

            internal T Read<T>()
            {
                return Read<T>(0);
            }
        }

        private bool disposedValue;
        private SafeProcessHandle processHandle;

        private struct MemoryPage
        {
            public ulong StartAddress;
            public ulong EndAddress;
            public ulong RefCount;
            public ulong FreeAddress;
        }

        private List<MemoryPage> memoryPages = new List<MemoryPage>();
        private int refCount;

        public MemoryAllocation Alloc(ulong size)
        {
            if (disposedValue)
                throw new ObjectDisposedException("Win32RemoteProcessMemory");

            if (size == 0)
            {
                return new MemoryAllocation(this, 0, 0);
            }

            int page_index;
            MemoryPage page = default;
            bool found = false;

            for (page_index = 0; page_index < memoryPages.Count; page_index++) {
                page = memoryPages[page_index];
                if (page.EndAddress - page.FreeAddress >= size)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                GetSystemInfo(out SYSTEM_INFO info);

                var vm_size = (size + (ulong)info.dwPageSize - 1) & ~((ulong)info.dwPageSize - 1);

                IntPtr ptr = VirtualAllocEx(processHandle, IntPtr.Zero, new IntPtr((long)vm_size),
                    MEM_COMMIT|MEM_RESERVE, PAGE_READWRITE);
                if (ptr == IntPtr.Zero)
                    throw new Win32Exception();

                page = new MemoryPage();
                page.StartAddress = (ulong)ptr;
                page.EndAddress = (ulong)ptr + vm_size;
                page.FreeAddress = page.StartAddress;
                page.RefCount = 0;

                page_index = memoryPages.Count;
                memoryPages.Add(page);
            }

            MemoryAllocation result = new MemoryAllocation(this, page.FreeAddress, size);
            page.FreeAddress += (size + 7) & ~(ulong)7;
            page.RefCount += 1;
            memoryPages[page_index] = page;
            return result;
        }

        private void Unref(MemoryAllocation allocation)
        {
            int page_index;
            MemoryPage page;

            if (allocation.Size == 0 || Disposed)
                return;

            for (page_index = 0; page_index < memoryPages.Count; page_index++) {
                page = memoryPages[page_index];
                if (page.StartAddress <= allocation.Address &&
                    page.EndAddress > allocation.Address)
                {
                    page.RefCount -= 1;
                    if (page.RefCount == 0)
                    {
                        memoryPages[page_index] = memoryPages[memoryPages.Count - 1];
                        memoryPages.RemoveAt(memoryPages.Count - 1);
                        bool result = VirtualFreeEx(processHandle, new IntPtr((long)page.StartAddress),
                            IntPtr.Zero, MEM_RELEASE);
                        if (!result)
                            throw new Win32Exception();
                    }
                    else
                    {
                        memoryPages[page_index] = page;
                    }
                    return;
                }
            }
        }

        public bool Disposed => disposedValue;

        public int Pid { get; }

        public void Write(byte[] data, ulong offset)
        {
            if (disposedValue)
                throw new ObjectDisposedException("Win32RemoteProcessMemory");

            ulong length = (ulong)data.LongLength;
            if (offset + length < offset)
                throw new OverflowException("Write would go past the last page of memory");
            bool result = WriteProcessMemory(processHandle, new IntPtr((long)offset), data,
                new IntPtr(data.LongLength), IntPtr.Zero);
            if (!result)
                throw new Win32Exception();
        }

        public MemoryAllocation WriteAlloc(byte[] data)
        {
            ulong length = (ulong)data.LongLength;

            MemoryAllocation result = Alloc(length);
            try
            {
                result.Write(data);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        public MemoryAllocation WriteAlloc(ValueType val)
        {
            ulong length = (ulong)Marshal.SizeOf(val);

            MemoryAllocation result = Alloc(length);
            try
            {
                result.Write(val);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        public byte[] ReadBytes(ulong offset, ulong length)
        {
            if (disposedValue)
                throw new ObjectDisposedException("Win32RemoteProcessMemory");

            if (offset + length < offset)
                throw new OverflowException("Read would go past the last page of memory");

            var buffer = new byte[length];
            bool result = ReadProcessMemory(processHandle, new IntPtr((long)offset), buffer,
                new IntPtr((long)length), IntPtr.Zero);
            if (!result)
                throw new Win32Exception();
            return buffer;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                foreach (var page in memoryPages)
                {
                    VirtualFreeEx(processHandle, new IntPtr((long)page.StartAddress),
                        new IntPtr((long)(page.EndAddress - page.StartAddress)), MEM_DECOMMIT | MEM_RELEASE);
                }
                memoryPages = null;
                processHandle.Dispose();
                disposedValue = true;
            }
        }

        ~Win32RemoteProcessMemory()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
#endif
