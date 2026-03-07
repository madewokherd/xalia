using System.Threading.Tasks;
using Xalia.Input;
using Xalia.Sdl;
using Xalia.UiDom;

namespace Xalia.Ui
{
    internal class MapToKeyRoutine : UiDomRoutine
    {
        public MapToKeyRoutine(WindowingSystem windowing, int keycode, string name) :
            base(name)
        {
            Windowing = windowing;
            KeyCode = keycode;
        }

        public WindowingSystem Windowing { get; }
        public int KeyCode { get; }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            bool was_pressed = false;
            bool ever_released = false;
            InputState state;
            do
            {
                state = await queue.Dequeue();
                bool is_pressed = state.Pressed;
                if (!is_pressed)
                    ever_released = true;
                if (ever_released)
                {
                    if (is_pressed != was_pressed)
                        await Windowing.SendKey(KeyCode, is_pressed, was_pressed);
                    was_pressed = is_pressed;
                }
            } while (state.Kind != InputStateKind.Disconnected);
        }
    }
}
