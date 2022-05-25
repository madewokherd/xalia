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
        Pressed
    }

    public struct InputState
    {
        public InputStateKind Kind;

        public static InputState Combine(InputState a, InputState b)
        {
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
    }
}
