using MyExcelMAUIApp.Logic;
using Xunit;

namespace MyExcelMAUIApp.Tests;

public class SpreadsheetTests
{
    // Створює таблицю 5x5 і заповнює вказані клітинки, потім перераховує її.
    private static Spreadsheet CreateSheet(params (string Cell, string Text)[] cells)
    {
        var sheet = new Spreadsheet(5, 5);
        foreach (var (cell, text) in cells)
        {
            CellReference.TryParse(cell, out int row, out int column);
            sheet.SetExpression(row, column, text);
        }
        sheet.Recalculate();
        return sheet;
    }

    // ТЕСТ 1: правильні вирази обчислюються правильно
    // (бінарні/унарні операції, mmax/mmin, всі порівняння, довгі числа, дужки).
    // A1 = 5, B1 = 3; перевіряється вираз у C1.
    [Theory]
    [InlineData("A1 > B1", "ІСТИНА")]
    [InlineData("A1 < B1", "ХИБНІСТЬ")]
    [InlineData("A1 >= 5", "ІСТИНА")]
    [InlineData("B1 <= 2", "ХИБНІСТЬ")]
    [InlineData("A1 = B1 + 2", "ІСТИНА")]
    [InlineData("A1 <> B1", "ІСТИНА")]
    [InlineData("mmax(A1, B1, 10) = 10", "ІСТИНА")]
    [InlineData("mmin(A1, B1) <> 3", "ХИБНІСТЬ")]
    [InlineData("-A1 + +B1 * 2 - 10 / 3 <= 0", "ІСТИНА")]
    [InlineData("(A1 > B1) = (B1 <> 3)", "ХИБНІСТЬ")]
    [InlineData("123456789012345678901234567890 * 2 > 100", "ІСТИНА")]
    [InlineData("A1 + B1", "8")]
    public void Evaluates_Valid_Expressions_Correctly(string expression, string expected)
    {
        var sheet = CreateSheet(("A1", "5"), ("B1", "3"), ("C1", expression));

        string actual = sheet.GetDisplayText(0, 2, DisplayMode.Values);

        Assert.Equal(expected, actual);
        Assert.False(sheet.HasError(0, 2));
    }

    // ТЕСТ 2: перший етап — синтаксична перевірка знаходить помилку
    // і вказує правильну позицію символа.
    [Theory]
    [InlineData("A1 >", 5)]          // немає правого операнда
    [InlineData("(A1 > 2", 8)]       // не закрито дужку
    [InlineData("1.5 > 1", 2)]       // дробові числа не підтримуються
    [InlineData("mmax()", 6)]        // функція без аргументів (N >= 1)
    [InlineData("A1 < B1 < 3", 9)]   // подвійне порівняння
    public void Reports_Syntax_Errors_With_Position(string expression, int expectedPosition)
    {
        var sheet = CreateSheet(("A1", "5"), ("B1", "3"), ("C1", expression));

        Assert.True(sheet.HasError(0, 2));
        Assert.Equal("#ПОМИЛКА", sheet.GetDisplayText(0, 2, DisplayMode.Values));

        string message = Assert.Single(sheet.GetErrors()).Message;
        Assert.Contains("Синтаксична помилка", message);
        Assert.Contains($"(символ {expectedPosition})", message);
    }

    // ТЕСТ 3: другий етап — помилки обчислення:
    // ділення на нуль, циклічні посилання, неіснуюча клітинка, змішування типів.
    [Fact]
    public void Detects_Evaluation_Errors()
    {
        var sheet = CreateSheet(
            ("A1", "5"),
            ("B1", "A1 / (A1 - 5) > 1"),   // ділення на нуль
            ("C1", "D1 = 1"),                // цикл: C1 -> D1 -> C1
            ("D1", "C1 = 1"),
            ("E1", "Z99 > 1"),               // клітинки Z99 немає в таблиці 5x5
            ("A2", "A1 > 1"),
            ("B2", "A2 + 1"));               // логічне значення в арифметиці

        Assert.Contains("Ділення на нуль", sheet.DescribeCell(0, 1));
        Assert.Contains("Циклічне посилання", sheet.DescribeCell(0, 2) + sheet.DescribeCell(0, 3));
        Assert.Contains("неіснуючу клітинку", sheet.DescribeCell(0, 4));
        Assert.Contains("логічне значення", sheet.DescribeCell(1, 1));

        // Правильна клітинка поруч з помилковими не зіпсована
        Assert.Equal("ІСТИНА", sheet.GetDisplayText(1, 0, DisplayMode.Values));
    }
}
