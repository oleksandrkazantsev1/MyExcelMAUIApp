using System.Globalization;
using System.Text;

namespace MyExcelMAUIApp.Logic;

/// <summary>Допоміжні методи для імен клітинок (A1, B12, AA3).</summary>
public static class CellReference
{
    /// <summary>Назва стовпця за номером (1 → A, 26 → Z, 27 → AA).</summary>
    public static string ColumnName(int column)
    {
        var sb = new StringBuilder();
        while (column > 0)
        {
            int modulo = (column - 1) % 26;
            sb.Insert(0, (char)('A' + modulo));
            column = (column - 1) / 26;
        }
        return sb.ToString();
    }

    /// <summary>Ім'я клітинки за 0-based індексами рядка та стовпця.</summary>
    public static string ToName(int row, int column) =>
        ColumnName(column + 1) + (row + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>Розбирає ім'я виду "AB12" (літери вже у верхньому регістрі) у 0-based індекси.</summary>
    public static bool TryParse(string text, out int row, out int column)
    {
        row = column = -1;
        int i = 0, col = 0;
        while (i < text.Length && text[i] >= 'A' && text[i] <= 'Z')
        {
            col = col * 26 + (text[i] - 'A' + 1);
            i++;
        }
        if (i == 0 || i == text.Length) return false;
        if (!int.TryParse(text.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out int rowNumber) || rowNumber < 1)
            return false;
        row = rowNumber - 1;
        column = col - 1;
        return true;
    }
}
