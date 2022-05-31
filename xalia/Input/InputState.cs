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
        Repeat
    }

    public struct InputState
    {
        public InputStateKind Kind;

        public static InputState Combine(InputState a, InputState b)
        {
            if (a.Kind == InputStateKind.Repeat)
                return a;
            if (b.Kind == InputStateKind.Repeat)
                return b;
            if (a.Kind == InputStateKind.Pressed)
                return a;
            if (b.Kind == InputStateKind.Pressed)
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

        public bool Pressed
        {
            get
            {
                switch (Kind)
                {
                    case InputStateKind.Pressed:
                    case InputStateKind.Repeat:
                        return true;
                    case InputStateKind.Disconnected:
                    case InputStateKind.Released:
                        return false;
                }
                throw new NotImplementedException(); // Hopefully the compiler will warn about missed enum values?
            }
        }
    }
}
