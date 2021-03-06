#if WINDOWS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Interop.UIAutomationClient;

using static SDL2.SDL;

namespace Xalia.Interop
{
    internal static class Win32
    {
        const string USER_LIB = "user32";
        const string OLEACC_LIB = "oleacc";

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

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const int WS_VISIBLE = 0x10000000;

        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOPMOST = 0x00000008;

        public static readonly IntPtr HWND_TOPMOST = (IntPtr)(-1);

        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate void WINEVENTPROC(IntPtr hWinEventProc, uint eventId, IntPtr hwnd, int idObject,
            int idChild, int idEventThread, int dwmsEventTime);

        [DllImport(USER_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WINEVENTPROC pfnWinEventProc, int idProcess, int idThread, uint dwFlags);

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

        public const uint EVENT_OBJECT_CREATE = 0x8000;
        public const uint EVENT_OBJECT_DESTROY = 0x8001;

        public const uint WINEVENT_OUTOFCONTEXT = 0;

        public const int OBJID_WINDOW = 0;
        public const int OBJID_CLIENT = -4;

        public const int CHILDID_SELF = 0;

        [DllImport(OLEACC_LIB, CallingConvention = CallingConvention.Winapi)]
        public static extern int AccessibleObjectFromEvent(IntPtr hwnd, int dwId, int dwChildId,
            [Out, MarshalAs(UnmanagedType.Interface)] out IAccessible accessible,
            [Out, MarshalAs(UnmanagedType.Struct)] out object pvarChild);

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

        public static IEnumerable<IntPtr> EnumWindows()
        {
            var result = new List<IntPtr>();
            EnumWindowsList = result;

            EnumWindows(EnumWindowsToListDelegate, IntPtr.Zero);

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
        }

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

        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx, dy;
            public int mouseData, dwFlags, time;
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

        public static bool WindowIsVisible(IntPtr hwnd)
        {
            var style = unchecked((int)(long)GetWindowLong(hwnd, GWL_STYLE));
            return (style & WS_VISIBLE) != 0;
        }

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
    }
}
#endif
