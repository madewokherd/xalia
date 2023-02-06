using System;
using System.Threading.Tasks;

namespace Xalia.UiDom
{
    public class UiDomRoutineSync : UiDomRoutinePress
    {
        public Action<UiDomRoutineSync> Routine { get; }

        public UiDomRoutineSync(UiDomElement element, string name, UiDomValue[] arglist,
            Action<UiDomRoutineSync> routine) : base(element, name, arglist)
        {
            Routine = routine;
        }

        public UiDomRoutineSync(Action<UiDomRoutineSync> routine) : this(null, null, null, routine) { }
        public UiDomRoutineSync(UiDomElement element, Action<UiDomRoutineSync> routine) : this(element, null, null, routine) { }
        public UiDomRoutineSync(string name, Action<UiDomRoutineSync> routine) : this(null, name, null, routine) { }
        public UiDomRoutineSync(UiDomElement element, string name, Action<UiDomRoutineSync> routine) : this(element, name, null, routine) { }
        public UiDomRoutineSync(string name, UiDomValue[] arglist, Action<UiDomRoutineSync> routine) : this(null, name, arglist, routine) { }

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
