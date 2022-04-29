using System.Linq;

using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace Gazelle.Gudl
{
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
}
