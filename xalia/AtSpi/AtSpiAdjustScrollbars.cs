using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
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

        private static readonly double xscale = 100.0 / 6 / 32767;
        private static readonly double yscale = 100.0 / 6 / 32767;
        private static readonly long delay_ticks = 10000000 / 120;

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
            var stopwatch = new Stopwatch();
            InputState prev_state = new InputState(InputStateKind.Disconnected), state;
            long last_repeat = 0;
            while (true)
            {
                state = await queue.Dequeue();
                if (state.Kind == InputStateKind.Disconnected)
                    break;
                if ((state.Kind == InputStateKind.Pulse || state.Kind == InputStateKind.Repeat) &&
                    prev_state.Kind == InputStateKind.AnalogJoystick)
                {
                    await DoAdjustment(prev_state, xscale, yscale);
                    if (stopwatch.IsRunning)
                        stopwatch.Reset();
                }
                else if (state.Kind == InputStateKind.AnalogJoystick && state.Intensity >= 1000)
                {
                    if (!stopwatch.IsRunning)
                    {
                        stopwatch.Start();
                        last_repeat = 0;
                        await DoAdjustment(state, xscale, yscale);
                    }
                    while (queue.IsEmpty)
                    {
                        var elapsed_ticks = stopwatch.ElapsedTicks - last_repeat;
                        if (elapsed_ticks < delay_ticks)
                        {
                            await Task.WhenAny(queue.WaitForInput(), Task.Delay(new TimeSpan(delay_ticks - elapsed_ticks)));
                            continue;
                        }
                        long num_steps = elapsed_ticks / delay_ticks;

                        await DoAdjustment(state, Math.Min(num_steps, 60) * xscale, Math.Min(num_steps, 60) * yscale);
                        last_repeat += delay_ticks * num_steps;
                    }
                }
                else if (state.Kind == InputStateKind.PixelDelta && state.Intensity > 0)
                {
                    await DoAdjustment(state, 1, 1);
                }
                else
                {
                    stopwatch.Reset();
                }
                prev_state = state;
            }
        }

        private async Task DoAdjustment(InputState state, double xmult, double ymult)
        {
            double xofs = state.XAxis * xmult;
            double yofs = state.YAxis * ymult;

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
