#if WINDOWS
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static SDL2.SDL;

namespace Xalia.Interop
{
    internal static class Win32
    {
        const string NT_LIB = "ntdll";
        const string KERNEL_LIB = "kernel32";
        const string GDI_LIB = "gdi32";
        const string USER_LIB = "user32";
        const string SHCORE_LIB = "shcore";
        const string OLEACC_LIB = "oleacc";
        const string UIA_LIB = "uiautomationcore";

        // Wine extension
        [DllImport(NT_LIB, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int __wine_dbg_output(byte[] str);

        public enum PROCESSINFOCLASS
        {
            ProcessBasicInformation = 0,
            
            // Wine extension:
            ProcessWineMakeProcessSystem = 1000,
        }

        [DllImport(NT_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int NtSetInformationProcess(IntPtr process, PROCESSINFOCLASS infoclass,
            out SafeWaitHandle handle, int size);

        public static IntPtr GetCurrentProcess()
        {
            return new IntPtr(-1);
        }

        [DllImport(KERNEL_LIB, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetCurrentThreadId();

        public const int PROCESS_VM_OPERATION = 0x8;
        public const int PROCESS_VM_READ = 0x10;
        public const int PROCESS_VM_WRITE = 0x20;
        public const int PROCESS_QUERY_INFORMATION = 0x400;
        public const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport(KERNEL_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern SafeProcessHandle OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport(KERNEL_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool IsWow64Process(SafeProcessHandle hProcess, out bool Wow64Process);

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_INFO
        {
            public short wProcessorArchitecture;
            public short wReserved;
            public int dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public int dwNumberOfProcessors;
            public int dwProcessorType;
            public int dwAllocationGranularity;
            public short wProcessorLevel;
            public short wProcessorRevision;
        }

        [DllImport(KERNEL_LIB, CallingConvention = CallingConvention.StdCall)]
        public static extern void GetSystemInfo(out SYSTEM_INFO info);

        public const int MEM_COMMIT = 0x1000;
        public const int MEM_RESERVE = 0x2000;

        public const int PAGE_NOACCESS = 0x1;
        public const int PAGE_READONLY = 0x2;
        public const int PAGE_READWRITE = 0x4;
        public const int PAGE_WRITECOPY = 0x8;
        public const int PAGE_EXECUTE = 0x10;
        public const int PAGE_EXECUTE_READ = 0x20;
        public const int PAGE_EXECUTE_READWRITE = 0x40;
        public const int PAGE_EXECUTE_WRITECOPY = 0x80;
        public const int PAGE_GUARD = 0x100;
        public const int PAGE_NOCACHE = 0x200;
        public const int PAGE_WRITECOMBINE = 0x400;

        [DllImport(KERNEL_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(SafeProcessHandle hProcess, IntPtr lpAddress, IntPtr dwSize,
            int flAllocationType, int flProtest);

        public const int MEM_DECOMMIT = 0x4000;
        public const int MEM_RELEASE = 0x8000;

        [DllImport(KERNEL_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool VirtualFreeEx(SafeProcessHandle hProcess, IntPtr lpAddress, IntPtr dwSize,
            int dwFreeType);

        [DllImport(KERNEL_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WriteProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress,
            [In] byte[] lpBuffer, IntPtr nSize, IntPtr lpNumberOfBytesWritten);

        [DllImport(KERNEL_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer, IntPtr nSize, IntPtr lpNumberOfBytesRead);

        [DllImport(GDI_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport(GDI_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport(GDI_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        public const int BI_RGB = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        public const int DIB_RGB_COLORS = 0;

        [DllImport(GDI_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, int usage,
            out IntPtr ppvBits, IntPtr hSection, int offset);

        [DllImport(GDI_LIB, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool DeleteObject(IntPtr ho);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr WNDPROC(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEXW
        {
            public int cbSize;
            public int style;
            public WNDPROC lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public extern static IntPtr RegisterClassExW([In]ref WNDCLASSEXW wndclassex);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
        public extern static IntPtr CreateWindowExW(int dwExStyle, IntPtr lpClassName, string lpWindowName,
            int dwStyle, int x, int y, int width, int height,
            IntPtr hwndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        public const int SW_HIDE = 0;
        public const int SW_SHOWNOACTIVATE = 4;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public extern static IntPtr DefWindowProcW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public extern static IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public extern static IntPtr GetWindowLongW(IntPtr hwnd, int index);

        public static IntPtr GetWindowLong(IntPtr hwnd, int index)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtrW(hwnd, index);
            else
                return GetWindowLongW(hwnd, index);
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        extern static IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr new_long);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        extern static IntPtr SetWindowLongW(IntPtr hwnd, int index, IntPtr new_long);

        public static IntPtr SetWindowLong(IntPtr hwnd, int index, IntPtr new_long)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtrW(hwnd, index, new_long);
            else
                return SetWindowLongW(hwnd, index, new_long);
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public extern static bool SetWindowPos(IntPtr hwnd, IntPtr insert_after, int x, int y, int width, int height, uint flags);

        public const int GWLP_ID = -12;
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const int WS_MAXIMIZEBOX = 0x00010000;
        public const int WS_TABSTOP = 0x00010000;
        public const int WS_MINIMIZEBOX = 0x00020000;
        public const int WS_GROUP = 0x00020000;
        public const int WS_SYSMENU = 0x00080000;
        public const int WS_HSCROLL = 0x00100000;
        public const int WS_VSCROLL = 0x00200000;
        public const int WS_DLGFRAME = 0x00400000;
        public const int WS_BORDER = 0x00800000;
        public const int WS_MAXIMIZE = 0x01000000;
        public const int WS_CLIPSIBLINGS = 0x04000000;
        public const int WS_DISABLED = 0x08000000;
        public const int WS_VISIBLE = 0x10000000;
        public const int WS_MINIMIZE = 0x20000000;
        public const int WS_CHILD = 0x40000000;
        public const int WS_POPUP = -0x80000000;

        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_CONTROLPARENT = 0x00010000;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public static readonly IntPtr HWND_TOPMOST = (IntPtr)(-1);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_HIDEWINDOW = 0x0080;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr SetActiveWindow(IntPtr hwnd);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr SetFocus(IntPtr hwnd);

        public const int MONITOR_DEFAULTTONULL = 0;
        public const int MONITOR_DEFAULTTOPRIMARY = 1;
        public const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        public const int MDT_EFFECTIVE_DPI = 0;
        public const int MDT_ANGULAR_DPI = 1;
        public const int MDT_RAW_DPI = 2;
        public const int MDT_DEFAULT = MDT_EFFECTIVE_DPI;

        [DllImport(SHCORE_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out int dpix, out int dpiy);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int GetDpiForWindow(IntPtr hwnd);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate void WINEVENTPROC(IntPtr hWinEventProc, uint eventId, IntPtr hwnd, int idObject,
            int idChild, int idEventThread, int dwmsEventTime);

        public const byte AC_SRC_OVER = 0;
        public const byte AC_SRC_ALPHA = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        public const int ULW_ALPHA = 2;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend,
            int dwFlags);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, IntPtr pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend,
            int dwFlags);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WINEVENTPROC pfnWinEventProc, int idProcess, int idThread, uint dwFlags);

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint EVENT_SYSTEM_MENUSTART = 0x0004;
        public const uint EVENT_SYSTEM_MENUEND = 0x0005;
        public const uint EVENT_SYSTEM_MENUPOPUPSTART = 0x0006;
        public const uint EVENT_SYSTEM_MENUPOPUPEND = 0x0007;
        public const uint EVENT_SYSTEM_CAPTURESTART = 0x0008;
        public const uint EVENT_SYSTEM_CAPTUREEND = 0x0009;
        public const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
        public const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;

        public const uint EVENT_OBJECT_CREATE = 0x8000;
        public const uint EVENT_OBJECT_DESTROY = 0x8001;
        public const uint EVENT_OBJECT_SHOW = 0x8002;
        public const uint EVENT_OBJECT_HIDE = 0x8003;
        public const uint EVENT_OBJECT_REORDER = 0x8004;
        public const uint EVENT_OBJECT_FOCUS = 0x8005;
        public const uint EVENT_OBJECT_SELECTION = 0x8006;
        public const uint EVENT_OBJECT_STATECHANGE = 0x800A;
        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
        public const uint EVENT_OBJECT_VALUECHANGE = 0x800E;
        public const uint EVENT_OBJECT_DEFACTIONCHANGE = 0x8011;
        public const uint EVENT_OBJECT_CLOAKED = 0x8017;
        public const uint EVENT_OBJECT_UNCLOAKED = 0x8018;

        public const uint STATE_SYSTEM_UNAVAILABLE = 0x1;
        public const uint STATE_SYSTEM_INVISIBLE = 0x8000;
        public const uint STATE_SYSTEM_OFFSCREEN = 0x10000;

        public const uint WINEVENT_OUTOFCONTEXT = 0;

        public const int OBJID_WINDOW = 0;
        public const int OBJID_CLIENT = -4;
        public const int OBJID_VSCROLL = -5;
        public const int OBJID_HSCROLL = -6;
        public const int OBJID_QUERYCLASSNAMEIDX = -12;

        public const int CHILDID_SELF = 0;

        public const int NAVDIR_NEXT = 5;
        public const int NAVDIR_PREVIOUS = 6;
        public const int NAVDIR_FIRSTCHILD = 7;
        public const int NAVDIR_LASTCHILD = 8;

        public static bool AccessibleNavigate(ref IAccessible acc, ref int child_id, int navdir)
        {
            var result = acc.accNavigate(navdir, child_id);
            if (result is null)
                return false;
            if (result is int i)
                child_id = i;
            else
                acc = (IAccessible)result;
            return true;
        }

        [DllImport(OLEACC_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int AccessibleObjectFromEvent(IntPtr hwnd, int dwId, int dwChildId,
            [Out, MarshalAs(UnmanagedType.Interface)] out IAccessible accessible,
            [Out, MarshalAs(UnmanagedType.Struct)] out object pvarChild);

        [DllImport(OLEACC_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int ObjectFromLresult(IntPtr lResult,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, IntPtr wParam, out IAccessible ppvObject);

        [ComImport, Guid("6d5140c1-7436-11ce-8034-00aa006009fa")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IServiceProvider
        {
            IntPtr QueryService(
                ref Guid guidService,
                ref Guid riid);
        }

        [ComImport, Guid("00000114-0000-0000-c000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IOleWindow
        {
            IntPtr GetWindow();

            void ContextSensitiveHelp(bool fEnterMode);
        }

        // IA2EventID
        public const int IA2_EVENT_TEXT_CHANGED = 0x11c;
        public const int IA2_EVENT_TEXT_INSERTED = 0x11e;
        public const int IA2_EVENT_TEXT_REMOVED = 0x11f;
        public const int IA2_EVENT_TEXT_UPDATED = 0x120;

        [StructLayout(LayoutKind.Sequential)]
        public struct IA2Locale
        {
            [MarshalAs(UnmanagedType.BStr)] public string language;
            [MarshalAs(UnmanagedType.BStr)] public string country;
            [MarshalAs(UnmanagedType.BStr)] public string variant;
        }

        [ComImport, Guid("618736e0-3c3d-11cf-810c-00aa00389b71")]
        [InterfaceType(ComInterfaceType.InterfaceIsDual)]
        public interface IAccessible
        {
            // IAccessible methods
            [return: MarshalAs(UnmanagedType.IDispatch)]
            object get_accParent();

            int get_accChildCount();

            [return: MarshalAs(UnmanagedType.IDispatch)]
            object get_accChild([MarshalAs(UnmanagedType.Struct)] object varChildId);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accName([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accValue([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accDescription([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.Struct)]
            object get_accRole([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.Struct)]
            object get_accState([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accHelp([MarshalAs(UnmanagedType.Struct)] object varID);

            long get_accHelpTopic([MarshalAs(UnmanagedType.BStr)] out string helpfile, [MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accKeyboardShortcut([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.Struct)]
            object get_accFocus();

            [return: MarshalAs(UnmanagedType.Struct)]
            object get_accSelection();

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accDefaultAction([MarshalAs(UnmanagedType.Struct)] object varID);

            void accSelect(long flagsSelect, [MarshalAs(UnmanagedType.Struct)] object varID);

            void accLocation(out int left, out int top, out int width, out int height, [MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.Struct)]
            object accNavigate(long dir, [MarshalAs(UnmanagedType.Struct)] object varStart);

            [return: MarshalAs(UnmanagedType.Struct)]
            object accHitTest(long left, long top);

            void accDoDefaultAction([MarshalAs(UnmanagedType.Struct)] object varID);

            void set_accName([MarshalAs(UnmanagedType.Struct)] object varID, [MarshalAs(UnmanagedType.BStr)] string name);

            void set_accValue([MarshalAs(UnmanagedType.Struct)] object varID, [MarshalAs(UnmanagedType.BStr)] string value);
        }

        [ComImport, Guid("e89f726e-c4f4-4c19-bb19-b647d7fa8478")]
        [InterfaceType(ComInterfaceType.InterfaceIsDual)]
        public interface IAccessible2
        {
            // IAccessible methods
            [return: MarshalAs(UnmanagedType.IDispatch)]
            object get_accParent();

            int get_accChildCount();

            [return: MarshalAs(UnmanagedType.IDispatch)]
            object get_accChild([MarshalAs(UnmanagedType.Struct)] object varChildId);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accName([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accValue([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accDescription([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.Struct)]
            object get_accRole([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.Struct)]
            object get_accState([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accHelp([MarshalAs(UnmanagedType.Struct)] object varID);

            long get_accHelpTopic([MarshalAs(UnmanagedType.BStr)] out string helpfile, [MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accKeyboardShortcut([MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.Struct)]
            object get_accFocus();

            [return: MarshalAs(UnmanagedType.Struct)]
            object get_accSelection();

            [return: MarshalAs(UnmanagedType.BStr)]
            string get_accDefaultAction([MarshalAs(UnmanagedType.Struct)] object varID);

            void accSelect(long flagsSelect, [MarshalAs(UnmanagedType.Struct)] object varID);

            void accLocation(out int left, out int top, out int width, out int height, [MarshalAs(UnmanagedType.Struct)] object varID);

            [return: MarshalAs(UnmanagedType.Struct)]
            object accNavigate(long dir, [MarshalAs(UnmanagedType.Struct)] object varStart);

            [return: MarshalAs(UnmanagedType.Struct)]
            object accHitTest(long left, long top);

            void accDoDefaultAction([MarshalAs(UnmanagedType.Struct)] object varID);

            void set_accName([MarshalAs(UnmanagedType.Struct)] object varID, [MarshalAs(UnmanagedType.BStr)] string name);

            void set_accValue([MarshalAs(UnmanagedType.Struct)] object varID, [MarshalAs(UnmanagedType.BStr)] string value);

            //IAccessible2 methods

            long nRelations { get; }

            IntPtr get_relation(long relationIndex); // returns IAccessibleRelation*

            long get_relations(long maxRelations, [Out] IntPtr[] relations); // array of IAccessibleRelation*

            long role { get; }

            void scrollTo(int scrollType); // takes IA2ScrollType enum

            void scrollToPoint(int coordinateType, long x, long y); // takes IA2CoordinateType enum

            long get_groupPosition(out long groupLevel, out long similarItemsInGroup);

            long states { get; } // returns AccessibleStates

            string extendedRole { [return: MarshalAs(UnmanagedType.BStr)] get; }

            string localizedExtendedRole { [return: MarshalAs(UnmanagedType.BStr)] get; }

            long nExtendedStates { get; }

            long get_extendedStates(long maxExtendedStates,
                [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.BStr)][In][Out] ref string[] extendedStates);

            long get_localizedExtendedStates(long maxLocalizedExtendedStates,
                [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.BStr)][In][Out] ref string[] localizedExtendedStates);

            int uniqueID { get; }

            IntPtr windowHandle { get; }

            long indexInParent { get; }

            IA2Locale locale { get; }

            string attributes { [return: MarshalAs(UnmanagedType.BStr)] get; }
        }

        [ComImport, Guid("d49ded83-5b25-43f4-9b95-93b44595979e")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAccessibleApplication
        {
            string appName
            {
                [return: MarshalAs(UnmanagedType.BStr)]
                get;
            }

            string appVersion
            {
                [return: MarshalAs(UnmanagedType.BStr)]
                get;
            }

            string toolkitName
            {
                [return: MarshalAs(UnmanagedType.BStr)]
                get;
            }

            string toolkitVersion
            {
                [return: MarshalAs(UnmanagedType.BStr)]
                get;
            }
        }

        public static readonly Guid IID_IAccessible = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");
        public static readonly Guid IID_IAccessible2 = new Guid("e89f726e-c4f4-4c19-bb19-b647d7fa8478");
        public static readonly Guid IID_IAccessibleApplication = new Guid("d49ded83-5b25-43f4-9b95-93b44595979e");

        public static IAccessible2 QueryIAccessible2(object acc)
        {
            try
            {
                IServiceProvider sp = (IServiceProvider)acc;

                if (sp is null)
                    return null;

                Guid service_id = IID_IAccessible;
                Guid iid = IID_IAccessible2;

                IntPtr pIA2;

                try
                {
                    // This is the method documented by the spec - guidService = IID_IAccessible
                    pIA2 = sp.QueryService(ref service_id, ref iid);
                }
                catch
                {
                    // This is the method UI Automation seems to use - guidService = IID_IAccessible2
                    service_id = IID_IAccessible2;
                    pIA2 = sp.QueryService(ref service_id, ref iid);
                }

                return (IAccessible2)Marshal.GetTypedObjectForIUnknown(pIA2, typeof(IAccessible2));
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotImplementedException)
            {
            }
            catch (InvalidCastException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (COMException)
            {
            }
            return null;
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        public const uint GA_PARENT = 1;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr GetDesktopWindow();

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate bool WNDENUMPROC(IntPtr hwnd, IntPtr lParam);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern bool EnumWindows(WNDENUMPROC lpEnumProc, IntPtr lParam);

        [ThreadStatic]
        private static List<IntPtr> EnumWindowsList;

        private static bool EnumWindowsToList(IntPtr hwnd, IntPtr lParam)
        {
            EnumWindowsList.Add(hwnd);
            return true;
        }

        private static WNDENUMPROC EnumWindowsToListDelegate = new WNDENUMPROC(EnumWindowsToList);

        public static List<IntPtr> EnumWindows()
        {
            var result = new List<IntPtr>();
            EnumWindowsList = result;

            EnumWindows(EnumWindowsToListDelegate, IntPtr.Zero);

            EnumWindowsList = null;

            return result;
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool EnumChildWindows(IntPtr hwndParent, WNDENUMPROC lpEnumProc, IntPtr lParam);

        private static bool EnumChildWindowsToList(IntPtr hwnd, IntPtr lParam)
        {
            if (GetAncestor(hwnd, GA_PARENT) == lParam)
                EnumWindowsList.Add(hwnd);
            return true;
        }

        private static WNDENUMPROC EnumChildWindowsToListDelegate = new WNDENUMPROC(EnumChildWindowsToList);

        public static List<IntPtr> EnumImmediateChildWindows(IntPtr hwndParent)
        {
            var result = new List<IntPtr>();
            EnumWindowsList = result;

            EnumChildWindows(hwndParent, EnumChildWindowsToListDelegate, hwndParent);

            EnumWindowsList = null;

            return result;
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int lpdwProcessId);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        public const int GW_OWNER = 4;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left, top, right, bottom;
            public int width => right - left;
            public int height => bottom - top;

            public bool IsEmpty()
            {
                return right <= left || bottom <= top;
            }

            internal RECT Intersect(RECT other)
            {
                RECT result = new RECT
                {
                    left = Math.Max(left, other.left),
                    top = Math.Max(top, other.top),
                    right = Math.Min(right, other.right),
                    bottom = Math.Min(bottom, other.bottom)
                };
                return result;
            }

            internal RECT Offset(POINT other)
            {
                RECT result = new RECT
                {
                    left = left + other.x,
                    top = top + other.y,
                    right = right + other.x,
                    bottom = bottom + other.y,
                };
                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x, y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx, cy;
        }

        public const int GUI_CARETBLINKING = 0x1;
        public const int GUI_INMOVESIZE = 0x2;
        public const int GUI_INMENUMODE = 0x4;
        public const int GUI_POPUPMENUMODE = 0x8;
        public const int GUI_SYSTEMMENUMODE = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool GetGUIThreadInfo(int idThread, ref GUITHREADINFO pgui);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        public static extern int RealGetWindowClassW(IntPtr hwnd, StringBuilder ptszClassName, int cchClassNameMax);

        public static string RealGetWindowClass(IntPtr hwnd)
        {
            // According to WNDCLASS documentation, the maximum name length is 256
            var sb = new StringBuilder(256);

            RealGetWindowClassW(hwnd, sb, 256);

            return sb.ToString();
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        public static extern int GetClassNameW(IntPtr hWnd, StringBuilder ptszClassName, int cchClassNameMax);

        public static string GetClassName(IntPtr hwnd)
        {
            // According to WNDCLASS documentation, the maximum name length is 256
            var sb = new StringBuilder(256);

            GetClassNameW(hwnd, sb, 256);

            return sb.ToString();
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        public static extern short VkKeyScanW(char ch);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern short GetAsyncKeyState(int vKey);

        public const int INPUT_MOUSE = 0;
        public const int INPUT_KEYBOARD = 1;
        public const int INPUT_HARDWARE = 2;

        public const int KEYEVENTF_EXTENDEDKEY = 0x1;
        public const int KEYEVENTF_KEYUP = 0x2;
        public const int KEYEVENTF_UNICODE = 0x4;
        public const int KEYEVENTF_SCANCODE = 0x8;

        public const int MOUSEEVENTF_MOVE = 0x1;
        public const int MOUSEEVENTF_LEFTDOWN = 0x2;
        public const int MOUSEEVENTF_LEFTUP = 0x4;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x8;
        public const int MOUSEEVENTF_RIGHTUP = 0x10;
        public const int MOUSEEVENTF_MIDDLEDOWN = 0x20;
        public const int MOUSEEVENTF_MIDDLEUP = 0x40;
        public const int MOUSEEVENTF_XDOWN = 0x80;
        public const int MOUSEEVENTF_XUP = 0x100;
        public const int MOUSEEVENTF_WHEEL = 0x800;
        public const int MOUSEEVENTF_HWHEEL = 0x1000;
        public const int MOUSEEVENTF_MOVE_NOCOALESCE = 0x2000;
        public const int MOUSEEVENTF_VIRTUALDESK = 0x4000;
        public const int MOUSEEVENTF_ABSOLUTE = 0x8000;

        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public short wVk;
            public short wScan;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public int uMsg;
            public short wParamL;
            public short wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUT_UNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;

            public INPUT_UNION u;
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int SendInput(int cInputs, INPUT[] pInputs, int cbSize);

        public const int IDC_ARROW = 32512;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

        public static bool WindowIsVisible(IntPtr hwnd)
        {
            var style = unchecked((int)(long)GetWindowLong(hwnd, GWL_STYLE));
            return (style & WS_VISIBLE) != 0;
        }

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        public const int SM_CXVSCROLL = 2;
        public const int SM_CYHSCROLL = 3;
        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int GetSystemMetrics(int nIndex);

        public static void NormalizeScreenCoordinates(ref int x, ref int y)
        {
            x = (int)Math.Round((double)(x - GetSystemMetrics(SM_XVIRTUALSCREEN)) * 65535 / GetSystemMetrics(SM_CXVIRTUALSCREEN));
            y = (int)Math.Round((double)(y - GetSystemMetrics(SM_YVIRTUALSCREEN)) * 65535 / GetSystemMetrics(SM_CYVIRTUALSCREEN));
        }

        public const int MA_NOACTIVATE = 3;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CREATESTRUCTW
        {
            public IntPtr lpCreateParams;
            public IntPtr hInstance;
            public IntPtr hMenu;
            public IntPtr hwndParent;
            public int cy, cx, y, x;
            public int style;
            public IntPtr lpszName;
            public IntPtr lpszClass;
            public int dwExStyle;
        }

        public const int WM_DESTROY = 0x2;
        public const int WM_ACTIVATE = 0x6;
        public const int WM_GETTEXT = 0xd;
        public const int WM_MOUSEACTIVATE = 0x21;
        public const int WM_GETOBJECT = 0x3d;
        public const int WM_NCCREATE = 0x81;
        public const int WM_HSCROLL = 0x114;
        public const int WM_VSCROLL = 0x115;
        public const int WM_USER = 0x400;

        public const int WA_ACTIVE = 1;

        public const int SB_LINEUP = 0;
        public const int SB_LINELEFT = 0;
        public const int SB_LINEDOWN = 1;
        public const int SB_LINERIGHT = 1;
        public const int SB_PAGEUP = 2;
        public const int SB_PAGELEFT = 2;
        public const int SB_PAGEDOWN = 3;
        public const int SB_PAGERIGHT = 3;
        public const int SB_THUMBPOSITION = 4;
        public const int SB_THUMBTRACK = 5;
        public const int SB_TOP = 6;
        public const int SB_LEFT = 6;
        public const int SB_BOTTOM = 7;
        public const int SB_RIGHT = 7;
        public const int SB_ENDSCROLL = 8;

        public const int SBS_VERT = 0x1;

        public static int HIWORD(int dword)
        {
            return (dword >> 16) & 0xffff;
        }

        public static int LOWORD(int dword)
        {
            return dword & 0xffff;
        }

        public static IntPtr MAKEWPARAM(ushort low, ushort high)
        {
            return new IntPtr((high << 16) | low);
        }

        public const int QS_KEY = 0x1;
        public const int QS_MOUSEMOVE = 0x2;
        public const int QS_MOUSEBUTTON = 0x4;
        public const int QS_MOUSE = QS_MOUSEMOVE | QS_MOUSEBUTTON;
        public const int QS_POSTMESSAGE = 0x8;
        public const int QS_TIMER = 0x10;
        public const int QS_PAINT = 0x20;
        public const int QS_SENDMESSAGE = 0x40;
        public const int QS_HOTKEY = 0x80;
        public const int QS_RAWINPUT = 0x400;
        public const int QS_TOUCH = 0x800;
        public const int QS_POINTER = 0x1000;
        public const int QS_INPUT = QS_MOUSE | QS_KEY | QS_RAWINPUT | QS_TOUCH | QS_POINTER;
        public const int QS_ALLEVENTS = QS_INPUT | QS_POSTMESSAGE | QS_TIMER | QS_PAINT | QS_HOTKEY;
        public const int QS_ALLINPUT = QS_ALLEVENTS | QS_SENDMESSAGE;

        public const int INFINITE = -1;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        unsafe public extern static int MsgWaitForMultipleObjects(int nCount, IntPtr* pHandles, bool fWaitAll,
            int dwMilliseconds, int dwWakeMask);

        unsafe public static int MsgWaitForSingleObject(SafeWaitHandle handle, int dwMilliseconds, int dwWakeMask)
        {
            bool success = false;
            IntPtr raw_handle;
            try
            {
                handle.DangerousAddRef(ref success);

                if (!success || handle.IsClosed || handle.IsInvalid)
                    // Avoids race condition when another thread may free the handle - treat that as signaled
                    return 0;

                raw_handle = handle.DangerousGetHandle();

                return MsgWaitForMultipleObjects(1, &raw_handle, false, dwMilliseconds, dwWakeMask);
            }
            finally
            {
                if (success)
                    handle.DangerousRelease();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public int message;
            public IntPtr wParam;
            public IntPtr lParam;
            public int time;
            public POINT pt;
            public int lPrivate;
        }

        public const int PM_REMOVE = 0x1;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public extern static bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, int wMsgFilterMin,
            int wMsgFilterMax, int wRemoveMsg);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public extern static bool TranslateMessage(ref MSG lpMsg);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public extern static bool DispatchMessageW(ref MSG lpMsg);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public extern static int RegisterWindowMessageW([MarshalAs(UnmanagedType.LPWStr)] string lpString);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public extern static IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate void SendAsyncProc(IntPtr hwnd, int msg, IntPtr userdata, IntPtr lresult);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public extern static bool SendMessageCallbackW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam, SendAsyncProc lpResultCallBack, IntPtr dwData);

        private class SendMessageAsyncCallback
        {
            public SendMessageAsyncCallback()
            {
                source = new TaskCompletionSource<IntPtr>();
                gchandle = (IntPtr)GCHandle.Alloc(this);
            }

            public TaskCompletionSource<IntPtr> source;
            public IntPtr gchandle;

            public static void callback(IntPtr hwnd, int msg, IntPtr userdata, IntPtr lresult)
            {
                var handle = GCHandle.FromIntPtr(userdata);

                var instance = (SendMessageAsyncCallback)handle.Target;

                instance.source.SetResult(lresult);

                handle.Free();
            }

            public static SendAsyncProc callback_fn = new SendAsyncProc(callback);
        }

        public static Task<IntPtr> SendMessageAsync(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam)
        {
            var callback = new SendMessageAsyncCallback();
            if (!SendMessageCallbackW(hwnd, msg, wparam, lparam, SendMessageAsyncCallback.callback_fn, callback.gchandle))
                return Task.FromException<IntPtr>(new Win32Exception());
            return callback.source.Task;
        }

        public const int SIF_RANGE = 0x1;
        public const int SIF_PAGE = 0x2;
        public const int SIF_POS = 0x4;
        public const int SIF_DISABLENOSCROLL = 0x8;
        public const int SIF_TRACKPOS = 0x10;
        public const int SIF_ALL = SIF_RANGE|SIF_PAGE|SIF_POS|SIF_TRACKPOS;

        [StructLayout(LayoutKind.Sequential)]
        public struct SCROLLBARINFO
        {
            public int cbSize;
            public RECT rcScrollBar;
            public int dxyLineButton;
            public int xyThumbTop;
            public int xyThumbBottom;
            public int reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=6)]
            public int[] rgstate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SCROLLINFO
        {
            public int cbSize;
            public int fMask;
            public int nMin;
            public int nMax;
            public int nPage;
            public int nPos;
            public int nTrackPos;
            public int max_value => nMax - nPage + 1; // The actual maximum value of nPos
        }

        public const int SB_HORZ = 0;
        public const int SB_VERT = 1;
        public const int SB_CTL = 2;

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public extern static bool GetScrollInfo(IntPtr hwnd, int nBar, ref SCROLLINFO si);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public extern static bool GetScrollBarInfo(IntPtr hwnd, int idObject, ref SCROLLBARINFO sbi);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public extern static int SetScrollInfo(IntPtr hwnd, int nBar, ref SCROLLINFO lpsi, bool redraw);

        [ComImport, Guid("4ce576fa-83dc-4F88-951c-9d0782b4e376")]
        public class UIHostNoLaunch
        {
        }

        [ComImport, Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ITipInvocation
        {
            void Toggle(IntPtr hwnd);
        }

        public static IntPtr GetSdlWindowHwnd(IntPtr sdl_window)
        {
            SDL_SysWMinfo info = default;

            SDL_VERSION(out info.version);

            if (SDL_GetWindowWMInfo(sdl_window, ref info) != SDL_bool.SDL_TRUE)
                throw new Exception(SDL_GetError());

            return info.info.win.window;
        }

        // Button
        public const int BS_DEFPUSHBUTTON = 0x1;
        public const int BS_CHECKBOX = 0x2;
        public const int BS_AUTOCHECKBOX = 0x3;
        public const int BS_RADIOBUTTON = 0x4;
        public const int BS_3STATE = 0x5;
        public const int BS_AUTO3STATE = 0x6;
        public const int BS_AUTORADIOBUTTON = 0x9;
        public const int BS_DEFSPLITBUTTON = 0xc;
        public const int BS_DEFCOMMANDLINK = 0xf;
        public const int BS_TYPEMASK = 0xf;

        public const int BS_LEFT = 0x100;
        public const int BS_RIGHT = 0x200;
        public const int BS_CENTER = 0x300;
        public const int BS_TOP = 0x400;
        public const int BS_BOTTOM = 0x800;
        public const int BS_VCENTER = 0xC00;

        public const int BM_GETSTATE = 0xf2;
        public const int BM_CLICK = 0xf5;

        public const int BST_CHECKED = 0x1;
        public const int BST_INDETERMINATE = 0x2;
        public const int BST_PUSHED = 0x4;
        public const int BST_FOCUS = 0x8;
        public const int BST_HOT = 0x200;
        public const int BST_DROPDOWNPUSHED = 0x400;

        // Combo Box
        public const int CBS_SIMPLE = 0x1;
        public const int CBS_DROPDOWN = 0x2;
        public const int CBS_DROPDOWNLIST = 0x3;

        public const int CBS_TYPEMASK = 0x3; // not an SDK constant, just here for code clarity

        public const int CB_SHOWDROPDOWN = 0x14f;
        public const int CB_GETCOMBOBOXINFO = 0x164;

        [StructLayout(LayoutKind.Sequential)]
        public struct COMBOBOXINFO
        {
            public int cbSize;
            public RECT rcItem;
            public RECT rcButton;
            public int stateButton;
            public IntPtr hwndCombo;
            public IntPtr hwndItem;
            public IntPtr hwndList;
        }

        // Dialog
        public const int DM_GETDEFID = WM_USER;

        public const int DC_HASDEFID = 0x534b;

        // Edit
        public const int ES_CENTER = 0x1;
        public const int ES_RIGHT = 0x2;

        // Header
        public const int HDM_FIRST = 0x1200;
        public const int HDM_GETITEMCOUNT = HDM_FIRST + 0;
        public const int HDM_GETITEMRECT = HDM_FIRST + 7;

        // List view
        public const int LVS_ICON = 0x0;
        public const int LVS_REPORT = 0x1;
        public const int LVS_SMALLICON = 0x2;
        public const int LVS_LIST = 0x3;
        public const int LVS_TYPEMASK = 0x3;
        public const int LVS_ALIGNTOP = 0x0;
        public const int LVS_ALIGNMASK = 0xc00;

        public const int LVS_EX_CHECKBOXES = 0x4;

        public const int LVM_FIRST = 0x1000;
        public const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
        public const int LVM_GETITEMRECT = LVM_FIRST + 14;
        public const int LVM_ENSUREVISIBLE = LVM_FIRST + 19;
        public const int LVM_SCROLL = LVM_FIRST + 20;
        public const int LVM_GETCOLUMNWIDTH = LVM_FIRST + 29;
        public const int LVM_GETHEADER = LVM_FIRST + 31;
        public const int LVM_GETTOPINDEX = LVM_FIRST + 39;
        public const int LVM_GETCOUNTPERPAGE = LVM_FIRST + 40;
        public const int LVM_GETORIGIN = LVM_FIRST + 41;
        public const int LVM_SETITEMSTATE = LVM_FIRST + 43;
        public const int LVM_GETITEMSTATE = LVM_FIRST + 44;
        public const int LVM_GETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 55;
        public const int LVM_SETVIEW = LVM_FIRST + 142;
        public const int LVM_GETVIEW = LVM_FIRST + 143;

        public const int LV_VIEW_ICON = 0;
        public const int LV_VIEW_DETAILS = 1;
        public const int LV_VIEW_SMALLICON = 2;
        public const int LV_VIEW_LIST = 3;
        public const int LV_VIEW_TILE = 4;
        public const int LV_VIEW_MAX = 5;

        public const int LVIR_BOUNDS = 0;
        public const int LVIR_ICON = 1;
        public const int LVIR_LABEL = 2;
        public const int LVIR_SELECTBOUNDS = 3;

        public const int LVIS_STATEIMAGEMASK = 0xf000;

        // Not in SDK headers:
        public const int LVIS_UNCHECKED = 0x1000;
        public const int LVIS_CHECKED = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        public struct LVITEM32
        {
            public int mask;
            public int iItem;
            public int iSubItem;
            public int state;
            public int stateMask;
            public int pszText; // pointer - LPSTR
            public int cchTextMax;
            public int iImage;
            public int lParam; // pointer - LPARAM
            public int iIndent;
            public int iGroupId;
            public int cColumns;
            public int puColumns; // pointer - PUINT
            public int piColFmt; // pointer - int*
            public int iGroup;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LVITEM64
        {
            public int mask;
            public int iItem;
            public int iSubItem;
            public int state;
            public int stateMask;
            public long pszText; // pointer - LPSTR
            public int cchTextMax;
            public int iImage;
            public long lParam; // pointer - LPARAM
            public int iIndent;
            public int iGroupId;
            public int cColumns;
            public long puColumns; // pointer - PUINT
            public long piColFmt; // pointer - int*
            public int iGroup;
        }

        // Static Control
        public const int SS_LEFT = 0x0000;
        public const int SS_CENTER = 0x0001;
        public const int SS_RIGHT = 0x0002;
        public const int SS_ICON = 0x0003;
        public const int SS_BLACKRECT = 0x0004;
        public const int SS_GRAYRECT = 0x0005;
        public const int SS_WHITERECT = 0x0006;
        public const int SS_BLACKFRAME = 0x0007;
        public const int SS_GRAYFRAME = 0x0008;
        public const int SS_WHITEFRAME = 0x0009;
        public const int SS_USERITEM = 0x000a;
        public const int SS_SIMPLE = 0x000b;
        public const int SS_LEFTNOWORDWRAP = 0x000c;
        public const int SS_OWNERDRAW = 0x000d;
        public const int SS_BITMAP = 0x000e;
        public const int SS_ENHMETAFILE = 0x000f;
        public const int SS_ETCHEDHORZ = 0x0010;
        public const int SS_ETCHEDVERT = 0x0011;
        public const int SS_ETCHEDFRAME = 0x0012;
        public const int SS_TYPEMASK = 0x001f;
        public const int SS_ENDELLIPSIS = 0x4000;
        public const int SS_PATHELLIPSIS = 0x8000;
        public const int SS_WORDELLIPSIS = 0xc000;
        public const int SS_ELLIPSISMASK = 0xc000;

        // Tab Control
        public const int TCS_BOTTOM = 0x0002;
        public const int TCS_RIGHT = 0x0002;
        public const int TCS_VERTICAL = 0x0080;
        public const int TCS_BUTTONS = 0x0100;
        public const int TCS_MULTILINE = 0x0200;
        public const int TCS_RAGGEDRIGHT = 0x0800;

        public const int TCM_FIRST = 0x1300;
        public const int TCM_GETITEMCOUNT = TCM_FIRST + 4;
        public const int TCM_GETITEMRECT = TCM_FIRST + 10;
        public const int TCM_GETCURSEL = TCM_FIRST + 11;

        // Trackbar
        public const int TBS_AUTOTICKS = 0x00000001;
        public const int TBS_VERT = 0x00000002;
        public const int TBS_TOP = 0x00000004;
        public const int TBS_LEFT = 0x00000004;
        public const int TBS_BOTH = 0x00000008;

        public const int TBM_GETPOS = WM_USER;
        public const int TBM_GETRANGEMIN = WM_USER + 1;
        public const int TBM_GETRANGEMAX = WM_USER + 2;
        public const int TBM_SETPOS = WM_USER + 5;
        public const int TBM_GETLINESIZE = WM_USER + 23;
        public const int TBM_SETPOSNOTIFY = WM_USER + 34;

        public const int TB_THUMBPOSITION = 4;
        public const int TB_THUMBTRACK = 5;
        public const int TB_ENDTRACK = 8;

        // UI Automation:

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int UiaGetRootNode(out IntPtr phnode);

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern bool UiaNodeRelease(IntPtr hnode);

        public static readonly int UiaAppendRuntimeId = 3;

        public static readonly Guid IID_IAccessibleEx = new Guid("f8b80ada-2c44-48d0-89be-5ff23c9cd875");

        public const int UIA_StructureChangedEventId = 20002;
        public const int UIA_AutomationPropertyChangedEventId = 20004;

        public const int UIA_BoundingRectanglePropertyId = 30001;
        public const int UIA_RuntimeIdPropertyId = 30003;
        public const int UIA_ControlTypePropertyId = 30003;
        public const int UIA_IsEnabledPropertyId = 30010;
        public const int UIA_NativeWindowHandlePropertyId = 30020;
        public const int UIA_IsOffscreenPropertyId = 30022;

        [ComImport, Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IRawElementProviderSimple
        {
            int ProviderOptions { get; }

            [return: MarshalAs(UnmanagedType.IUnknown)]
            object GetPatternProvider(int patternId);

            [return: MarshalAs(UnmanagedType.Struct)]
            object GetPropertyValue(int propertyId);

            IRawElementProviderSimple HostRawElementProvider { get; }
        }

        public enum NavigateDirection
        {
            Parent,
            NextSibling,
            PreviousSibling,
            FirstChild,
            LastChild
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UiaRect
        {
            public double left, top, width, height;
        }

        [ComImport, Guid("f7063da8-8359-439c-9297-bbc5299a7d87")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IRawElementProviderFragment
        {
            IRawElementProviderFragment Navigate(NavigateDirection direction);

            [return: MarshalAs(UnmanagedType.SafeArray)]
            int[] GetRuntimeId();

            UiaRect BoundingRectangle { get; }

            [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)]
            object[] GetEmbeddedFragmentRoots();

            void SetFocus();

            IRawElementProviderFragmentRoot FragmentRoot { get; }
        }

        [ComImport, Guid("620ce2a5-ab8f-40a9-86cb-de3c75599b58")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IRawElementProviderFragmentRoot
        {
            IRawElementProviderFragment ElementProviderFromPoint(double x, double y);

            IRawElementProviderFragment GetFocus();
        }

        public enum EventArgsType
        {
            Simple,
            PropertyChanged,
            StructureChanged,
            AsyncContentLoaded,
            WindowClosed,
            TextEditTextChanged,
            Changes,
            Notification
        }

        [StructLayout (LayoutKind.Sequential)]
        public struct UiaEventArgs
        {
            public EventArgsType Type;
            public int EventId;
        }

        public enum StructureChangeType
        {
            ChildAdded,
            ChildRemoved,
            ChildrenInvalidated,
            ChildrenBulkAdded,
            ChildrenBulkRemoved,
            ChildrenReordered
        }

        [StructLayout (LayoutKind.Sequential)]
        public struct UiaStructureChangedEventArgs
        {
            public EventArgsType Type;
            public int EventId;

            public StructureChangeType StructureChangeType;
            public IntPtr pRuntimeId;
            public int cRuntimeIdLen;
        }

        [StructLayout (LayoutKind.Sequential)]
        public struct UiaPropertyChangedEventArgs
        {
            public EventArgsType Type;
            public int EventId;

            public int PropertyId;
            [MarshalAs(UnmanagedType.Struct)]
            public object OldValue;
            [MarshalAs(UnmanagedType.Struct)]
            public object NewValue;
        }

        [Flags]
        public enum TreeScope
        {
            Element = 0x1,
            Children = 0x2,
            Descendants = 0x4,
            Parent = 0x8,
            Ancestors = 0x10,
            Subtree = Element | Children | Descendants
        }

        public enum ConditionType
        {
            True,
            False,
            Property,
            And,
            Or,
            Not
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UiaCondition
        {
            public ConditionType ConditionType;
        }

        public enum AutomationElementMode
        {
            None,
            Full
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UiaCacheRequest
        {
            public IntPtr pViewCondition;
            public TreeScope Scope;
            public IntPtr pProperties;
            public int cProperties;
            public IntPtr pPatterns;
            public int cPatterns;
            public AutomationElementMode automationElementMode;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate void UiaEventCallback(IntPtr pArgs, [MarshalAs(UnmanagedType.SafeArray)] object[,] pRequestedData,
            [MarshalAs(UnmanagedType.BStr)] string pTreeStructure);

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int UiaAddEvent(IntPtr hnode, int eventid, UiaEventCallback pCallback,
            TreeScope scope, IntPtr pProperties, int cProperties, ref UiaCacheRequest pRequest,
            out IntPtr phEvent);

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int UiaRemoveEvent(IntPtr hEvent);

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int UiaEventAddWindow(IntPtr hEvent, IntPtr hwnd);

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int UiaEventRemoveWindow(IntPtr hEvent, IntPtr hwnd);

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int UiaGetRuntimeId(IntPtr hnode,
            [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_I4)] out int[] pruntimeId);

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int UiaNavigate(IntPtr hnode, NavigateDirection direction, IntPtr pCondition,
            ref UiaCacheRequest pRequest, [MarshalAs(UnmanagedType.SafeArray)] out object[,] ppRequestedData,
            [MarshalAs(UnmanagedType.BStr)] out string ppTreeStructure);

        public const int UIA_PFIA_UNWRAP_BRIDGE = 1;

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int UiaProviderFromIAccessible(IAccessible pAccessible, int idChild, int dwFlags, out IRawElementProviderSimple ppProvider);

        public const int UIA_IAFP_UNWRAP_BRIDGE = 1;

        [DllImport(UIA_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int UiaIAccessibleFromProvider(IRawElementProviderSimple pProvider, int dwFlags, out IAccessible ppAccessible, [MarshalAs(UnmanagedType.Struct)] out object pvarChild);
    }
}
#endif
