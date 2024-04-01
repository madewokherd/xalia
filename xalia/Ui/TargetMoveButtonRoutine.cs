using System;
using System.Threading.Tasks;
using Xalia.Input;
using Xalia.UiDom;

namespace Xalia.Ui
{
    internal class TargetMoveButtonRoutine : UiDomRoutineSync
    {
        public TargetMoveButtonRoutine(UiMain main, string name, Action<UiDomRoutineSync> action)
            : base(name, action)
        {
            Main = main;
        }

        public UiMain Main { get; }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            Main.TargetMoveRoutineStarted();
            try
            {
                await base.ProcessInputQueue(queue);
            }
            finally
            {
                Main.TargetMoveRoutineStopped();
            }
        }
    }
}
