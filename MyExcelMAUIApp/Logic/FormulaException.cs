namespace MyExcelMAUIApp.Logic;

/// <summary>Базовий клас помилок виразу. Position — 0-based позиція символа (або -1).</summary>
public abstract class FormulaException : Exception
{
    protected FormulaException(string message, int position) : base(message) => Position = position;

    public int Position { get; }

    protected abstract string Category { get; }

    /// <summary>Повний опис помилки для користувача (з локалізацією позиції).</summary>
    public string Describe() =>
        Position >= 0 ? $"{Category} (символ {Position + 1}): {Message}" : $"{Category}: {Message}";
}

/// <summary>Помилка першого етапу — синтаксична перевірка.</summary>
public sealed class FormulaSyntaxException : FormulaException
{
    public FormulaSyntaxException(string message, int position) : base(message, position) { }
    protected override string Category => "Синтаксична помилка";
}

/// <summary>Помилка другого етапу — обчислення значення.</summary>
public sealed class FormulaEvaluationException : FormulaException
{
    public FormulaEvaluationException(string message, int position = -1) : base(message, position) { }
    protected override string Category => "Помилка обчислення";
}
