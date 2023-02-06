using System;

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
        AnalogButton, // XAxis ranges from 0 to 32767
        PixelDelta, // Mouse movement
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
            if (a.Kind == InputStateKind.PixelDelta && a.Intensity > 0)
                return a;
            if (b.Kind == InputStateKind.PixelDelta && b.Intensity > 0)
                return b;
            if (a.Kind == InputStateKind.Pressed)
                return a;
            if (b.Kind == InputStateKind.Pressed)
                return b;
            if (a.IsAnalog && (!b.IsAnalog || b.Intensity < a.Intensity))
                return a;
            if (b.IsAnalog)
                return b;
            if (a.Kind == InputStateKind.Released)
                return a;
            if (b.Kind == InputStateKind.Released)
                return b;
            return a; // Disconnected
        }

        public override string ToString()
        {
            string extra;
            switch (Kind)
            {
                case InputStateKind.AnalogJoystick:
                    extra = $" xaxis={XAxis}, yaxis={YAxis}";
                    break;
                case InputStateKind.AnalogButton:
                    extra = $" intensity={XAxis}";
                    break;
                case InputStateKind.PixelDelta:
                    extra = $" dx={XAxis} dy={YAxis}";
                    break;
                default:
                    extra = "";
                    break;
            }
            return $"InputState kind={Kind}{extra}";
        }

        public short Intensity
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
                    case InputStateKind.PixelDelta:
                        return (short)Math.Min(Math.Max(Math.Abs((int)XAxis), Math.Abs((int)YAxis)), 32767);
                    case InputStateKind.AnalogButton:
                        return XAxis;
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
                    case InputStateKind.PixelDelta:
                        return Intensity > 0;
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
            if (Kind == InputStateKind.PixelDelta)
                return false; // translating this to button presses requires more than just 1 snapshot
            if (Kind == InputStateKind.Repeat)
                return true;
            return Pressed && !previous_state.Pressed;
        }

        public bool IsAnalog
        {
            get
            {
                switch (Kind)
                {
                    case InputStateKind.AnalogJoystick:
                    case InputStateKind.AnalogButton:
                    case InputStateKind.PixelDelta:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}
