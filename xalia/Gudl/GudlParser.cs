using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace Xalia.Gudl
{
    static class GudlParser
    {
        public static TokenListParser<GudlToken, GudlExpression> IdentifierExpression =
            from id in Token.EqualTo(GudlToken.Identifier)
            select (GudlExpression)new IdentifierExpression(id.ToStringValue());

        public static TokenListParser<GudlToken, GudlExpression> ParenExpression =
            from _start in Token.EqualTo(GudlToken.LParen)
            from expr in Expression
            from _end in Token.EqualTo(GudlToken.RParen)
            select expr;

        public static TokenListParser<GudlToken, GudlExpression> StringExpression =
            from s in Token.EqualTo(GudlToken.String)
            select (GudlExpression)new StringExpression(GudlTokenizer.GudlString.Parse(s.ToStringValue()));

        public static TokenListParser<GudlToken, GudlExpression> IntegerExpression =
            from s in Token.EqualTo(GudlToken.Integer)
            select (GudlExpression)new IntegerExpression(int.Parse(s.ToStringValue()));

        public static TokenListParser<GudlToken, GudlExpression> DoubleExpression =
            from s in Token.EqualTo(GudlToken.Double)
            select (GudlExpression)new DoubleExpression(double.Parse(s.ToStringValue()));

        public static TokenListParser<GudlToken, GudlExpression> UnitExpression =
            ParenExpression
            .Or(IdentifierExpression)
            .Or(StringExpression)
            .Or(IntegerExpression)
            .Or(DoubleExpression)
            .Or(Parse.Ref(() => SignExpression))
            .Or(Parse.Ref(() => NotExpression))
            .Named("expression");

        public static TokenListParser<GudlToken, GudlExpression> NameExpression =
            IdentifierExpression
            .Or(StringExpression)
            .Named("property name");

        static TokenListParser<GudlToken, GudlExpression> BinaryExpression(TokenListParser<GudlToken,GudlExpression> operand, GudlToken op1, params GudlToken[] ops)
        {
            var op = Token.EqualTo(op1);
            foreach (var token in ops)
            {
                op = op.Or(Token.EqualTo(token));
            }

            var operation = op.Then(parsed_op => operand.Select(parsed_operand => (parsed_op, parsed_operand)));

            return operand.Then(left => operation.Many().Select(operations =>
            {
                GudlExpression result = left;

                foreach ((var parsed_op, var parsed_operation) in operations)
                {
                    result = new BinaryExpression(result, parsed_operation, parsed_op.Kind);
                }

                return result;
            }));
        }

        static TokenListParser<GudlToken, GudlExpression> UnaryExpression(TokenListParser<GudlToken, GudlExpression> operand, GudlToken op1, params GudlToken[] ops)
        {
            var op = Token.EqualTo(op1);
            foreach (var token in ops)
            {
                op = op.Or(Token.EqualTo(token));
            }

            return op.AtLeastOnce().Then(parsed_ops => operand.Select(parsed_operand =>
            {
                GudlExpression result = parsed_operand;

                for (int i = parsed_ops.Length-1; i >= 0; i--)
                {
                    result = new UnaryExpression(result, parsed_ops[i].Kind);
                }

                return result;
            }));
        }

        public static TokenListParser<GudlToken, (GudlToken, GudlExpression[])> DotOperation =
            from _token in Token.EqualTo(GudlToken.Dot)
            from expr in UnitExpression
            select (GudlToken.Dot, new GudlExpression[] { expr });

        public static TokenListParser<GudlToken, (GudlToken, GudlExpression[])> ArglistExpression =
            from _start in Token.EqualTo(GudlToken.LParen)
            from arglist in Expression.ManyDelimitedBy(Token.EqualTo(GudlToken.Comma))
            from _end in Token.EqualTo(GudlToken.RParen)
            select (GudlToken.LParen, arglist);

        public static TokenListParser<GudlToken, GudlExpression> ApplyExpression =
            UnitExpression.Then(dot =>
                DotOperation.Or(ArglistExpression).Many().Select(exprs =>
                {
                    GudlExpression result = dot;

                    foreach ((var token, var arglist) in exprs)
                    {
                        switch (token)
                        {
                            case GudlToken.Dot:
                                result = new BinaryExpression(result, arglist[0], GudlToken.Dot);
                                break;
                            case GudlToken.LParen:
                                result = new ApplyExpression(result, arglist);
                                break;
                        }
                    }

                    return result;
                }));

        public static TokenListParser<GudlToken, GudlExpression> SignExpression =
            UnaryExpression(ApplyExpression, GudlToken.Plus, GudlToken.Minus);

        public static TokenListParser<GudlToken, GudlExpression> ProductExpression =
            BinaryExpression(ApplyExpression, GudlToken.Mult, GudlToken.IDiv, GudlToken.Modulo, GudlToken.Div);

        public static TokenListParser<GudlToken, GudlExpression> SumExpression =
            BinaryExpression(ProductExpression, GudlToken.Plus, GudlToken.Minus);

        public static TokenListParser<GudlToken, GudlExpression> InequalityExpression =
            BinaryExpression(SumExpression, GudlToken.Equal, GudlToken.NotEqual,
                GudlToken.Lt, GudlToken.Gt, GudlToken.Lte, GudlToken.Gte);

        public static TokenListParser<GudlToken, GudlExpression> NotExpression =
            UnaryExpression(InequalityExpression, GudlToken.Not);

        public static TokenListParser<GudlToken, GudlExpression> AndExpression =
            BinaryExpression(InequalityExpression, GudlToken.And);

        public static TokenListParser<GudlToken, GudlExpression> OrExpression =
            BinaryExpression(AndExpression, GudlToken.Or);

        public static TokenListParser<GudlToken, GudlExpression> Expression = OrExpression.Named("expression");

        public static TokenListParser<GudlToken, GudlDeclaration> DeclarationStatement =
            from name in NameExpression.Named("declaration")
            from expr in Token.EqualTo(GudlToken.Colon).Message("expected ':', '(', or '{'").IgnoreThen(Expression)
            from _ in Token.EqualTo(GudlToken.Semicolon)
            select new GudlDeclaration(name, expr);

        public static TokenListParser<GudlToken, GudlStatement[]> DeclarationBlock =
            (from start in Token.EqualTo(GudlToken.LBrace)
            from statements in Parse.Ref(() => Statements)
            from end in Token.EqualTo(GudlToken.RBrace)
            select statements).Named("block");

        public static TokenListParser<GudlToken, GudlSelector> SelectorStatement =
            from kind in NameExpression
            from condition in ParenExpression.OptionalOrDefault()
            from block in DeclarationBlock
            select new GudlSelector(kind, condition, block);

        public static TokenListParser<GudlToken, GudlStatement> Statement =
            DeclarationStatement.Select(decl => (GudlStatement)decl).Try()
            .Or(SelectorStatement.Select(sel => (GudlStatement)sel))
            .Named("statement");

        public static TokenListParser<GudlToken, GudlStatement[]> Statements =
            Statement.Many();

        static string FormatParseError(string gudl, string filename, Position position, string message)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"  File {filename}, line {position.Line}");

            using (StringReader sr = new StringReader(gudl))
            {
                for (int i = 1; i < position.Line; i++)
                {
                    sr.ReadLine();
                }
                sb.AppendLine($"    {sr.ReadLine()}");
            }
            sb.AppendLine($"    {new string(' ', position.Column - 1)}^");
            sb.AppendLine(message);
            return sb.ToString();
        }

        public static bool TryParse(string gudl, string filename, out GudlStatement[] value, out string error)
        {
            var tokens = GudlTokenizer.Instance.TryTokenize(gudl);
            if (!tokens.HasValue)
            {
                value = null;
                error = FormatParseError(gudl, filename, tokens.ErrorPosition, tokens.ErrorMessage);
                return false;
            }

            var parsed = Statements.AtEnd().TryParse(tokens.Value);
            if (!parsed.HasValue)
            {
                value = null;
                error = FormatParseError(gudl, filename, parsed.ErrorPosition, parsed.ErrorMessage);
                return false;
            }

            value = parsed.Value;
            error = null;
            return true;
        }

        public static bool TryParse(string filename, out GudlStatement[] value, out string error)
        {
            string contents;
            using (var reader = new StreamReader(filename))
            {
                contents = reader.ReadToEnd();
            }
            return TryParse(contents, filename, out value, out error);
        }

        public static GudlExpression ParseExpression(string expr)
        {
            var tokens = GudlTokenizer.Instance.TryTokenize(expr);
            if (!tokens.HasValue)
                throw new ArgumentException();

            var parsed = Expression.AtEnd().TryParse(tokens.Value);
            if (!parsed.HasValue)
                throw new ArgumentException();

            return parsed.Value;
        }
    }
}
