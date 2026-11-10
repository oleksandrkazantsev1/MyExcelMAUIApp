using System.Text.Encodings.Web;
using System.Text.Json;

namespace MyExcelMAUIApp.Logic;

/// <summary>Власний формат файлу таблиці (JSON).</summary>
public sealed class SpreadsheetData
{
    public int Version { get; set; } = 1;
    public int Rows { get; set; }
    public int Columns { get; set; }
    public string[][] Cells { get; set; } = Array.Empty<string[]>();
}

public static class SpreadsheetSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(Spreadsheet sheet)
    {
        var data = new SpreadsheetData
        {
            Rows = sheet.RowCount,
            Columns = sheet.ColumnCount,
            Cells = new string[sheet.RowCount][]
        };
        for (int r = 0; r < sheet.RowCount; r++)
        {
            data.Cells[r] = new string[sheet.ColumnCount];
            for (int c = 0; c < sheet.ColumnCount; c++)
                data.Cells[r][c] = sheet.GetExpression(r, c);
        }
        return JsonSerializer.Serialize(data, Options);
    }

    public static Spreadsheet Deserialize(string json)
    {
        SpreadsheetData? data;
        try
        {
            data = JsonSerializer.Deserialize<SpreadsheetData>(json, Options);
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Файл має неправильний формат (не вдалося розібрати JSON).");
        }

        if (data == null || data.Rows < 1 || data.Rows > Spreadsheet.MaxRows ||
            data.Columns < 1 || data.Columns > Spreadsheet.MaxColumns || data.Cells.Length != data.Rows)
            throw new InvalidDataException("Файл не є коректною таблицею або має недопустимі розміри.");

        var sheet = new Spreadsheet(data.Rows, data.Columns);
        for (int r = 0; r < data.Rows; r++)
        {
            string[]? row = data.Cells[r];
            if (row == null || row.Length != data.Columns)
                throw new InvalidDataException($"Рядок {r + 1} у файлі пошкоджено.");
            for (int c = 0; c < data.Columns; c++)
                sheet.SetExpression(r, c, row[c] ?? "");
        }
        sheet.Recalculate();
        return sheet;
    }
}
