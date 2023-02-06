using Superpower.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;

using static Xalia.Interop.Win32;

namespace Xalia.Uia.Win32
{
    internal class Win32Trackbar : Win32Element
    {
        public Win32Trackbar(IntPtr hwnd, UiDomRoot root) : base(hwnd, root) { }

        static UiDomEnum role = new UiDomEnum(new string[] { "slider" });
        static Win32Trackbar()
        {
            string[] aliases = {
                "vertical", "win32_vertical",
                "horizontal", "win32_horizontal",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i=0; i<aliases.Length; i+=2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
        }

        private static readonly Dictionary<string, string> property_aliases;

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
                case "is_win32_trackbar":
                case "slider":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "win32_vertical":
                    depends_on.Add((this, new IdentifierExpression("win32_style")));
                    if (WindowStyleKnown)
                        return UiDomBoolean.FromBool((WindowStyle & TBS_VERT) == TBS_VERT);
                    return UiDomUndefined.Instance;
                case "win32_horizontal":
                    depends_on.Add((this, new IdentifierExpression("win32_style")));
                    if (WindowStyleKnown)
                        return UiDomBoolean.FromBool((WindowStyle & TBS_VERT) == 0);
                    return UiDomUndefined.Instance;
            }
            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override void DumpProperties()
        {
            if (WindowStyleKnown)
            {
                Console.WriteLine($"  win32_vertical: {(WindowStyle & TBS_VERT) == TBS_VERT}");
                Console.WriteLine($"  win32_horizontal: {(WindowStyle & TBS_VERT) == 0}");
            }
            base.DumpProperties();
        }

        public override async Task<double> GetMinimumIncrement()
        {
            var result = (int)await SendMessageAsync(Hwnd, TBM_GETLINESIZE, IntPtr.Zero, IntPtr.Zero);
            if (result == 0)
                result = 1;
            return result;
        }

        private double remainder;

        public override async Task OffsetValue(double ofs)
        {
            int current_pos = (int)await SendMessageAsync(Hwnd, TBM_GETPOS, IntPtr.Zero, IntPtr.Zero);

            double new_pos = current_pos + remainder + ofs;

            int pos_ofs = (int)Math.Truncate(new_pos - current_pos);

            int new_pos_int = current_pos + pos_ofs;

            if (new_pos_int != current_pos)
            {
                if (pos_ofs < 0)
                {
                    int min = (int)await SendMessageAsync(Hwnd, TBM_GETRANGEMIN, IntPtr.Zero, IntPtr.Zero);

                    if (new_pos_int < min)
                        new_pos = new_pos_int = min;
                }
                else
                {
                    int max = (int)await SendMessageAsync(Hwnd, TBM_GETRANGEMAX, IntPtr.Zero, IntPtr.Zero);

                    if (new_pos_int > max)
                        new_pos = new_pos_int = max;
                }
            }

            if (new_pos_int != current_pos)
            {
                await SendMessageAsync(Hwnd, TBM_SETPOS, new IntPtr(1), new IntPtr(new_pos_int));
                IntPtr parent = GetAncestor(Hwnd, GA_PARENT);
                bool vertical = ((int)GetWindowLong(Hwnd, GWL_STYLE) & TBS_VERT) == TBS_VERT;
                await SendMessageAsync(parent, vertical ? WM_VSCROLL : WM_HSCROLL,
                    MAKEWPARAM(TB_THUMBTRACK, (ushort)new_pos_int), Hwnd);
                await SendMessageAsync(parent, vertical ? WM_VSCROLL : WM_HSCROLL,
                    MAKEWPARAM(TB_THUMBPOSITION, (ushort)new_pos_int), Hwnd);
                await SendMessageAsync(parent, vertical ? WM_VSCROLL : WM_HSCROLL,
                    MAKEWPARAM(TB_ENDTRACK, 0), Hwnd);
            }

            remainder = new_pos - new_pos_int;
        }
    }
}
