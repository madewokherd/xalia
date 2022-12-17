using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Input;

namespace Xalia.UiDom
{
    public class UiDomRepeatAction : UiDomRoutine
    {
        public UiDomRepeatAction(UiDomRoutine action, TimeSpan initial_delay, TimeSpan repeat_delay)
        {
            Action = action;
            InitialDelay = initial_delay;
            RepeatDelay = repeat_delay;
        }

        public UiDomRoutine Action { get; }
        public TimeSpan InitialDelay { get; }
        public TimeSpan RepeatDelay { get; }

        public static UiDomValue GetMethod()
        {
            return new UiDomMethod("repeat_action", ApplyFn);
        }

        private static UiDomValue ApplyFn(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length < 2)
                return UiDomUndefined.Instance;

            UiDomRoutine action = context.Evaluate(arglist[0], root, depends_on) as UiDomRoutine;
            if (action is null)
                return UiDomUndefined.Instance;

            UiDomValue initial_delay = context.Evaluate(arglist[1], root, depends_on);
            TimeSpan initial_delay_ts;

            if (initial_delay is UiDomInt ii)
            {
                if (ii.Value <= 0)
                    return action;
                initial_delay_ts = new TimeSpan((long)(ii.Value * 10000));
            }
            else if (initial_delay is UiDomDouble id)
            {
                if (id.Value <= 0)
                    return action;
                initial_delay_ts = new TimeSpan((long)(id.Value * 10000));
            }
            else
                return action;

            TimeSpan repeat_delay_ts;

            if (arglist.Length >= 3)
            {
                UiDomValue repeat_delay = context.Evaluate(arglist[2], root, depends_on);

                if (repeat_delay is UiDomInt ri)
                {
                    if (ri.Value <= 0)
                        repeat_delay_ts = initial_delay_ts;
                    else
                        repeat_delay_ts = new TimeSpan((long)(ri.Value * 10000));
                }
                else if (repeat_delay is UiDomDouble rd)
                {
                    if (rd.Value <= 0)
                        repeat_delay_ts = initial_delay_ts;
                    else
                        repeat_delay_ts = new TimeSpan((long)(rd.Value * 10000));
                }
                else
                    repeat_delay_ts = initial_delay_ts;
            }
            else
                repeat_delay_ts = initial_delay_ts;

            return new UiDomRepeatAction(action, initial_delay_ts, repeat_delay_ts);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomRepeatAction r)
                return Action.Equals(r.Action) && InitialDelay == r.InitialDelay && RepeatDelay == r.RepeatDelay;
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(UiDomRepeatAction).GetHashCode() ^ (Action, InitialDelay, RepeatDelay).GetHashCode();
        }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            Stopwatch stopwatch = new Stopwatch();
            bool is_initial = true;
            InputQueue inner_queue = new InputQueue(queue.Action);
            Utils.RunTask(Action.ProcessInputQueue(inner_queue));
            InputState prev_state = new InputState(InputStateKind.Disconnected);
            while (true) {
                InputState state = await queue.Dequeue();
                inner_queue.Enqueue(state);
                if (state.Kind == InputStateKind.Disconnected)
                {
                    break;
                }
                if (state.JustPressed(prev_state))
                {
                    stopwatch.Restart();
                    is_initial = true;
                }
                if (state.Pressed)
                {
                    if (stopwatch.IsRunning)
                    {
                        while (queue.IsEmpty)
                        {
                            long remaining_delay = (is_initial ? InitialDelay : RepeatDelay).Ticks -
                                stopwatch.ElapsedTicks;
                            if (remaining_delay <= 0)
                            {
                                stopwatch.Restart();
                                is_initial = false;
                                await inner_queue.WaitForConsumer();
                                inner_queue.Enqueue(new InputState(InputStateKind.Repeat));
                                inner_queue.Enqueue(state);
                                continue;
                            }
                            await Task.WhenAny(queue.WaitForInput(), Task.Delay(new TimeSpan(remaining_delay)));
                        }
                    }
                }
                else
                {
                    is_initial = true;
                    stopwatch.Reset();
                }

                await inner_queue.WaitForConsumer();

                prev_state = state;
            }
        }

        public override string ToString()
        {
            return $"repeat_action({Action}, {InitialDelay.Ticks / 10000.0}, {RepeatDelay.Ticks / 10000.0})";
        }
    }
}
