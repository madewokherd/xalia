using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Xalia.Gudl;
using Xalia.Sdl;
using Xalia.UiDom;

namespace Xalia.Ui
{
    internal class MapToKey : UiDomValue
    {
        public MapToKey(WindowingSystem windowing)
        {
            Windowing = windowing;
        }

        public WindowingSystem Windowing { get; }

        public override string ToString()
        {
            return "map_to_key";
        }

        private UiDomRoutine RoutineForKeyCode(int keycode, string name)
        {
            return new MapToKeyRoutine(Windowing, keycode, name);
        }

        protected override UiDomValue EvaluateDot(UiDomValue context, GudlExpression expr,
            UiDomRoot root, [In][Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (expr is StringExpression st)
            {
                return EvaluateIdentifier(st.Value, root, depends_on);
            }
            if (expr is IntegerExpression i)
            {
                try
                {
                    return RoutineForKeyCode((int)i.Value, $"map_to_key.{i.Value.ToString(CultureInfo.InvariantCulture)}");
                }
                catch (OverflowException)
                {
                    return UiDomUndefined.Instance;
                }
            }
            return Evaluate(expr, root, depends_on);
        }

        protected override UiDomValue EvaluateApply(UiDomValue context, GudlExpression[] arglist,
            UiDomRoot root, [In][Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length != 1)
                return UiDomUndefined.Instance;
            var expr = arglist[0];
            UiDomValue right = context.Evaluate(expr, root, depends_on);
            if (right is UiDomString st)
            {
                return EvaluateIdentifier(st.Value, root, depends_on);
            }
            if (right.TryToInt(out int i))
            {
                return RoutineForKeyCode(i, $"map_to_key.{i.ToString(CultureInfo.InvariantCulture)}");
            }
            return UiDomUndefined.Instance;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            var keycode = Windowing.GetKeySym(id);

            if (keycode != 0)
            {
                return RoutineForKeyCode(keycode, $"map_to_key.{id.ToString(CultureInfo.InvariantCulture)}");
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
