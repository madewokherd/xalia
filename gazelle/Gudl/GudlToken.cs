using Superpower.Display;
using Superpower.Parsers;

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

        [Token(Example = "+")]
        Plus,

        [Token(Example = "-")]
        Minus,

        Identifier,

        String,

        Integer,
    }
}
