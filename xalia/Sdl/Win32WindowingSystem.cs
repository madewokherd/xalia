#if WINDOWS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using SDL2;

using static Xalia.Interop.Win32;

namespace Xalia.Sdl
{
    internal class Win32WindowingSystem : WindowingSystem
    {
        public override bool CanShowKeyboard()
        {
            return true;
        }

        public override Task ShowKeyboardAsync()
        {
            try {
                var invocation = new UIHostNoLaunch() as ITipInvocation;

                invocation.Toggle(GetDesktopWindow());
            }
            catch (COMException)
            {
                // If TabTip.exe is not running, we can get REGDB_E_CLASSNOTREG
                Process.Start(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Common Files\microsoft shared\ink\TabTip.exe"));
            }

            return Task.CompletedTask;
        }

        public override bool CanSendKeys => true;

        public override int GetKeySym(string key)
        {
            switch (key)
            {
                case "click":
                case "lclick":
                case "leftclick":
                case "leftbutton":
                case "left_click":
                case "left_button":
                case "lbutton":
                    return 0x1;
                case "rclick":
                case "rightclick":
                case "rightbutton":
                case "right_click":
                case "right_button":
                case "rbutton":
                    return 0x2;
                case "ctrl_break":
                case "cancel":
                    return 0x3;
                case "mclick":
                case "middleclick":
                case "middlebutton":
                case "middle_click":
                case "middle_button":
                case "mbutton":
                    return 0x4;
                case "xbutton1":
                    return 0x5;
                case "xbutton2":
                    return 0x6;
                case "back_space":
                case "backspace":
                case "back":
                    return 0x8;
                case "tab":
                    return 0x9;
                case "clear":
                    return 0xc;
                case "enter":
                case "return":
                    return 0xd;
                case "shift":
                    return 0x10;
                case "ctrl":
                case "control":
                    return 0x11;
                case "alt":
                case "meta":
                case "menu":
                    return 0x12;
                case "pause":
                case "break":
                    return 0x13;
                case "capslock":
                case "caps_lock":
                case "capital":
                    return 0x14;
                case "kana":
                case "hanguel":
                case "hangul":
                    return 0x15;
                case "ime_on":
                    return 0x16;
                case "junja":
                    return 0x17;
                case "final":
                    return 0x18;
                case "hanja":
                case "kanji":
                    return 0x19;
                case "ime_off":
                    return 0x1a;
                case "esc":
                case "escape":
                    return 0x1b;
                case "convert":
                    return 0x1c;
                case "nonconvert":
                    return 0x1d;
                case "accept":
                    return 0x1e;
                case "mode_change":
                case "modechange":
                    return 0x1f;
                case "space":
                case "space_bar":
                case "spacebar":
                    return 0x20;
                case "page_up":
                case "pageup":
                case "prior":
                    return 0x21;
                case "page_down":
                case "pagedown":
                case "next":
                    return 0x22;
                case "end":
                    return 0x23;
                case "home":
                    return 0x24;
                case "left":
                    return 0x25;
                case "up":
                    return 0x26;
                case "right":
                    return 0x27;
                case "down":
                    return 0x28;
                case "select":
                    return 0x29;
                case "print":
                    return 0x2a;
                case "execute":
                    return 0x2b;
                case "printscreen":
                case "print_screen":
                case "snapshot":
                    return 0x2c;
                case "ins":
                case "insert":
                    return 0x2d;
                case "del":
                case "delete":
                    return 0x2e;
                case "help":
                    return 0x2f;
                case "win":
                case "windows":
                case "super":
                case "lwindows":
                case "lsuper":
                case "left_win":
                case "left_windows":
                case "left_super":
                case "lwin":
                    return 0x5b;
                case "rwindows":
                case "rsuper":
                case "right_win":
                case "right_windows":
                case "right_super":
                case "rwin":
                    return 0x5c;
                case "applications":
                case "apps":
                    return 0x5d;
                case "sleep":
                    return 0x5f;
                case "numpad0":
                    return 0x60;
                case "numpad1":
                    return 0x61;
                case "numpad2":
                    return 0x62;
                case "numpad3":
                    return 0x63;
                case "numpad4":
                    return 0x64;
                case "numpad5":
                    return 0x65;
                case "numpad6":
                    return 0x66;
                case "numpad7":
                    return 0x67;
                case "numpad8":
                    return 0x68;
                case "numpad9":
                    return 0x69;
                case "numpad_multiply":
                case "multiply":
                    return 0x6a;
                case "numpad_add":
                case "add":
                    return 0x6b;
                case "separator": // ???
                    return 0x6c;
                case "numpad_subtract":
                case "subtract":
                    return 0x6d;
                case "numpad_decimal":
                case "decimal":
                    return 0x6e;
                case "numpad_divide":
                case "divide":
                    return 0x6f;
                case "num_lock":
                case "numlock":
                    return 0x90;
                case "scroll_lock":
                case "scrolllock":
                case "scroll":
                    return 0x91;
                case "leftshift":
                case "left_shift":
                case "lshift":
                    return 0xa0;
                case "rightshift":
                case "right_shift":
                case "rshift":
                    return 0xa1;
                case "leftcontrol":
                case "leftctrl":
                case "left_control":
                case "left_ctrl":
                case "lctrl":
                case "lcontrol":
                    return 0xa2;
                case "rightcontrol":
                case "rightctrl":
                case "right_control":
                case "right_ctrl":
                case "rctrl":
                case "rcontrol":
                    return 0xa3;
                case "leftalt":
                case "leftmeta":
                case "leftmenu":
                case "left_alt":
                case "left_meta":
                case "left_menu":
                case "lalt":
                case "lmeta":
                case "lmenu":
                    return 0xa4;
                case "rightalt":
                case "rightmeta":
                case "rightmenu":
                case "right_alt":
                case "right_meta":
                case "right_menu":
                case "ralt":
                case "rmeta":
                case "rmenu":
                    return 0xa5;
                case "browser_back":
                    return 0xa6;
                case "forward":
                case "browser_forward":
                    return 0xa7;
                case "refresh":
                case "browser_refresh":
                    return 0xa8;
                case "browser_stop":
                    return 0xa9;
                case "search":
                case "browser_search":
                    return 0xaa;
                case "favorites":
                case "browser_favorites":
                    return 0xab;
                case "browser_home":
                    return 0xac;
                case "mute":
                case "volume_mute":
                    return 0xad;
                case "volume_down":
                    return 0xae;
                case "volume_up":
                    return 0xaf;
                case "next_track":
                case "media_next_track":
                    return 0xb0;
                case "prev_track":
                case "media_prev_track":
                    return 0xb1;
                case "media_stop":
                    return 0xb2;
                case "play_pause":
                case "media_play_pause":
                    return 0xb3;
                case "mail":
                case "launch_mail":
                    return 0xb4;
                case "media_select":
                case "launch_media_select":
                    return 0xb5;
                case "app1":
                case "launch_app1":
                    return 0xb6;
                case "app2":
                case "launch_app2":
                    return 0xb7;
                case "oem1":
                case "oem_1":
                    return 0xba;
                case "plus":
                case "oem_plus":
                    return 0xba;
                case "comma":
                case "oem_comma":
                    return 0xbc;
                case "minus":
                case "oem_minus":
                    return 0xbd;
                case "period":
                case "oem_period":
                    return 0xbe;
                case "oem2":
                case "oem_2":
                    return 0xbf;
                case "oem3":
                case "oem_3":
                    return 0xc0;
                case "oem4":
                case "oem_4":
                    return 0xdb;
                case "oem5":
                case "oem_5":
                    return 0xdc;
                case "oem6":
                case "oem_6":
                    return 0xdd;
                case "oem7":
                case "oem_7":
                    return 0xde;
                case "oem102":
                case "oem_102":
                    return 0xe2;
                case "process":
                case "process_key":
                    return 0xe5;
                /* sending this on its own is not useful :
                case "packet":
                    return 0xe7;
                */
                case "attn":
                    return 0xf6;
                case "crsel":
                    return 0xf7;
                case "exsel":
                    return 0xf8;
                case "erase_eof":
                case "ereof":
                    return 0xf9;
                case "play":
                    return 0xfa;
                case "zoom":
                    return 0xfb;
                case "pa1":
                    return 0xfd;
                case "oem_clear":
                    return 0xfe;
            }
            if (key.StartsWith("f") && int.TryParse(key.Substring(1), out int fkey) &&
                fkey > 0 && fkey <= 24)
            {
                return 0x6f + fkey;
            }
            if (key.Length == 1)
            {
                // Can't get an actual virtual key code for this, just use some private encoding to pass it through
                return key[0] << 16;
            }
            return base.GetKeySym(key);
        }

        unsafe public override Task SendKey(int keysym)
        {
            int modifier_flags = 0;
            bool packet = false;
            INPUT[] inputs = new INPUT[8];
            int num_inputs = 0;

            if ((keysym & 0xffff) == 0)
            {
                // This is a character passed directly from GetKeySym.
                short scan = VkKeyScanW((char)(keysym >> 16));

                if ((scan & 0xf800) != 0) // Either -1 or flags we don't know how to handle
                    packet = true;
                else
                {
                    keysym = scan & 0xff;
                    modifier_flags = scan >> 8;
                }
            }

            if (packet)
            {
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].u.ki.wScan = (short)(keysym >> 16);
                inputs[0].u.ki.dwFlags = KEYEVENTF_UNICODE;
                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].u.ki.wScan = (short)(keysym >> 16);
                inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE|KEYEVENTF_KEYUP;
                num_inputs = 2;
            }
            else
            {
                if (((modifier_flags & 1) == 1) && (GetAsyncKeyState(VK_SHIFT) & 0x8000) == 0)
                {
                    inputs[num_inputs].type = INPUT_KEYBOARD;
                    inputs[num_inputs].u.ki.wVk = VK_SHIFT;
                    num_inputs++;
                }
                if (((modifier_flags & 2) == 2) && (GetAsyncKeyState(VK_CONTROL) & 0x8000) == 0)
                {
                    inputs[num_inputs].type = INPUT_KEYBOARD;
                    inputs[num_inputs].u.ki.wVk = VK_CONTROL;
                    num_inputs++;
                }
                if (((modifier_flags & 4) == 4) && (GetAsyncKeyState(VK_MENU) & 0x8000) == 0)
                {
                    inputs[num_inputs].type = INPUT_KEYBOARD;
                    inputs[num_inputs].u.ki.wVk = VK_MENU;
                    num_inputs++;
                }
                inputs[num_inputs].type = INPUT_KEYBOARD;
                inputs[num_inputs].u.ki.wVk = (short)keysym;
                num_inputs++;

                for (int i = num_inputs-1; i >= 0; i--)
                {
                    inputs[num_inputs] = inputs[i];
                    inputs[num_inputs].u.ki.dwFlags |= KEYEVENTF_KEYUP;
                    num_inputs++;
                }
            }

