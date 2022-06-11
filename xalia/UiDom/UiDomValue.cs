using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Xalia.Gudl;

namespace Xalia.UiDom
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

            if (expr is IntegerExpression i)
                return new UiDomInt(i.Value);

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
                    case GudlToken.Plus:
                        {
                            UiDomValue inner = Evaluate(un.Inner, root, depends_on);
                            if (inner is UiDomInt)
                                return inner;
                            return UiDomUndefined.Instance;
                        }
                    case GudlToken.Minus:
                        {
                            UiDomValue inner = Evaluate(un.Inner, root, depends_on);
                            if (inner is UiDomInt integer)
                                return new UiDomInt(-integer.Value);
                            return UiDomUndefined.Instance;
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
                    case GudlToken.Plus:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);

                            if (left is UiDomInt lint && right is UiDomInt rint)
                                return new UiDomInt(lint.Value + rint.Value);
                            if (left is UiDomRoutine lrou && right is UiDomRoutine rrou)
                                return new UiDomRoutineSequence(lrou, rrou);
                            return UiDomUndefined.Instance;
                        }
                    case GudlToken.Minus:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);

                            if (left is UiDomInt lint && right is UiDomInt rint)
                                return new UiDomInt(lint.Value - rint.Value);
                            return UiDomUndefined.Instance;
                        }
                    case GudlToken.Mult:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);

                            if (left is UiDomInt lint && right is UiDomInt rint)
                                return new UiDomInt(lint.Value * rint.Value);
                            return UiDomUndefined.Instance;
                        }
                    case GudlToken.IDiv:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);

                            if (left is UiDomInt lint && right is UiDomInt rint)
                            {
                                if (rint.Value == 0)
                                    return UiDomUndefined.Instance;

                                int mod = lint.Value % rint.Value;
                                int quotient = lint.Value / rint.Value;


                                if (mod != 0 && ((mod > 0) != (rint.Value > 0)))
                                {
                                    quotient -= 1;
                                }

                                return new UiDomInt(quotient);
                            }
                            return UiDomUndefined.Instance;
                        }
                    case GudlToken.Modulo:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);

                            if (left is UiDomInt lint && right is UiDomInt rint)
                            {
                                if (rint.Value == 0)
                                    return UiDomUndefined.Instance;

                                int mod = lint.Value % rint.Value;

                                if (mod != 0 && ((mod > 0) != (rint.Value > 0)))
                                {
                                    mod = mod + rint.Value;
                                }

                                return new UiDomInt(mod);
                            }
                            return UiDomUndefined.Instance;
                        }
                    case GudlToken.Gt:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);

                            if (left.Compare(right, out int sign))
                            {
                                return UiDomBoolean.FromBool(sign > 0);
                            }
                            return UiDomUndefined.Instance;
                        }
                    case GudlToken.Lt:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);

                            if (left.Compare(right, out int sign))
                            {
                                return UiDomBoolean.FromBool(sign < 0);
                            }
                            return UiDomUndefined.Instance;
                        }
                    case GudlToken.Gte:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);

                            if (left.Compare(right, out int sign))
                            {
                                return UiDomBoolean.FromBool(sign >= 0);
                            }
                            return UiDomUndefined.Instance;
                        }
                    case GudlToken.Lte:
                        {
                            UiDomValue left = Evaluate(bin.Left, root, depends_on);
                            UiDomValue right = Evaluate(bin.Right, root, depends_on);

                            if (left.Compare(right, out int sign))
                            {
                                return UiDomBoolean.FromBool(sign <= 0);
                            }
                            return UiDomUndefined.Instance;
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

        public virtual bool Compare(UiDomValue other, out int sign)
        {
            sign = 0;
            return false;
        }
    }
}
