using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xalia.Input;

namespace Xalia.UiDom
{
    public class UiDomRoutineAsync : UiDomRoutinePress
    {
        public delegate Task AsyncRoutine(UiDomRoutineAsync obj);

        public AsyncRoutine Routine { get; }

        public UiDomRoutineAsync(UiDomElement element, string name,
            AsyncRoutine routine) : base(element, name)
        {
            Routine = routine;
        }

        public override async Task OnPress()
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
    }
}
