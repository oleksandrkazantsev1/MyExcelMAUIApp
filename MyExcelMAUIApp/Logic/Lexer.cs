namespace MyExcelMAUIApp.Logic;

/// <summary>Лексичний аналіз: розбиває рядок виразу на токени.</summary>
public static class Lexer
{
    private static bool IsDigit(char c) => c >= '0' && c <= '9';
    private static bool IsLatin(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    public static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            int start = i;

            if (IsDigit(c))
            {
                while (i < text.Length && IsDigit(text[i])) i++;
                if (i < text.Length && IsLatin(text[i]))
                    throw new FormulaSyntaxException("Некоректне число: після цифр не можуть іти літери", start);
                tokens.Add(new Token(TokenType.Number, text.Substring(start, i - start), start));
                continue;
            }

            if (IsLatin(c))
            {
                while (i < text.Length && IsLatin(text[i])) i++;
                int lettersEnd = i;
                while (i < text.Length && IsDigit(text[i])) i++;
                if (i < text.Length && IsLatin(text[i]))
                    throw new FormulaSyntaxException("Некоректне ім'я: літери після цифр", start);

                string word = text.Substring(start, i - start);
                if (i == lettersEnd)
                {
                    string lower = word.ToLowerInvariant();
                    if (lower == "mmax" || lower == "mmin")
                        tokens.Add(new Token(TokenType.Function, lower, start));
                    else
                        throw new FormulaSyntaxException(
                            $"Невідомий ідентифікатор \"{word}\". Очікувалось посилання на клітинку (наприклад, A1) або функція mmax/mmin", start);
                }
                else
                {
                    if (lettersEnd - start > 3)
                        throw new FormulaSyntaxException($"Занадто довга назва стовпця у \"{word}\"", start);
                    if (i - lettersEnd > 7)
                        throw new FormulaSyntaxException($"Занадто великий номер рядка у \"{word}\"", start);
                    tokens.Add(new Token(TokenType.CellReference, word.ToUpperInvariant(), start));
                }
                continue;
            }

            i++;
            switch (c)
            {
                case '+': tokens.Add(new Token(TokenType.Plus, "+", start)); break;
                case '-': tokens.Add(new Token(TokenType.Minus, "-", start)); break;
                case '*': tokens.Add(new Token(TokenType.Star, "*", start)); break;
                case '/': tokens.Add(new Token(TokenType.Slash, "/", start)); break;
                case '(': tokens.Add(new Token(TokenType.LeftParen, "(", start)); break;
                case ')': tokens.Add(new Token(TokenType.RightParen, ")", start)); break;
                case ',': tokens.Add(new Token(TokenType.Comma, ",", start)); break;
                case '=': tokens.Add(new Token(TokenType.Equal, "=", start)); break;
                case '<':
                    if (i < text.Length && text[i] == '=') { i++; tokens.Add(new Token(TokenType.LessEqual, "<=", start)); }
                    else if (i < text.Length && text[i] == '>') { i++; tokens.Add(new Token(TokenType.NotEqual, "<>", start)); }
                    else tokens.Add(new Token(TokenType.Less, "<", start));
                    break;
                case '>':
                    if (i < text.Length && text[i] == '=') { i++; tokens.Add(new Token(TokenType.GreaterEqual, ">=", start)); }
                    else tokens.Add(new Token(TokenType.Greater, ">", start));
                    break;
                default:
                    throw new FormulaSyntaxException(DescribeInvalidChar(c), start);
            }
        }

        tokens.Add(new Token(TokenType.End, "", text.Length));
        return tokens;
    }

    private static string DescribeInvalidChar(char c)
    {
        if (c == '.') return "Недопустимий символ '.': підтримуються лише цілі числа";
        if (char.IsLetter(c)) return $"Недопустимий символ '{c}': використовуйте латинські літери (A-Z)";
        return $"Недопустимий символ '{c}'";
    }
}
