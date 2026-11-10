namespace MyExcelMAUIApp.Logic;

public enum TokenType
{
    Number, CellReference, Function,
    Plus, Minus, Star, Slash,
    LeftParen, RightParen, Comma,
    Equal, Less, Greater, LessEqual, GreaterEqual, NotEqual,
    End
}

public sealed class Token
{
    public Token(TokenType type, string text, int position)
    {
        Type = type;
        Text = text;
        Position = position;
    }

    public TokenType Type { get; }
    public string Text { get; }
    public int Position { get; }
}
