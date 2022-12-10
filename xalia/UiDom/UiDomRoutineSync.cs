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

        public override Task OnInput(InputSystem.ActionStateChangeEventArgs e)
        {
            if (e.JustPressed)
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
            return Task.CompletedTask;
        }
    }
}