            SendInput(num_inputs, inputs, Marshal.SizeOf<INPUT>());

            return Task.CompletedTask;
        }

        public override Task SendMouseMotion(int x, int y)
        {
            INPUT[] inputs = new INPUT[1];

            NormalizeScreenCoordinates(ref x, ref y);

            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = x;
            inputs[0].u.mi.dy = y;
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK;

            SendInput(1, inputs, Marshal.SizeOf<INPUT>());
            return Task.CompletedTask;
        }

        public override Task SendMouseButton(MouseButton button, bool is_press)
        {
            INPUT[] inputs = new INPUT[1];

            inputs[0].type = INPUT_MOUSE;
            switch (button)
            {
                case MouseButton.LeftButton:
                    inputs[0].u.mi.dwFlags = is_press ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
                    break;
                case MouseButton.MiddleButton:
                    inputs[0].u.mi.dwFlags = is_press ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;
                    break;
                case MouseButton.RightButton:
                    inputs[0].u.mi.dwFlags = is_press ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
                    break;
                default:
                    throw new NotImplementedException();
            }

            SendInput(1, inputs, Marshal.SizeOf<INPUT>());
            return Task.CompletedTask;
        }

        public override Task SendScroll(int xdelta, int ydelta)
        {
            INPUT[] inputs = new INPUT[2];
            int i = 0;
            if (xdelta != 0)
            {
                inputs[i].type = INPUT_MOUSE;
                inputs[i].u.mi.mouseData = xdelta;
                inputs[i].u.mi.dwFlags = MOUSEEVENTF_HWHEEL;
                i++;
            }
            if (ydelta != 0)
            {
                inputs[i].type = INPUT_MOUSE;
                inputs[i].u.mi.mouseData = -ydelta;
                inputs[i].u.mi.dwFlags = MOUSEEVENTF_WHEEL;
                i++;
            }
            SendInput(i, inputs, Marshal.SizeOf<INPUT>());
            return Task.CompletedTask;
        }
    }
}
#endif