using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xalia.Input
{
    public enum InputStateKind
    {
        Disconnected,
        Released,
        Pressed,
        Repeat,
        Pulse, // Indicates input source that can only send an "activate" event, not track state
        AnalogJoystick, // XAxis and YAxis range from -32768 to 32767
        AnalogButton // XAxis ranges from 0 to 32767
    }

    public struct InputState
    {
        public InputStateKind Kind;
        public short XAxis;
        public short YAxis;

        public InputState(InputStateKind kind)
        {
            Kind = kind;
            XAxis = 0;
            YAxis = 0;
        }

        public static InputState Combine(InputState a, InputState b)
        {
            if (a.Kind == InputStateKind.Pulse)
                return a;
            if (b.Kind == InputStateKind.Pulse)
                return b;
            if (a.Kind == InputStateKind.Repeat)
                return a;
            if (b.Kind == InputStateKind.Repeat)
                return b;
            if (a.Kind == InputStateKind.Pressed)
                return a;
            if (b.Kind == InputStateKind.Pressed)
                return b;
            if (a.Kind == InputStateKind.AnalogJoystick && (b.Kind != InputStateKind.AnalogJoystick || b.Intensity < a.Intensity))
                return a;
            if (b.Kind == InputStateKind.AnalogJoystick)
                return b;
            if (a.Kind == InputStateKind.AnalogButton && (b.Kind != InputStateKind.AnalogButton || b.Intensity < a.Intensity))
                return a;
            if (b.Kind == InputStateKind.AnalogButton)
                return b;
            if (a.Kind == InputStateKind.Released)
                return a;
            if (b.Kind == InputStateKind.Released)
                return b;
            return a;
        }

        public override string ToString()
        {
            return $"InputState kind={Kind}";
        }

        public ushort Intensity
        {
            get
            {
                switch (Kind)
                {
                    case InputStateKind.Pressed:
                    case InputStateKind.Repeat:
                    case InputStateKind.Pulse:
                        return 32767;

                    case InputStateKind.Disconnected:
                    case InputStateKind.Released:
                        return 0;
                    case InputStateKind.AnalogJoystick:
                        return (ushort)Math.Min(Math.Sqrt((int)XAxis * XAxis + (int)YAxis * YAxis), 32767);
                    case InputStateKind.AnalogButton:
                        return (ushort)XAxis;
                }
                throw new NotImplementedException(); // Hopefully the compiler will warn about missed enum values?
            }
        }

        public bool Pressed
        {
            get
            {
                switch (Kind)
                {
                    case InputStateKind.Pressed:
                    case InputStateKind.Repeat:
                    case InputStateKind.Pulse:
                        return true;
                    case InputStateKind.Disconnected:
                    case InputStateKind.Released:
                        return false;
                    case InputStateKind.AnalogButton:
                    case InputStateKind.AnalogJoystick:
                        return Intensity >= 10000; // arbitrary cutoff
                }
                throw new NotImplementedException(); // Hopefully the compiler will warn about missed enum values?
            }
        }

        public bool JustPressed(InputState previous_state)
        {
            if (Kind == InputStateKind.Pulse)
                return true;
            if (previous_state.Kind == InputStateKind.Disconnected)
                // If a button was *already pressed* when first connected, it wasn't "just pressed".
                return false;
            if (Kind == InputStateKind.Repeat)
                return true;
            return Pressed && !previous_state.Pressed;
        }
    }
}
