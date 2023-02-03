using FlaUI.UIA2.Patterns;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.Input;
using Xalia.UiDom;

namespace Xalia.Uia
{
    internal class UiaAdjustScrollContainer : UiDomRoutine
    {
        public UiaAdjustScrollContainer(UiaElement element) : base(element, "uia_adjust_scroll_container")
        {
            Element = element;
        }

        private static readonly double xscale = 100.0 / 6 / 32767;
        private static readonly double yscale = 100.0 / 6 / 32767;
        private static readonly long delay_ticks = 10000000 / 120;

        public new UiaElement Element { get; }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            var stopwatch = new Stopwatch();
            InputState prev_state = new InputState(InputStateKind.Disconnected), state;
            long last_repeat = 0;
            double xremainder=0, yremainder=0;
            while (true)
            {
                state = await queue.Dequeue();
                if (state.Kind == InputStateKind.Disconnected)
                    break;
                if ((state.Kind == InputStateKind.Pulse || state.Kind == InputStateKind.Repeat) &&
                    prev_state.Kind == InputStateKind.AnalogJoystick)
                {
                    (xremainder, yremainder) = await DoAdjustment(prev_state, xscale, yscale, xremainder, yremainder);
                    if (stopwatch.IsRunning)
                        stopwatch.Reset();
                }
                else if (state.Kind == InputStateKind.AnalogJoystick && state.Intensity >= 1000)
                {
                    if (!stopwatch.IsRunning)
                    {
                        stopwatch.Start();
                        last_repeat = 0;
                        (xremainder, yremainder) = await DoAdjustment(prev_state, xscale, yscale, xremainder, yremainder);
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

                        (xremainder, yremainder) = await DoAdjustment(prev_state,
                            Math.Min(num_steps, 60) * xscale, Math.Min(num_steps, 60) * yscale,
                            xremainder, yremainder);
                        last_repeat += delay_ticks * num_steps;
                    }
                }
                else if (state.Kind == InputStateKind.PixelDelta && state.Intensity > 0)
                {
                    (xremainder, yremainder) = await DoAdjustment(state, 1, 1, xremainder, yremainder);
                }
                else
                {
                    stopwatch.Reset();
                }
                prev_state = state;
            }
        }

        private async Task<(double xremainder, double yremainder)> DoAdjustment(InputState state, double xmult, double ymult, double xremainder, double yremainder)
        {
            double xofs = state.XAxis * xmult + xremainder;
            double yofs = state.YAxis * ymult + yremainder;

            double xadjustment = Math.Truncate(xofs);
            double yadjustment = Math.Truncate(yofs);

            if (xadjustment == 0 && yadjustment == 0)
            {
                return (xofs, yofs);
            }

            return await Element.Root.CommandThread.OnBackgroundThread(() => {
                try
                {
                    var scroll = Element.ElementWrapper.AutomationElement.Patterns.Scroll.Pattern;

                    double xpercent = scroll.HorizontalScrollPercent;
                    if (xpercent != -1) // UIA_ScrollPatternNoScroll
                    {
                        xpercent += xadjustment / scroll.HorizontalViewSize * 100;
                        xpercent = Math.Min(Math.Max(xpercent, 0), 100);
                    }

                    double ypercent = scroll.VerticalScrollPercent;
                    if (ypercent != -1) // UIA_ScrollPatternNoScroll
                    {
                        ypercent += yadjustment / scroll.VerticalViewSize * 100;
                        ypercent = Math.Min(Math.Max(ypercent, 0), 100);
                    }

                    scroll.SetScrollPercent(xpercent, ypercent);

                    return (
                        xpercent == -1 ? 0 : xremainder,
                        ypercent == -1 ? 0 : yremainder);
                }
                catch (Exception e)
                {
                    if (!UiaElement.IsExpectedException(e))
                        throw;
                    return (0, 0);
                }
            }, Element.ElementWrapper.Pid);
        }
    }
}
