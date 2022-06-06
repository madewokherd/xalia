using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xalia
{
    internal class XKeyCodes
    {
        public static int GetKeyCode(string name)
        {
            switch (name)
            {
                // TODO: fill this out?
                case "space":
                    return 0x20;
                case "back_space":
                case "backspace":
                    return 0xff08;
                case "tab":
                    return 0xff09;
                case "enter":
                case "return":
                    return 0xff0d;
                case "pause":
                    return 0xff13;
                case "esc":
                case "escape":
                    return 0xff1b;
                case "home":
                    return 0xff50;
                case "left":
                    return 0xff51;
                case "up":
                    return 0xff52;
                case "right":
                    return 0xff53;
                case "down":
                    return 0xff54;
                case "page_up":
                case "pageup":
                    return 0xff55;
                case "page_down":
                case "pagedown":
                    return 0xff56;
                case "end":
                    return 0xff57;
                case "insert":
                    return 0xff63;
                case "del":
                case "delete":
                    return 0xffff;
            }
            if (name.Length == 1)
                return name[0];
            return 0;
        }
    }
}
