using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Input;

namespace Xalia.UiDom
{
    public class UiDomRepeatAction : UiDomRoutine
    {
        public UiDomRepeatAction(UiDomValue context, UiDomRoot root, GudlExpression action_expression, long initial_delay, long repeat_delay)
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
        public long InitialDelay { get; }
        public long RepeatDelay { get; }

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
            long initial_delay_ticks;

            if (initial_delay.TryToDouble(out double id))
            {
                if (id <= 0)
                    return action;
                initial_delay_ticks = (long)(id * Stopwatch.Frequency / 1000);
            }
            else
                return action;

            long repeat_delay_ticks;

            if (arglist.Length >= 3)
            {
                UiDomValue repeat_delay = context.Evaluate(arglist[2], root, depends_on);

                if (repeat_delay.TryToDouble(out double rd))
                {
                    if (rd <= 0)
                        repeat_delay_ticks = initial_delay_ticks;
                    else
                        repeat_delay_ticks = (long)(rd * Stopwatch.Frequency / 1000);
                }
                else
                    repeat_delay_ticks = initial_delay_ticks;
            }
            else
                repeat_delay_ticks = initial_delay_ticks;

            return new UiDomRepeatAction(context, root, action_expr, initial_delay_ticks, repeat_delay_ticks);
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
                            inner_queue = new InputQueue();
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
                        {
                            inner_queue.Enqueue(state);
                        }
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
                        long remaining_delay = (is_initial ? InitialDelay : RepeatDelay) -
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
                        await Task.WhenAny(queue.WaitForInput(), watcher.WaitChanged(), Task.Delay(TimeSpan.FromSeconds(remaining_delay / (double)Stopwatch.Frequency)));
                        continue;
                    }
                    await Task.WhenAny(queue.WaitForInput(), watcher.WaitChanged());
                }
            }
        }

        public override string ToString()
        {
            return $"{Context}.(repeat_action({ActionExpression}, {(InitialDelay / (double)Stopwatch.Frequency * 1000).ToString(CultureInfo.InvariantCulture)}, {(RepeatDelay / (double)Stopwatch.Frequency * 1000).ToString(CultureInfo.InvariantCulture)}))";
        }
    }
}
