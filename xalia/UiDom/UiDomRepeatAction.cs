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
        public UiDomRepeatAction(UiDomValue context, UiDomRoot root, GudlExpression action_expression, TimeSpan initial_delay, TimeSpan repeat_delay)
        {
            Context = context;
            Root = root;
            ActionExpression = action_expression;
            InitialDelay = initial_delay;
            RepeatDelay = repeat_delay;
        }

        public UiDomValue Context { get; }
        public UiDomRoot Root { get; }
        public GudlExpression ActionExpression { get; }
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

            GudlExpression action_expr = arglist[0];
            UiDomRoutine action = context.Evaluate(action_expr, root, depends_on) as UiDomRoutine;
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

            return new UiDomRepeatAction(context, root, action_expr, initial_delay_ts, repeat_delay_ts);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomRepeatAction r)
                return ActionExpression.Equals(r.ActionExpression) && InitialDelay == r.InitialDelay && RepeatDelay == r.RepeatDelay;
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(UiDomRepeatAction).GetHashCode() ^ (Context, ActionExpression, InitialDelay, RepeatDelay).GetHashCode();
        }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            Stopwatch stopwatch = new Stopwatch();
            bool is_initial = true;
            using (var watcher = new ExpressionWatcher(Context, Root, ActionExpression))
            {
                InputQueue inner_queue = null;
                UiDomRoutine action = null;
                InputState prev_state = new InputState(InputStateKind.Disconnected);
                InputState state = new InputState(InputStateKind.Disconnected);
                while (true)
                {
                    if (stopwatch.IsRunning && !state.Pressed)
                    {
                        stopwatch.Reset();
                    }
                    if (state.Kind != InputStateKind.Disconnected)
                    {
                        var new_action = watcher.CurrentValue as UiDomRoutine;
                        if (new_action is null)
                        {
                            if (!(action is null))
                            {
                                inner_queue.Enqueue(new InputState(InputStateKind.Disconnected));
                                action = null;
                                inner_queue = null;
                                continue;
                            }
                        }
                        else if (!new_action.Equals(action))
                        {
                            if (!(action is null))
                            {
                                inner_queue.Enqueue(new InputState(InputStateKind.Disconnected));
                                action = null;
                                inner_queue = null;
                            }
                            inner_queue = new InputQueue(queue.Action);
                            action = new_action;
                            Utils.RunTask(action.ProcessInputQueue(inner_queue));
                            inner_queue.Enqueue(state);
                            continue;
                        }
                    }
                    if (!queue.IsEmpty || state.Kind == InputStateKind.Disconnected)
                    {
                        prev_state = state;
                        state = await queue.Dequeue();
                        if (!(inner_queue is null))
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
                        continue;
                    }
                    if (stopwatch.IsRunning && state.Pressed)
                    {
                        long remaining_delay = (is_initial ? InitialDelay : RepeatDelay).Ticks -
                                stopwatch.ElapsedTicks;
                        if (remaining_delay <= 0)
                        {
                            stopwatch.Restart();
                            is_initial = false;
                            if (!(inner_queue is null))
                            {
                                inner_queue.Enqueue(new InputState(InputStateKind.Repeat));
                                inner_queue.Enqueue(state);
                                await inner_queue.WaitForConsumer();
                            }
                            continue;
                        }
                        await Task.WhenAny(queue.WaitForInput(), watcher.WaitChanged(), Task.Delay(new TimeSpan(remaining_delay)));
                        continue;
                    }
                    await Task.WhenAny(queue.WaitForInput(), watcher.WaitChanged());
                }
            }
        }

        public override string ToString()
        {
            return $"{Context}.(repeat_action({ActionExpression}, {InitialDelay.Ticks / 10000.0}, {RepeatDelay.Ticks / 10000.0}))";
        }
    }
}
