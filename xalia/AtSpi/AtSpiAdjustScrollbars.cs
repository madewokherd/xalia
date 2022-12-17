using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.AtSpi.DBus;
using Xalia.Input;
using Xalia.UiDom;

namespace Xalia.AtSpi
{
    internal class AtSpiAdjustScrollbars : UiDomRoutine
    {
        public AtSpiAdjustScrollbars(AtSpiElement hscroll, AtSpiElement vscroll)
        {
            HScroll = hscroll;
            VScroll = vscroll;
        }

        public AtSpiElement HScroll { get; }
        public AtSpiElement VScroll { get; }

        private static readonly double xscale = 100.0 / 3 / 32767;
        private static readonly double yscale = 100.0 / 3 / 32767;

        public override bool Equals(object obj)
        {
            if (obj is AtSpiAdjustScrollbars r)
            {
                return HScroll == r.HScroll && VScroll == r.VScroll;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(AtSpiAdjustScrollbars).GetHashCode() ^
                (HScroll, VScroll).GetHashCode();
        }

        public override string ToString()
        {
            string h = HScroll is null ? "undefined" : HScroll.ToString();
            string v = VScroll is null ? "undefined" : VScroll.ToString();
            return $"adjust_scrollbars({h}, {v})";
        }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            InputState prev_state = new InputState(InputStateKind.Disconnected), state;
            do
            {
                state = await queue.Dequeue();
                if (state.JustPressed(prev_state))
                {
                    if (state.Kind == InputStateKind.AnalogJoystick)
                        await DoAdjustment(state);
                    else if (prev_state.Kind == InputStateKind.AnalogJoystick)
                        await DoAdjustment(prev_state);
                }
                prev_state = state;
            } while (state.Kind != InputStateKind.Disconnected);
        }

        private async Task DoAdjustment(InputState state)
        {
            double xofs = state.XAxis * xscale;
            double yofs = state.YAxis * yscale;

            if (!(HScroll is null))
            {
                if (xofs > 0)
                {
                    var maximum_value = await HScroll.value_iface.GetMaximumValueAsync();

                    var current_value = await HScroll.value_iface.GetCurrentValueAsync();

                    var new_value = current_value + xofs;

                    if (new_value > maximum_value)
                        new_value = maximum_value;

                    if (new_value != current_value)
                        await HScroll.value_iface.SetCurrentValueAsync(new_value);
                }
                else if (xofs < 0)
                {
                    var minimum_value = await HScroll.value_iface.GetMinimumValueAsync();

                    var current_value = await HScroll.value_iface.GetCurrentValueAsync();

                    var new_value = current_value + xofs;

                    if (new_value < minimum_value)
                        new_value = minimum_value;

                    if (new_value != current_value)
                        await HScroll.value_iface.SetCurrentValueAsync(new_value);
                }
            }
            if (!(VScroll is null))
            {
                if (yofs > 0)
                {
                    var maximum_value = await VScroll.value_iface.GetMaximumValueAsync();

                    var current_value = await VScroll.value_iface.GetCurrentValueAsync();

                    var new_value = current_value + yofs;

                    if (new_value > maximum_value)
                        new_value = maximum_value;

                    if (new_value != current_value)
                        await VScroll.value_iface.SetCurrentValueAsync(new_value);
                }
                else if (yofs < 0)
                {
                    var minimum_value = await VScroll.value_iface.GetMinimumValueAsync();

                    var current_value = await VScroll.value_iface.GetCurrentValueAsync();

                    var new_value = current_value + yofs;

                    if (new_value < minimum_value)
                        new_value = minimum_value;

                    if (new_value != current_value)
                        await VScroll.value_iface.SetCurrentValueAsync(new_value);
                }
            }
        }
    }
}
