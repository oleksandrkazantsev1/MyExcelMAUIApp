using System.Globalization;
using System.Numerics;

namespace MyExcelMAUIApp.Logic;

/// <summary>
/// Синтаксичний аналіз (рекурсивний спуск). Граматика (БНФ):
///   вираз     ::= додавання [ порівняння додавання ]
///   порівняння::= "=" | "&lt;" | "&gt;" | "&lt;=" | "&gt;=" | "&lt;&gt;"
///   додавання ::= доданок { ("+" | "-") доданок }
///   доданок   ::= унарний { ("*" | "/") унарний }
///   унарний   ::= ("+" | "-") унарний | первинний
///   первинний ::= ЧИСЛО | КЛІТИНКА | функція | "(" вираз ")"
///   функція   ::= ("mmax" | "mmin") "(" вираз { "," вираз } ")"
/// </summary>
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _index;

    private Parser(List<Token> tokens) => _tokens = tokens;

    /// <summary>Перший етап: перевіряє синтаксис і будує дерево. Кидає FormulaSyntaxException.</summary>
    public static Node Parse(string text)
    {
        var parser = new Parser(Lexer.Tokenize(text));
        Node node = parser.ParseExpression();
        if (parser.Current.Type != TokenType.End)
            throw parser.Unexpected("після завершеного виразу");
        return node;
    }

    private Token Current => _tokens[_index];

    private Token Advance()
    {
        Token t = _tokens[_index];
        if (t.Type != TokenType.End) _index++;
        return t;
    }

    private static bool IsComparison(TokenType type) =>
        type is TokenType.Equal or TokenType.Less or TokenType.Greater
             or TokenType.LessEqual or TokenType.GreaterEqual or TokenType.NotEqual;

    private static string Describe(Token t) => t.Type == TokenType.End ? "кінець виразу" : $"\"{t.Text}\"";

    private FormulaSyntaxException Unexpected(string context) =>
        new($"Неочікуваний елемент {Describe(Current)} {context}", Current.Position);

    private FormulaSyntaxException Expected(string what) =>
        new($"Очікувалось: {what}, але знайдено {Describe(Current)}", Current.Position);

    private Node ParseExpression()
    {
        Node left = ParseAdditive();
        if (IsComparison(Current.Type))
        {
            Token op = Advance();
            Node right = ParseAdditive();
            return new ComparisonNode(op.Type, left, right, op.Position);
        }
        return left;
    }

    private Node ParseAdditive()
    {
        Node left = ParseTerm();
        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            Token op = Advance();
            Node right = ParseTerm();
            left = new BinaryNode(op.Type == TokenType.Plus ? '+' : '-', left, right, op.Position);
        }
        return left;
    }

    private Node ParseTerm()
    {
        Node left = ParseUnary();
        while (Current.Type is TokenType.Star or TokenType.Slash)
        {
            Token op = Advance();
            Node right = ParseUnary();
            left = new BinaryNode(op.Type == TokenType.Star ? '*' : '/', left, right, op.Position);
        }
        return left;
    }

    private Node ParseUnary()
    {
        if (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            Token op = Advance();
            Node operand = ParseUnary();
            return new UnaryNode(op.Type == TokenType.Plus ? '+' : '-', operand, op.Position);
        }
        return ParsePrimary();
    }

    private Node ParsePrimary()
    {
        Token t = Current;
        switch (t.Type)
        {
            case TokenType.Number:
                Advance();
                return new NumberNode(BigInteger.Parse(t.Text, CultureInfo.InvariantCulture), t.Position);

            case TokenType.CellReference:
                Advance();
                if (!CellReference.TryParse(t.Text, out int row, out int column))
                    throw new FormulaSyntaxException(
                        $"Некоректне посилання \"{t.Text}\" (номери рядків починаються з 1)", t.Position);
                return new CellNode(row, column, t.Position);

            case TokenType.Function:
                return ParseFunction();

            case TokenType.LeftParen:
            {
                Advance();
                Node inner = ParseExpression();
                if (Current.Type != TokenType.RightParen)
                    throw Expected("закриваюча дужка \")\"");
                Advance();
                return inner;
            }

            case TokenType.End:
                throw new FormulaSyntaxException("Неочікуваний кінець виразу: очікувався операнд", t.Position);

            default:
                throw new FormulaSyntaxException(
                    $"Неочікуваний елемент {Describe(t)}: очікувався операнд (число, клітинка, mmax/mmin або \"(\")",
                    t.Position);
        }
    }

    private Node ParseFunction()
    {
        Token fn = Advance();
        if (Current.Type != TokenType.LeftParen)
            throw Expected($"відкриваюча дужка \"(\" після {fn.Text}");
        Advance();

        if (Current.Type == TokenType.RightParen)
            throw new FormulaSyntaxException($"Функція {fn.Text} вимагає принаймні один аргумент (N >= 1)", Current.Position);

        var arguments = new List<Node> { ParseExpression() };
        while (Current.Type == TokenType.Comma)
        {
            Advance();
            arguments.Add(ParseExpression());
        }

        if (Current.Type != TokenType.RightParen)
            throw Expected("кома або закриваюча дужка \")\"");
        Advance();
        return new FunctionNode(fn.Text, arguments, fn.Position);
    }
}
