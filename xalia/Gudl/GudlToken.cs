using Superpower.Display;
using Superpower.Parsers;

namespace Xalia.Gudl
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

        [Token(Example = "+")]
        Plus,

        [Token(Example = "-")]
        Minus,

        [Token(Example = "*")]
        Mult,

        [Token(Example = "~/")]
        IDiv,

        [Token(Example = "%")]
        Modulo,

        [Token(Example = "<")]
        Lt,

        [Token(Example = ">")]
        Gt,

        [Token(Example = "<=")]
        Lte,

        [Token(Example = ">=")]
        Gte,

        Identifier,

        String,

        Integer,
        Comma,
    }
}
