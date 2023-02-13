using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.UiDom;
using static Xalia.Interop.Win32;

namespace Xalia.Uia.Win32
{
    public class Win32VirtualScrollbar : UiDomElement
    {
        public Win32VirtualScrollbar(Win32Element parent, bool vertical) : base(parent.Root)
        {
            Parent = parent;
            Vertical = vertical;
            Hwnd = parent.Hwnd;
        }

        static UiDomEnum role = new UiDomEnum(new string[] { "scroll_bar", "scrollbar" });
        private bool MinimumIncrementKnown;
        private double MinimumIncrement;

        static Win32VirtualScrollbar()
        {
            string[] aliases = {
                "vertical", "win32_vertical",
                "horizontal", "win32_horizontal",
                "cxvscroll", "win32_cxvscroll",
                "cyhscroll", "win32_cyhscroll",
                "minimum_increment", "win32_minimum_increment",
            };
            property_aliases = new Dictionary<string, string>(aliases.Length / 2);
            for (int i=0; i<aliases.Length; i+=2)
            {
                property_aliases[aliases[i]] = aliases[i + 1];
            }
        }

        private static readonly Dictionary<string, string> property_aliases;

        public new Win32Element Parent { get; }
        public bool Vertical { get; }
        public IntPtr Hwnd { get; }

        public override string DebugId => $"Win32VirtualScrollbar-{Hwnd}-{(Vertical ? 'V': 'H')}";

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
                case "is_win32_subelement":
                case "is_win32_virtual_scrollbar":
                case "scroll_bar":
                case "scrollbar":
                case "visible":
                case "enabled":
                    return UiDomBoolean.True;
                case "role":
                case "control_type":
                    return role;
                case "win32_vertical":
                    return UiDomBoolean.FromBool(Vertical);
                case "win32_horizontal":
                    return UiDomBoolean.FromBool(!Vertical);
                case "win32_cxvscroll":
                    return new UiDomInt(GetSystemMetrics(SM_CXVSCROLL));
                case "win32_cyhscroll":
                    return new UiDomInt(GetSystemMetrics(SM_CYHSCROLL));
                case "win32_minimum_increment":
                    depends_on.Add((this, new IdentifierExpression("win32_minimum_increment")));
                    if (MinimumIncrementKnown)
                        return new UiDomDouble(MinimumIncrement);
                    return UiDomUndefined.Instance;
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }

        protected override void DumpProperties()
        {
            if (MinimumIncrementKnown)
                Console.WriteLine($"  win32_minimum_increment: {MinimumIncrement}");
            base.DumpProperties();
        }

        protected override void WatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_minimum_increment":
                        PollProperty(expression, RefreshMinimumIncrement, 200);
                        break;
                }
            }
            base.WatchProperty(expression);
        }

        protected override void UnwatchProperty(GudlExpression expression)
        {
            if (expression is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "win32_minimum_increment":
                        EndPollProperty(expression);
                        MinimumIncrementKnown = false;
                        break;
                }
            }
            base.UnwatchProperty(expression);
        }

        private async Task RefreshMinimumIncrement()
        {
            var minimum_increment = await GetMinimumIncrement();

            if (!MinimumIncrementKnown || minimum_increment != MinimumIncrement)
            {
                MinimumIncrementKnown = true;
                MinimumIncrement = minimum_increment;
                PropertyChanged("win32_minimum_increment", minimum_increment);
            }
        }

        public override Task<double> GetMinimumIncrement()
        {
            if (Vertical)
                return Parent.GetVScrollMinimumIncrement();
            else
                return Parent.GetHScrollMinimumIncrement();
        }

        public override Task OffsetValue(double ofs)
        {
            if (Vertical)
                return Parent.OffsetVScroll(ofs);
            else
                return Parent.OffsetHScroll(ofs);
        }
    }
}
