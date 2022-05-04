using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Gazelle.Gudl;

namespace Gazelle.UiDom
{
    public abstract class UiDomValue
    {
        protected virtual UiDomValue EvaluateIdentifierCore(string id, UiDomRoot root,
            [In][Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            return UiDomUndefined.Instance;
        }

        public UiDomValue EvaluateIdentifier(string id, UiDomRoot root,
            [In][Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            try
            {
                return EvaluateIdentifierCore(id, root, depends_on);
            }
            catch (Exception e)
            {
                Console.WriteLine($"failed evaluation of identifier {id} on {this}");
                Console.WriteLine(e);
                return UiDomUndefined.Instance;
            }
        }

        protected virtual UiDomValue EvaluateDot(UiDomValue context, GudlExpression expr,
            UiDomRoot root, [In][Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            if (expr is StringExpression st)
            {
                return EvaluateIdentifier(st.Value, root, depends_on);
            }
            return Evaluate(expr, root, depends_on);
        }

        protected virtual UiDomValue EvaluateApply(UiDomValue context, GudlExpression expr,
            UiDomRoot root, [In][Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            UiDomValue right = context.Evaluate(expr, root, depends_on);
            if (right is UiDomString st)
            {
                return EvaluateIdentifier(st.Value, root, depends_on);
            }
            return UiDomUndefined.Instance;
        }

        public UiDomValue Evaluate(GudlExpression expr, UiDomRoot root,
            [In] [Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            if (expr is StringExpression st)
                return new UiDomString(st.Value);

            if (expr is IdentifierExpression id)
            {
                switch (id.Name)
                {
                    case "true":
                        return UiDomBoolean.True;
                    case "false":
                        return UiDomBoolean.False;
                    case "undefined":
                        return UiDomUndefined.Instance;
                    default:
                        return EvaluateIdentifier(id.Name, root, depends_on);
                }
            }

            if (expr is UnaryExpression un)
            {
                switch (un.Kind)
                {
                    case GudlToken.Not:
                        {
                            UiDomValue inner = Evaluate(un.Inner, root, depends_on);
                            return UiDomBoolean.FromBool(!inner.ToBool());
                        }
                }
            }

            if (expr is BinaryExpression bin)
            {
                switch (bin.Kind)
                {
                    case GudlToken.Dot:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            try
                            {
                                return left.EvaluateDot(this, bin.Right, root, depends_on);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"failed evaluation of dot expression {bin.Right} on {left} in {this}");
                                Console.WriteLine(e);
                                return UiDomUndefined.Instance;
                            }
                        }
                    case GudlToken.LParen:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            try
                            {
                                return left.EvaluateApply(this, bin.Right, root, depends_on);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"failed evaluation of apply expression {bin.Right} on {left} in {this}");
                                Console.WriteLine(e);
                                return UiDomUndefined.Instance;
                            }
                        }
                    case GudlToken.Equal:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);
                            return UiDomBoolean.FromBool(left.Equals(right));
                        }
                    case GudlToken.NotEqual:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);
                            return UiDomBoolean.FromBool(!left.Equals(right));
                        }
                    case GudlToken.And:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            if (!left.ToBool())
                            {
                                return left;
                            }
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);
                            return right;
                        }
                    case GudlToken.Or:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            if (left.ToBool())
                            {
                                return left;
                            }
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);
                            return right;
                        }
                }
            }

            // Shouldn't get here.
            return UiDomUndefined.Instance;
        }

        public virtual bool ToBool()
        {
            return true;
        }
    }
}
