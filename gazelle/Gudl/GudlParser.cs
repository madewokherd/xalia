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
    enum GudlToken
    {
        [Token(Example="(")]
        LParen,

        [Token(Example=")")]
        RParen,

        [Token(Example=".")]
        Dot,

        [Token(Example="{")]
        LBrace,

        [Token(Example="}")]
        RBrace,

        [Token(Example=":")]
        Colon,

        [Token(Example=";")]
        Semicolon,

        [Token(Example="==")]
        Equal,

        [Token(Example="!=")]
        NotEqual,

        Identifier,

        String,
    }

    static class GudlTokenizer
    {
        // QuotedString.CStyle doesn't seem to allow \\, not sure why
        public static TextParser<Unit> GudlString =
            from open in Character.EqualTo('"')
            from content in Character.ExceptIn('\\', '"', '\r', '\n')
                .Or(Character.EqualTo('\\').IgnoreThen(Character.ExceptIn('\r', '\n'))).IgnoreMany()
            from close in Character.EqualTo('"')
            select Unit.Value;

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
                .Match(Identifier.CStyle, GudlToken.Identifier, requireDelimiters: true)
                .Match(GudlString, GudlToken.String, requireDelimiters: true)
                .Build();
    }

    static class GudlParser
    {
        public static bool TryParse(string gudl, string filename, out object value, out string error)
        {
            var tokens = GudlTokenizer.Instance.TryTokenize(gudl);
            if (!tokens.HasValue)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine($"  File {filename}, line {tokens.ErrorPosition.Line}");

                using (StringReader sr = new StringReader(gudl))
                {
                    for (int i=1; i<tokens.ErrorPosition.Line; i++)
                    {
                        sr.ReadLine();
                    }
                    sb.AppendLine($"    {sr.ReadLine()}");
                }
                sb.AppendLine($"    {new string(' ', tokens.ErrorPosition.Column-1)}^");
                sb.AppendLine(tokens.ErrorMessage);

                value = null;
                error = sb.ToString();
                return false;
            }

            value = tokens.Value;
            error = null;
            return true;
        }

        public static bool TryParse(string filename, out object value, out string error)
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
