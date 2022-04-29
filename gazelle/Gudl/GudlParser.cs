using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Superpower;
using Superpower.Display;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace Gazelle.Gudl
{
    public enum GudlToken
    {
        [Token(Example = "(")]
        LParen,

        [Token(Example = ")")]
        RParen,

        [Token(Example = ".")]
        Dot,

        [Token(Example = "{")]
        LBrace,

        [Token(Example = "}")]
        RBrace,

        [Token(Example = ":")]
        Colon,

        [Token(Example = ";")]
        Semicolon,

        [Token(Example = "==")]
        Equal,

        [Token(Example = "!=")]
        NotEqual,

        [Token(Example = "not")]
        Not,

        [Token(Example = "and")]
        And,

        [Token(Example = "or")]
        Or,

        [Token(Example = "else")]
        Else,

        Identifier,

        String,
    }

    static class GudlTokenizer
    {
        static char HexToChar(char ch)
        {
            if (ch >= '0' && ch <= '9')
                return (char)(ch - '0');
            if (ch >= 'a' && ch <= 'f')
                return (char)(ch - 'a' + 10);
            //else (ch >= 'A' && ch <= 'F')
            return (char)(ch - 'A' + 10);
        }

        public static TextParser<char> HexChar(int maxdigits)
        {
            var singlechar = Character.HexDigit.Select(HexToChar);

            var result = singlechar;

            for (int i = 1; i < maxdigits; i++)
            {
                result = result.Then(leading =>
                    singlechar.Select(end => (char)(leading * 16 + end)).OptionalOrDefault(leading));
            }

            return result;
        }

        // QuotedString.CStyle doesn't seem to allow \\, not sure why
        public static TextParser<string> GudlString =
            from open in Character.EqualTo('"')
            from content in Character.ExceptIn('\\', '"', '\r', '\n')
                .Or(Span.EqualTo("\\u").Try().IgnoreThen(HexChar(4)))
                .Or(Span.EqualTo("\\x").Try().IgnoreThen(HexChar(2)))
                .Or(Span.EqualTo("\\0").Try().Value('\0'))
                .Or(Span.EqualTo("\\a").Try().Value('\a'))
                .Or(Span.EqualTo("\\b").Try().Value('\b'))
                .Or(Span.EqualTo("\\f").Try().Value('\f'))
                .Or(Span.EqualTo("\\n").Try().Value('\n'))
                .Or(Span.EqualTo("\\r").Try().Value('\r'))
                .Or(Span.EqualTo("\\t").Try().Value('\t'))
                .Or(Span.EqualTo("\\v").Try().Value('\v'))
                .Or(Character.EqualTo('\\').IgnoreThen(Character.ExceptIn('\r', '\n'))).Many()
            from close in Character.EqualTo('"')
            select new string(content);

        public static Tokenizer<GudlToken> Instance =
            new TokenizerBuilder<GudlToken>()
                .Ignore(Span.WhiteSpace)
                .Ignore(Comment.CStyle)
                .Ignore(Comment.CPlusPlusStyle)
                .Match(Character.EqualTo('('), GudlToken.LParen)
                .Match(Character.EqualTo(')'), GudlToken.RParen)
                .Match(Character.EqualTo('.'), GudlToken.Dot)
                .Match(Character.EqualTo('{'), GudlToken.LBrace)
                .Match(Character.EqualTo('}'), GudlToken.RBrace)
                .Match(Character.EqualTo(':'), GudlToken.Colon)
                .Match(Character.EqualTo(';'), GudlToken.Semicolon)
                .Match(Span.EqualTo("=="), GudlToken.Equal)
                .Match(Character.EqualTo('='), GudlToken.Equal)
                .Match(Span.EqualTo("!="), GudlToken.NotEqual)
                .Match(Span.EqualTo("not"), GudlToken.Not, requireDelimiters: true)
                .Match(Span.EqualTo("and"), GudlToken.And, requireDelimiters: true)
                .Match(Span.EqualTo("or"), GudlToken.Or, requireDelimiters: true)
                .Match(Span.EqualTo("else"), GudlToken.Else, requireDelimiters: true)
                .Match(Identifier.CStyle, GudlToken.Identifier, requireDelimiters: true)
                .Match(GudlString, GudlToken.String, requireDelimiters: true)
                .Build();
    }

    public abstract class GudlExpression
    {
    }

    public class IdentifierExpression : GudlExpression
    {
        public IdentifierExpression(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
    }

    public class StringExpression : GudlExpression
    {
        public StringExpression(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }

    public class UnaryExpression : GudlExpression
    {
        public UnaryExpression(GudlExpression inner, GudlToken op)
        {
            Inner = inner;
            Kind = op;
        }

        public GudlExpression Inner { get; }
        public GudlToken Kind { get; }
    }
    public class BinaryExpression : GudlExpression
    {
        public BinaryExpression(GudlExpression left, GudlExpression right, GudlToken op)
        {
            Left = left;
            Right = right;
            Kind = op;
        }

        public GudlExpression Left { get; }
        public GudlExpression Right { get; }
        public GudlToken Kind { get; }
    }

    public abstract class GudlStatement
    {
    }

    public class GudlDeclaration : GudlStatement
    {
        public GudlDeclaration(GudlExpression property, GudlExpression value)
        {
            if (property is IdentifierExpression id)
                Property = id.Name;
            else if (property is StringExpression st)
                Property = st.Value;
            else
                throw new ArgumentException("property must be an IdentifierExpression or StringExpression");
            Value = value;
        }

        public string Property;
        public GudlExpression Value;
    }

    public class GudlSelector : GudlStatement
    {
        public GudlSelector(GudlExpression kind, GudlExpression condition, GudlStatement[] statements, GudlSelector @else)
        {
            if (kind is IdentifierExpression id)
                Kind = id.Name;
            else if (kind is StringExpression st)
                Kind = st.Value;
            else
                throw new ArgumentException("kind must be an IdentifierExpression or StringExpression");
            Condition = condition;
            Statements = statements;
            Else = @else;
        }

        public string Kind;
        public GudlExpression Condition;
        public GudlStatement[] Statements;
        public GudlSelector Else;
    }

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

        public static TokenListParser<GudlToken, GudlExpression> UnitExpression =
            ParenExpression
            .Or(IdentifierExpression)
            .Or(StringExpression)
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

        public static TokenListParser<GudlToken, GudlExpression> DotExpression =
            BinaryExpression(UnitExpression, GudlToken.Dot);

        public static TokenListParser<GudlToken, GudlExpression> CallExpression =
            DotExpression.Then(dot =>
                ParenExpression.Many().Select(exprs =>
                {
                    GudlExpression result = dot;

                    foreach (var expr in exprs)
                    {
                        result = new BinaryExpression(result, expr, GudlToken.LParen);
                    }

                    return result;
                }));

        public static TokenListParser<GudlToken, GudlExpression> InequalityExpression =
            BinaryExpression(CallExpression, GudlToken.Equal, GudlToken.NotEqual);

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

        public static TokenListParser<GudlToken, GudlSelector> ElseBlock =
            Token.EqualTo(GudlToken.Else).IgnoreThen(Parse.Ref(() => SelectorStatement).
                Or(DeclarationBlock.Select(block => new GudlSelector(new IdentifierExpression("else"), null, block, null))));

        public static TokenListParser<GudlToken, GudlSelector> SelectorStatement =
            from kind in NameExpression
            from condition in ParenExpression.OptionalOrDefault()
            from block in DeclarationBlock
            from elseblock in ElseBlock.OptionalOrDefault()
            select new GudlSelector(kind, condition, block, elseblock);

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
    }
}
