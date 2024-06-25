using System.Threading.Tasks;
using Xalia.Input;

namespace Xalia.UiDom
{
    internal class UiDomOnRelease : UiDomRoutine
    {
        public UiDomOnRelease(UiDomRoutine routine) : base("on_release", new UiDomValue[] { routine })
        {
            Routine = routine;
        }

        public UiDomRoutine Routine { get; }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            InputState prev_state = new InputState(InputStateKind.Disconnected), state;
            bool held = false;
            do
            {
                state = await queue.Dequeue();
                if (state.JustPressed(prev_state))
                {
                    held = true;
                }
                else if (held && !state.Pressed && state.Kind != InputStateKind.Disconnected)
                {
                    held = false;
                    Routine.Pulse();
                }
                prev_state = state;
            } while (state.Kind != InputStateKind.Disconnected);
        }
    }
}