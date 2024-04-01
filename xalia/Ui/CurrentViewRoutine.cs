using System.Threading.Tasks;
using Xalia.Input;
using Xalia.UiDom;

namespace Xalia.Ui
{
    internal class CurrentViewRoutine : UiDomRoutine
    {
        public CurrentViewRoutine(UiMain main, UiDomRoutine routine) :
            base("wrap_current_view_action", new UiDomValue[] { routine })
        {
            Main = main;
            Routine = routine;
        }

        public UiMain Main { get; }
        public UiDomRoutine Routine { get; }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            Main.CurrentViewRoutineStarted();
            try
            {
                await Routine.ProcessInputQueue(queue);
            }
            finally
            {
                Main.CurrentViewRoutineStopped();
            }
        }
    }
}
