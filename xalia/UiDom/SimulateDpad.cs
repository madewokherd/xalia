using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Input;

namespace Xalia.UiDom
{
    internal class SimulateDpad : UiDomRoutine
    {
        public string DpadName { get; }

        const int PRESSED_THRESHOLD = 10000;

        public SimulateDpad(string name) : base($"simulate_dpad.{name}")
        {
            DpadName = name;
        }

        public SimulateDpad() : this("dpad") { }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            return new SimulateDpad(id);
        }

        public override async Task ProcessInputQueue(InputQueue queue)
        {
            var up_sink = InputSystem.Instance.CreateVirtualInput($"{DpadName}_up");
            var down_sink = InputSystem.Instance.CreateVirtualInput($"{DpadName}_down");
            var left_sink = InputSystem.Instance.CreateVirtualInput($"{DpadName}_left");
            var right_sink = InputSystem.Instance.CreateVirtualInput($"{DpadName}_right");

            bool up = false, down = false, left = false, right = false;

            InputState released = new InputState(InputStateKind.Released);
            InputState pressed = new InputState(InputStateKind.Pressed);

            InputState state;
            do
            {
                state = await queue.Dequeue();

                if (state.Kind == InputStateKind.AnalogJoystick)
                {
                    bool up_pressed = state.YAxis < -PRESSED_THRESHOLD;
                    bool down_pressed = state.YAxis > PRESSED_THRESHOLD;
                    bool left_pressed = state.XAxis < -PRESSED_THRESHOLD;
                    bool right_pressed = state.XAxis > PRESSED_THRESHOLD;

                    up_sink.SetInputState(up_pressed ? pressed : released);
                    down_sink.SetInputState(down_pressed ? pressed : released);
                    left_sink.SetInputState(left_pressed ? pressed : released);
                    right_sink.SetInputState(right_pressed ? pressed : released);

                    up = up_pressed;
                    down = down_pressed;
                    left = left_pressed;
                    right = right_pressed;
                }
                else if (state.Kind == InputStateKind.Pulse ||
                    state.Kind == InputStateKind.Repeat)
                {
                    if (up)
                    {
                        up_sink.SetInputState(state);
                        up_sink.SetInputState(pressed);
                    }
                    if (down)
                    {
                        down_sink.SetInputState(state);
                        down_sink.SetInputState(pressed);
                    }
                    if (left)
                    {
                        left_sink.SetInputState(state);
                        left_sink.SetInputState(pressed);
                    }
                    if (right)
                    {
                        right_sink.SetInputState(state);
                        right_sink.SetInputState(pressed);
                    }
                }
            } while (state.Kind != InputStateKind.Disconnected);

            up_sink.Dispose();
            down_sink.Dispose();
            left_sink.Dispose();
            right_sink.Dispose();
        }
    }
}
