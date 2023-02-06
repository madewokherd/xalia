using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Input;

namespace Xalia.UiDom
{
    internal class UiDomRadialDeadzone : UiDomRoutine
    {
        public UiDomRadialDeadzone(UiDomRoutine routine, double deadzone) :
            base("radial_deadzone", new UiDomValue[] { routine, new UiDomDouble(deadzone) })
        {
            Routine = routine;
            Deadzone = deadzone;
        }

        public UiDomRoutine Routine { get; }
        public double Deadzone { get; }

        internal static UiDomValue ApplyFn(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length < 2)
                return UiDomUndefined.Instance;

            var routine = context.Evaluate(arglist[0], root, depends_on) as UiDomRoutine;
            if (routine is null)
                return UiDomUndefined.Instance;

            if (!context.Evaluate(arglist[1], root, depends_on).TryToDouble(out var deadzone))
                return UiDomUndefined.Instance;

            return new UiDomRadialDeadzone(routine, deadzone);
        }

        private static double ToEdgeDistance(short axis)
        {
            if (axis > 0)
                return 1.0-(axis / 32767.0);
            else
                return -1.0-(axis / 32768.0);
        }

        private static short FromEdgeDistance(double axis)
        {
            if (axis > 0.0)
                return (short)Math.Round((1.0 - axis) * 32767.0);
            else
                return (short)Math.Round((-1.0 - axis) * 32768.0);
        }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            var inner_queue = new InputQueue();
            Utils.RunTask(Routine.ProcessInputQueue(inner_queue));
            InputState state = default;

            do
            {
                state = await queue.Dequeue();

                if (state.Kind is InputStateKind.AnalogJoystick)
                {
                    double xedge = ToEdgeDistance(state.XAxis);
                    double yedge = ToEdgeDistance(state.YAxis);
                    double edge_distance = Math.Min(Math.Abs(xedge), Math.Abs(yedge));
                    double intensity = 1.0 - edge_distance;

                    if (intensity <= Deadzone)
                    {
                        state.XAxis = 0;
                        state.YAxis = 0;
                    }
                    else
                    {
                        double new_edge_distance = edge_distance / (1.0 - Deadzone);
                        double multiplier = (1.0 - new_edge_distance) / (1.0 - edge_distance);
                        state.XAxis = (short)Math.Round(state.XAxis * multiplier);
                        state.YAxis = (short)Math.Round(state.YAxis * multiplier);
                    }
                }
                else if (state.Kind is InputStateKind.AnalogButton)
                {
                    double edge_distance = ToEdgeDistance(state.Intensity);
                    double intensity = 1.0 - edge_distance;

                    if (intensity <= Deadzone)
                    {
                        state.XAxis = 0;
                    }
                    else
                    {
                        state.XAxis = FromEdgeDistance(edge_distance / (1.0 - Deadzone));
                    }
                }
                inner_queue.Enqueue(state);
            } while (!(state.Kind is InputStateKind.Disconnected));
        }
    }
}
