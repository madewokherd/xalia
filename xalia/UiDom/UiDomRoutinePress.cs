using System.Threading.Tasks;
using Xalia.Input;

namespace Xalia.UiDom
{
    public abstract class UiDomRoutinePress : UiDomRoutine
    {
        public UiDomRoutinePress() : base() { }
        public UiDomRoutinePress(UiDomElement element) : base(element) { }
        public UiDomRoutinePress(string name) : base(name) { }
        public UiDomRoutinePress(UiDomElement element, string name) : base(element, name) { }
        public UiDomRoutinePress(string name, UiDomValue[] arglist) : base(name, arglist) { }
        public UiDomRoutinePress(UiDomElement element, string name, UiDomValue[] arglist) : base(element, name, arglist) { }

        public abstract Task OnPress();

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            InputState prev_state = new InputState(InputStateKind.Disconnected), state;
            do
            {
                state = await queue.Dequeue();
                if (state.JustPressed(prev_state))
                {
                    await OnPress();
                }
                prev_state = state;
            } while (state.Kind != InputStateKind.Disconnected);
        }
    }
}
