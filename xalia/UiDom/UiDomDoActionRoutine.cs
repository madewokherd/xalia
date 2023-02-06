using System.Threading.Tasks;

using Xalia.Input;

namespace Xalia.UiDom
{
    internal class UiDomDoActionRoutine : UiDomRoutine
    {
        public UiDomDoActionRoutine(string action) : base("do_action", new UiDomValue[] { new UiDomString(action) })
        {
            Action = action;
        }

        public string Action { get; }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            using (var sink = InputSystem.Instance.CreateVirtualInput(Action))
            {
                while (true)
                {
                    var state = await queue.Dequeue();

                    if (state.Kind == InputStateKind.Disconnected)
                        break;

                    sink.SetInputState(state);
                }
            }
        }
    }
}
