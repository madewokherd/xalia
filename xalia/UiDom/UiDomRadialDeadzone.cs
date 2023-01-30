using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
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

        private static double NormalizeAxis(short axis)
        {
            if (axis > 0)
                return axis / 32767.0;
            else
                return axis / 32768.0;
        }

        private static short ToAxisValue(double normalized)
        {
            if (normalized > 0)
                return (short)Math.Round(normalized * 32767.0);
            else
                return (short)Math.Round(normalized * 32768.0);
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
                    double x = NormalizeAxis(state.XAxis);
                    double y = NormalizeAxis(state.YAxis);
                    double intensity = Math.Sqrt((x * x) + (y * y));

                    if (intensity <= Deadzone)
                    {
                        state.XAxis = 0;
                        state.YAxis = 0;
                    }
                    else
                    {
                        double multiplier = (intensity - Deadzone) / (1.0 - Deadzone) / intensity;
                        state.XAxis = ToAxisValue(x * multiplier);
                        state.YAxis = ToAxisValue(y * multiplier);
                    }
                }
                else if (state.Kind is InputStateKind.AnalogButton)
                {
                    double intensity = NormalizeAxis(state.XAxis);

                    if (intensity <= Deadzone)
                    {
                        state.XAxis = 0;
                    }
                    else
                    {
                        state.XAxis = ToAxisValue((intensity - Deadzone) / (1.0 - Deadzone));
                    }
                }
                inner_queue.Enqueue(state);
            } while (!(state.Kind is InputStateKind.Disconnected));
        }
    }
}
