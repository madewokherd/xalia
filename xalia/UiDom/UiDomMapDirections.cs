using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Navigation;
using Xalia.Gudl;
using Xalia.Input;

namespace Xalia.UiDom
{
    internal class UiDomMapDirections : UiDomRoutine
    {
        public UiDomMapDirections(UiDomValue context, UiDomRoot root, GudlExpression[] action_expressions)
        {
            Context = context;
            Root = root;
            ActionExpressions = action_expressions;
        }

        public UiDomValue Context { get; }
        public UiDomRoot Root { get; }
        public GudlExpression[] ActionExpressions { get; }

        const int pixel_activation_threshold = 120;

        internal static UiDomValue ApplyFn(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 4)
                return UiDomUndefined.Instance;
            foreach (var expr in arglist)
            {
                // evaluate expression to create dependencies, so we don't have to wait for them later
                context.Evaluate(expr, root, depends_on);
            }
            return new UiDomMapDirections(context, root, arglist);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is UiDomMapDirections d)
            {
                if (!Context.Equals(d.Context) || Root != d.Root)
                    return false;
                for (int i=0; i<4; i++)
                {
                    if (ActionExpressions[i] != d.ActionExpressions[i])
                        return false;
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return typeof(UiDomMapDirections).GetHashCode() ^ (Context, Root,
                ActionExpressions[0], ActionExpressions[1], ActionExpressions[2], ActionExpressions[3]).GetHashCode();
        }

        public override string ToString()
        {
            return $"{Context}.(map_directions({ActionExpressions[0]}, {ActionExpressions[1]}, {ActionExpressions[2]}, {ActionExpressions[3]}))";
        }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            ExpressionWatcher[] watchers = new ExpressionWatcher[4];
            InputQueue[] inner_queues = new InputQueue[4];
            int delta_x_remainder = 0, delta_y_remainder = 0;

            try
            {
                for (int i = 0; i < 4; i++)
                    watchers[i] = new ExpressionWatcher(Context, Root, ActionExpressions[i]);

                UiDomRoutine[] actions = new UiDomRoutine[4];
                short[] current_intensities = new short[4] { -1, -1, -1, -1 }; // sentinal value to indicate "we didn't get directional input yet"
                while (true)
                {
                    if (!queue.IsEmpty)
                    {
                        var state = await queue.Dequeue();

                        if (state.Kind == InputStateKind.Disconnected)
                            break;

                        switch (state.Kind)
                        {
                            case InputStateKind.AnalogJoystick:
                                {
                                    short[] new_intensities = new short[4]
                                    {
                                        state.XAxis < 0 ? (short)-Math.Max(state.XAxis, (short)-32767) : (short)0,
                                        state.YAxis > 0 ? state.YAxis : (short)0,
                                        state.YAxis < 0 ? (short)-Math.Max(state.YAxis, (short)-32767) : (short)0,
                                        state.XAxis > 0 ? state.XAxis : (short)0
                                    };
                                    for (int i = 0; i < 4; i++)
                                    {
                                        if (new_intensities[i] != current_intensities[i] &&
                                            !(inner_queues[i] is null))
                                        {
                                            var inner_state = new InputState(InputStateKind.AnalogButton);
                                            inner_state.XAxis = new_intensities[i];
                                            inner_queues[i].Enqueue(inner_state);
                                        }
                                    }
                                    current_intensities = new_intensities;
                                    break;
                                }
                            case InputStateKind.PixelDelta:
                                delta_x_remainder += state.XAxis;
                                delta_y_remainder += state.YAxis;

                                while (delta_x_remainder <= -pixel_activation_threshold)
                                {
                                    delta_x_remainder += pixel_activation_threshold;
                                    if (!(inner_queues[0] is null))
                                        inner_queues[0].Enqueue(new InputState(InputStateKind.Pulse));
                                }

                                while (delta_y_remainder >= pixel_activation_threshold)
                                {
                                    delta_y_remainder -= pixel_activation_threshold;
                                    if (!(inner_queues[1] is null))
                                        inner_queues[1].Enqueue(new InputState(InputStateKind.Pulse));
                                }

                                while (delta_y_remainder <= -pixel_activation_threshold)
                                {
                                    delta_y_remainder += pixel_activation_threshold;
                                    if (!(inner_queues[2] is null))
                                        inner_queues[2].Enqueue(new InputState(InputStateKind.Pulse));
                                }

                                while (delta_x_remainder >= pixel_activation_threshold)
                                {
                                    delta_x_remainder -= pixel_activation_threshold;
                                    if (!(inner_queues[3] is null))
                                        inner_queues[3].Enqueue(new InputState(InputStateKind.Pulse));
                                }
                                break;
                            case InputStateKind.Repeat:
                            case InputStateKind.Pulse:
                                {
                                    for (int i = 0; i < 4; i++)
                                    {
                                        if (current_intensities[i] >= 10000 &&
                                            !(inner_queues[i] is null))
                                        {
                                            inner_queues[i].Enqueue(state);
                                            var inner_state = new InputState(InputStateKind.AnalogButton);
                                            inner_state.XAxis = current_intensities[i];
                                            inner_queues[i].Enqueue(inner_state);
                                        }
                                    }
                                    break;
                                }
                        }

                        continue;
                    }
                    if (current_intensities[0] >= 0) // don't call inner routines until we have some input
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var new_action = watchers[i].CurrentValue as UiDomRoutine;
                            if (new_action is null)
                            {
                                if (!(actions[i] is null))
                                {
                                    inner_queues[i].Enqueue(new InputState(InputStateKind.Disconnected));
                                    inner_queues[i] = null;
                                    actions[i] = null;
                                }
                            }
                            else if (!new_action.Equals(actions[i]))
                            {
                                if (!(actions[i] is null))
                                {
                                    inner_queues[i].Enqueue(new InputState(InputStateKind.Disconnected));
                                    inner_queues[i] = null;
                                }
                                actions[i] = new_action;
                                inner_queues[i] = new InputQueue();
                                var inner_state = new InputState(InputStateKind.AnalogButton);
                                inner_state.XAxis = current_intensities[i];
                                inner_queues[i].Enqueue(inner_state);
                                Utils.RunTask(new_action.ProcessInputQueue(inner_queues[i]));
                            }
                        }
                    }
                    await Task.WhenAny(queue.WaitForInput(), watchers[0].WaitChanged(), watchers[1].WaitChanged(),
                        watchers[2].WaitChanged(), watchers[3].WaitChanged());
                }
            }
            finally
            {
                foreach (var watcher in watchers)
                    if (!(watcher is null))
                        watcher.Dispose();
                foreach (var inner_queue in inner_queues)
                    if (!(inner_queue is null))
                    {
                        inner_queue.Enqueue(new InputState(InputStateKind.Disconnected));
                    }
            }
        }
    }
}