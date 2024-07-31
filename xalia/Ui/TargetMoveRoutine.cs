using System;
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

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            Main.TargetMoveRoutineStarted();
            try
            {
                InputState prev_state = new InputState(InputStateKind.Disconnected), state;
                do
                {
                    state = await queue.Dequeue();
                    if (state.JustPressed(prev_state))
                    {
                        if (state.Kind == InputStateKind.AnalogJoystick)
                            DoMove(state);
                        else if (prev_state.Kind == InputStateKind.AnalogJoystick)
                            DoMove(prev_state);
                    }
                    prev_state = state;
                } while (state.Kind != InputStateKind.Disconnected);
            }
            finally
            {
                Main.TargetMoveRoutineStopped();
            }
        }

        private void DoMove(InputState state)
        {
            if (Math.Abs((int)state.XAxis) > Math.Abs((int)state.YAxis))
            {
                if (state.XAxis > 0)
                    Main.TargetMove(UiMain.Direction.Right);
                else
                    Main.TargetMove(UiMain.Direction.Left);
            }
            else
            {
                if (state.YAxis > 0)
                    Main.TargetMove(UiMain.Direction.Down);
                else
                    Main.TargetMove(UiMain.Direction.Up);
            }
        }
    }
}
