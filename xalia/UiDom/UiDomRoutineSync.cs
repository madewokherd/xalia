using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.Input;

namespace Xalia.UiDom
{
    public class UiDomRoutineSync : UiDomRoutinePress
    {
        public Action<UiDomRoutineSync> Routine { get; }

        public UiDomRoutineSync(UiDomElement element, string name,
            Action<UiDomRoutineSync> routine) : base(element, name)
        {
            Routine = routine;
        }

        public override Task OnPress()
        {
            try
            {
                Routine(this);
            }
            catch (Exception e)
            {
                Utils.OnError(e);
            }
            return Task.CompletedTask;
        }
    }
}
