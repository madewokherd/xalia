using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Diagnostics;
using Tmds.DBus;
using Xalia.AtSpi.DBus;
using Xalia.Input;

namespace Xalia.UiDom
{
    internal class UiDomAdjustScrollbars : UiDomRoutine
    {
        public UiDomAdjustScrollbars(UiDomElement hscroll, UiDomElement vscroll)
        {
            HScroll = hscroll;
            VScroll = vscroll;
        }

        public UiDomElement HScroll { get; }
        public UiDomElement VScroll { get; }

        private static readonly double xscale = 1.0 / 3 / 32767;
        private static readonly double yscale = 1.0 / 3 / 32767;
        private static readonly long delay_ticks = 10000000 / 120;

        public override bool Equals(object obj)
        {
            if (obj is UiDomAdjustScrollbars r)
            {
                return HScroll == r.HScroll && VScroll == r.VScroll;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(UiDomAdjustScrollbars).GetHashCode() ^
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
            double xinc, yinc;
            double loc_xscale, loc_yscale;

            if (HScroll is null)
                xinc = 0;
            else
                xinc = await HScroll.GetMinimumIncrement();

            if (VScroll is null)
                yinc = 0;
            else
                yinc = await VScroll.GetMinimumIncrement();

            loc_xscale = xinc * xscale;
            loc_yscale = yinc * yscale;

            while (true)
            {
                state = await queue.Dequeue();
                if (state.Kind == InputStateKind.Disconnected)
                    break;
                if ((state.Kind == InputStateKind.Pulse || state.Kind == InputStateKind.Repeat) &&
                    prev_state.Kind == InputStateKind.AnalogJoystick)
                {
                    await DoAdjustment(prev_state, loc_xscale, loc_yscale);
                    if (stopwatch.IsRunning)
                        stopwatch.Reset();
                }
                else if (state.Kind == InputStateKind.AnalogJoystick && state.Intensity >= 1000)
                {
                    if (!stopwatch.IsRunning)
                    {
                        stopwatch.Start();
                        last_repeat = 0;
                        await DoAdjustment(state, loc_xscale, loc_yscale);
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

                        await DoAdjustment(state, Math.Min(num_steps, 60) * loc_xscale, Math.Min(num_steps, 60) * loc_yscale);
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
                await HScroll.OffsetValue(xofs);
            }

            if (!(VScroll is null))
            {
                await VScroll.OffsetValue(yofs);
            }
        }
    }
}
