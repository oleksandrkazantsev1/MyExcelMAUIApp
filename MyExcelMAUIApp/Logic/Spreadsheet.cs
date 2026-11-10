using System.Numerics;

namespace MyExcelMAUIApp.Logic;

public enum DisplayMode { Expressions, Values }

public sealed record CellError(string CellName, string Message);

internal enum EvalState { NotStarted, InProgress, Done }

internal sealed class Cell
{
    public string Expression { get; set; } = "";
    public Node? Ast { get; set; }
    public Value? Result { get; set; }
    public FormulaException? Error { get; set; }
    public EvalState State { get; set; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Expression);

    public void ResetCalculation()
    {
        Ast = null;
        Result = null;
        Error = null;
        State = EvalState.NotStarted;
    }
}

/// <summary>
/// Модель електронної таблиці: зберігає вирази, виконує двоетапну обробку
/// (1 — синтаксична перевірка, 2 — обчислення з виявленням циклів).
/// Не залежить від інтерфейсу користувача.
/// </summary>
public sealed class Spreadsheet : IEvaluationContext
{
    public const int MaxRows = 100;
    public const int MaxColumns = 52;

    private readonly List<List<Cell>> _rows = new();

    public Spreadsheet(int rows, int columns)
    {
        if (rows < 1 || rows > MaxRows) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns < 1 || columns > MaxColumns) throw new ArgumentOutOfRangeException(nameof(columns));
        ColumnCount = columns;
        for (int r = 0; r < rows; r++) _rows.Add(CreateRow(columns));
    }

    public int RowCount => _rows.Count;
    public int ColumnCount { get; private set; }

    private static List<Cell> CreateRow(int columns)
    {
        var row = new List<Cell>(columns);
        for (int c = 0; c < columns; c++) row.Add(new Cell());
        return row;
    }

    // ---------- Редагування структури ----------

    public bool AddRow()
    {
        if (RowCount >= MaxRows) return false;
        _rows.Add(CreateRow(ColumnCount));
        return true;
    }

    public bool RemoveLastRow()
    {
        if (RowCount <= 1) return false;
        _rows.RemoveAt(RowCount - 1);
        return true;
    }

    public bool AddColumn()
    {
        if (ColumnCount >= MaxColumns) return false;
        foreach (var row in _rows) row.Add(new Cell());
        ColumnCount++;
        return true;
    }

    public bool RemoveLastColumn()
    {
        if (ColumnCount <= 1) return false;
        foreach (var row in _rows) row.RemoveAt(ColumnCount - 1);
        ColumnCount--;
        return true;
    }

    // ---------- Вміст клітинок ----------

    public string GetExpression(int row, int column) => _rows[row][column].Expression;

    /// <summary>Записує вираз. Повертає true, якщо вміст змінився.</summary>
    public bool SetExpression(int row, int column, string text)
    {
        Cell cell = _rows[row][column];
        if (cell.Expression == text) return false;
        cell.Expression = text;
        return true;
    }

    public bool HasError(int row, int column) => _rows[row][column].Error != null;

    public string GetDisplayText(int row, int column, DisplayMode mode)
    {
        Cell cell = _rows[row][column];
        if (mode == DisplayMode.Expressions) return cell.Expression;
        if (cell.IsEmpty) return "";
        if (cell.Error != null) return "#ПОМИЛКА";
        return cell.Result!.Value.ToString();
    }

    public string DescribeCell(int row, int column)
    {
        string name = CellReference.ToName(row, column);
        Cell cell = _rows[row][column];
        if (cell.IsEmpty) return $"{name}: порожня клітинка (у виразах дорівнює 0)";
        if (cell.Error != null) return $"{name}: {cell.Error.Describe()}";
        return $"{name} = {cell.Result!.Value}";
    }

    public IReadOnlyList<CellError> GetErrors()
    {
        var errors = new List<CellError>();
        for (int r = 0; r < RowCount; r++)
            for (int c = 0; c < ColumnCount; c++)
            {
                FormulaException? error = _rows[r][c].Error;
                if (error != null) errors.Add(new CellError(CellReference.ToName(r, c), error.Describe()));
            }
        return errors;
    }

    // ---------- Обробка ----------

    /// <summary>Повний перерахунок: етап 1 — синтаксис усіх клітинок, етап 2 — обчислення.</summary>
    public void Recalculate()
    {
        foreach (var row in _rows)
            foreach (Cell cell in row)
            {
                cell.ResetCalculation();
                if (cell.IsEmpty) { cell.State = EvalState.Done; continue; }
                try
                {
                    cell.Ast = Parser.Parse(cell.Expression);
                }
                catch (FormulaSyntaxException ex)
                {
                    cell.Error = ex;
                    cell.State = EvalState.Done;
                }
            }

        for (int r = 0; r < RowCount; r++)
            for (int c = 0; c < ColumnCount; c++)
                EnsureEvaluated(r, c);
    }

    private void EnsureEvaluated(int row, int column)
    {
        Cell cell = _rows[row][column];
        if (cell.State != EvalState.NotStarted) return;

        cell.State = EvalState.InProgress;
        try
        {
            if (cell.Ast != null) cell.Result = cell.Ast.Evaluate(this);
        }
        catch (FormulaEvaluationException ex)
        {
            cell.Error = ex;
        }
        cell.State = EvalState.Done;
    }

    /// <summary>Викликається вузлами дерева при зверненні до клітинки.</summary>
    public Value GetCellValue(int row, int column, int position)
    {
        string name = CellReference.ToName(row, column);
        if (row < 0 || row >= RowCount || column < 0 || column >= ColumnCount)
            throw new FormulaEvaluationException($"Посилання на неіснуючу клітинку {name}", position);

        Cell cell = _rows[row][column];
        if (cell.State == EvalState.InProgress)
            throw new FormulaEvaluationException($"Циклічне посилання: клітинка {name} залежить сама від себе", position);

        EnsureEvaluated(row, column);
        if (cell.Error != null)
            throw new FormulaEvaluationException($"Клітинка {name} містить помилку", position);

        return cell.Result ?? Value.FromInteger(BigInteger.Zero);
    }
}
