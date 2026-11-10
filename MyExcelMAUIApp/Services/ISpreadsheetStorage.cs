namespace MyExcelMAUIApp.Services;

/// <summary>Файл, що зберігається у сховищі (Id — шлях або ідентифікатор Google Drive).</summary>
public sealed record StoredFile(string Id, string Name);

/// <summary>Абстракція сховища таблиць: локальний диск або Google Drive.</summary>
public interface ISpreadsheetStorage
{
    string DisplayName { get; }

    /// <summary>Зберігає вміст і повертає опис місця збереження.</summary>
    Task<string> SaveAsync(string fileName, string content);

    Task<IReadOnlyList<StoredFile>> ListAsync();

    Task<string> ReadAsync(StoredFile file);
}
