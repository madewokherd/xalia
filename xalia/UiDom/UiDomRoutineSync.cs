using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.Input;

namespace Xalia.UiDom
{
    public class UiDomRoutineSync : UiDomRoutine
    {
        public Action<UiDomRoutineSync> Routine { get; }

        public UiDomRoutineSync(UiDomElement element, string name,
            Action<UiDomRoutineSync> routine) : base(element, name)
        {
            Routine = routine;
        }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            InputState prev_state = new InputState(InputStateKind.Disconnected), state;
            do
            {
                state = await queue.Dequeue();
                if (state.JustPressed(prev_state))
                {
                    try
                    {
                        Routine(this);
                    }
                    catch (Exception exc)
                    {
                        Utils.OnError(exc);
                    }
                }
                prev_state = state;
            } while (state.Kind != InputStateKind.Disconnected);
        }
    }
}
