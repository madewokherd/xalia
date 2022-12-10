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

        VirtualInputSink up_sink;
        VirtualInputSink down_sink;
        VirtualInputSink left_sink;
        VirtualInputSink right_sink;

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

        public override Task OnInput(InputSystem.ActionStateChangeEventArgs e)
        {
            InputState released = default;
            released.Kind = InputStateKind.Released;

            InputState pressed = default;
            pressed.Kind = InputStateKind.Pressed;

            if (e.State.Pressed)
            {
                if (up_sink is null)
                {
                    up_sink = InputSystem.Instance.CreateVirtualInput($"{DpadName}_up");
                    down_sink = InputSystem.Instance.CreateVirtualInput($"{DpadName}_down");
                    left_sink = InputSystem.Instance.CreateVirtualInput($"{DpadName}_left");
                    right_sink = InputSystem.Instance.CreateVirtualInput($"{DpadName}_right");

                    up_sink.SetInputState(released);
                    down_sink.SetInputState(released);
                    left_sink.SetInputState(released);
                    right_sink.SetInputState(released);
                }

                if (e.State.Kind == InputStateKind.AnalogJoystick)
                {
                    bool up_pressed = e.State.YAxis < -PRESSED_THRESHOLD;
                    bool down_pressed = e.State.YAxis > PRESSED_THRESHOLD;
                    bool left_pressed = e.State.XAxis < -PRESSED_THRESHOLD;
                    bool right_pressed = e.State.XAxis > PRESSED_THRESHOLD;

                    up_sink.SetInputState(up_pressed ? pressed : released);
                    down_sink.SetInputState(down_pressed ? pressed : released);
                    left_sink.SetInputState(left_pressed ? pressed : released);
                    right_sink.SetInputState(right_pressed ? pressed : released);
                }

                e.LockInput = true;
            }
            else if (!(up_sink is null))
            {
                // FIXME: if the input we get is released but not Disconnected, the dpad shouldn't be Disconnected either
                up_sink.Dispose();
                up_sink = null;
                down_sink.Dispose();
                down_sink = null;
                left_sink.Dispose();
                left_sink = null;
                right_sink.Dispose();
                right_sink = null;
            }

            return Task.CompletedTask;
        }
    }
}
