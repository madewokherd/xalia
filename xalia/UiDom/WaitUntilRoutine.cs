using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    public class WaitUntilRoutine : UiDomRoutinePress
    {
        public WaitUntilRoutine(UiDomValue context, GudlExpression expr, UiDomRoot root)
            : base("wait_until")
        {
            Context = context;
            Expression = expr;
            Root = root;
        }

        public UiDomValue Context { get; }
        public GudlExpression Expression { get; }
        public UiDomRoot Root { get; }

        public override string ToString()
        {
            return $"({Context.ToString()}).wait_until({Expression.ToString()})";
        }

        public override bool Equals(object obj)
        {
            if (obj is WaitUntilRoutine wait)
            {
                return Context == wait.Context && Expression == wait.Expression && Root == wait.Root;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (Context, Expression, Root).GetHashCode() ^ typeof(WaitUntilRoutine).GetHashCode();
        }

        public override async Task OnPress()
        {
            using (ExpressionWatcher watch = new ExpressionWatcher(Context, Root, Expression))
            {
                while (!watch.CurrentValue.ToBool())
                {
                    await watch.WaitChanged();
                }
            }
        }
    }
}
