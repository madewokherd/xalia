using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.Input;

namespace Xalia.UiDom
{
    public class UiDomRoutineAsync : UiDomRoutine
    {
        public delegate Task AsyncRoutine(UiDomRoutineAsync obj);

        public AsyncRoutine Routine { get; }

        public UiDomRoutineAsync(UiDomElement element, string name,
            AsyncRoutine routine) : base(element, name)
        {
            Routine = routine;
        }

        private async Task DoRoutine()
        {
            try
            {
                await Routine(this);
            }
            catch (Exception e)
            {
                Utils.OnError(e);
            }
        }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            InputState prev_state = new InputState(InputStateKind.Disconnected), state;
            do
            {
                state = await queue.Dequeue();
                if (state.JustPressed(prev_state))
                {
                    await DoRoutine();
                }
                prev_state = state;
            } while (state.Kind != InputStateKind.Disconnected);
        }
    }
}
