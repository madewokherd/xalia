using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.Input;
using Xalia.UiDom;

namespace Xalia.Ui
{
    internal class TargetMoveRoutine : UiDomRoutine
    {
        public TargetMoveRoutine(UiMain main) : base("target_move")
        {
            Main = main;
        }

        public UiMain Main { get; }

        public override void OnInput(InputSystem.ActionStateChangeEventArgs e)
        {
            base.OnInput(e);

            if (e.JustPressed)
            {
                InputState analog_state;

                if (e.State.Kind == InputStateKind.AnalogJoystick)
                    analog_state = e.State;
                else if (e.PreviousState.Kind == InputStateKind.AnalogJoystick)
                    analog_state = e.PreviousState;
                else
                    return;

                if (Math.Abs((int)analog_state.XAxis) > Math.Abs((int)analog_state.YAxis))
                {
                    double bias = analog_state.YAxis / Math.Abs((double)analog_state.XAxis);
                    if (analog_state.XAxis > 0)
                        Main.TargetMove(UiMain.Direction.Right, bias);
                    else
                        Main.TargetMove(UiMain.Direction.Left, bias);
                }
                else
                {
                    double bias = analog_state.XAxis / Math.Abs((double)analog_state.YAxis);
                    if (analog_state.YAxis > 0)
                        Main.TargetMove(UiMain.Direction.Down, bias);
                    else
                        Main.TargetMove(UiMain.Direction.Up, bias);
                }
            }
        }
    }
}
