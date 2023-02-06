using System;
using System.Threading.Tasks;

namespace Xalia.UiDom
{
    public class UiDomRoutineAsync : UiDomRoutinePress
    {
        public delegate Task AsyncRoutine(UiDomRoutineAsync obj);

        public AsyncRoutine Routine { get; }

        public UiDomRoutineAsync(UiDomElement element, string name, UiDomValue[] arglist,
            AsyncRoutine routine) : base(element, name, arglist)
        {
            Routine = routine;
        }

        public UiDomRoutineAsync(AsyncRoutine routine) : this(null, null, null, routine) { }
        public UiDomRoutineAsync(UiDomElement element, AsyncRoutine routine) : this(element, null, null, routine) { }
        public UiDomRoutineAsync(string name, AsyncRoutine routine) : this(null, name, null, routine) { }
        public UiDomRoutineAsync(UiDomElement element, string name, AsyncRoutine routine) : this(element, name, null, routine) { }
        public UiDomRoutineAsync(string name, UiDomValue[] arglist, AsyncRoutine routine) : this(null, name, arglist, routine) { }

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
