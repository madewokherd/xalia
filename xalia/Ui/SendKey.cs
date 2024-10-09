using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Xalia.Gudl;
using Xalia.Sdl;
using Xalia.UiDom;

namespace Xalia.Ui
{
    internal class SendKey : UiDomValue
    {
        public SendKey(WindowingSystem windowing)
        {
            Windowing = windowing;
        }

        public WindowingSystem Windowing { get; }

        public override string ToString()
        {
            return "send_key";
        }

        private UiDomRoutineAsync RoutineForKeyCode(int keycode, string name)
        {
            return new UiDomRoutineAsync(null, name, async (UiDomRoutineAsync rou) =>
            {
                await Windowing.SendKey(keycode);
            });
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
                    return RoutineForKeyCode((int)i.Value, $"send_key.{i.Value.ToString(CultureInfo.InvariantCulture)}");
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
                return RoutineForKeyCode(i, $"send_key.{i.ToString(CultureInfo.InvariantCulture)}");
            }
            return UiDomUndefined.Instance;
        }

        protected override UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            var keycode = Windowing.GetKeySym(id);

            if (keycode != 0)
            {
                return RoutineForKeyCode(keycode, $"send_key.{id.ToString(CultureInfo.InvariantCulture)}");
            }

            return base.EvaluateIdentifierCore(id, root, depends_on);
        }
    }
}
