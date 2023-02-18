using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xalia.Input;
using Xalia.Sdl;
using Xalia.UiDom;

namespace Xalia.Ui
{
    internal class SendScroll : UiDomRoutine
    {
        public SendScroll(UiDomElement element, WindowingSystem windowing) : base(element, "send_scroll")
        {
            Windowing = windowing;
        }

        private static readonly double xscale = 100.0 / 6 / 32767;
        private static readonly double yscale = 100.0 / 6 / 32767;
        private static readonly long delay_ticks = 10000000 / 120;

        public WindowingSystem Windowing { get; }

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

        private async Task<(double,double)> DoAdjustment(InputState state, double xmult, double ymult, double xremainder, double yremainder)
        {
            double xofs = state.XAxis * xmult + xremainder;
            double yofs = state.YAxis * ymult + yremainder;

            double xadjustment = Math.Truncate(xofs);
            double yadjustment = Math.Truncate(yofs);

            if (xadjustment == 0 && yadjustment == 0)
            {
                return (xofs, yofs);
            }

            var position = await Element.GetClickablePoint();

            if (!position.Item1)
            {
                Utils.DebugWriteLine($"WARNING: Could not get clickable point for {Element}");
                return (xofs - xadjustment, yofs - yadjustment);
            }

            try
            {
                await Windowing.SendMouseMotion(position.Item2, position.Item3);

                await Windowing.SendScroll((int)xadjustment, (int)yadjustment);
            }
            catch (NotImplementedException)
            {
                Utils.DebugWriteLine($"WARNING: Cannot send mouse scroll events on the current windowing system");
            }

            return (xofs - xadjustment, yofs - yadjustment);
        }
    }
}
