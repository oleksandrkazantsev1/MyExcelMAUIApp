using System.Text;

namespace MyExcelMAUIApp.Services;

/// <summary>Зберігання у папці "Документи\MyExcelMAUIApp".</summary>
public sealed class LocalFileStorage : ISpreadsheetStorage
{
    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyExcelMAUIApp");

    public string DisplayName => "Локальний диск";

    public async Task<string> SaveAsync(string fileName, string content)
    {
        Directory.CreateDirectory(Folder);
        string path = Path.Combine(Folder, FileNaming.Normalize(fileName));
        await File.WriteAllTextAsync(path, content, Encoding.UTF8);
        return path;
    }

    public Task<IReadOnlyList<StoredFile>> ListAsync()
    {
        IReadOnlyList<StoredFile> result = Array.Empty<StoredFile>();
        if (Directory.Exists(Folder))
        {
            result = new DirectoryInfo(Folder)
                .GetFiles("*" + FileNaming.Suffix)
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => new StoredFile(f.FullName, f.Name))
                .ToList();
        }
        return Task.FromResult(result);
    }

    public Task<string> ReadAsync(StoredFile file) => File.ReadAllTextAsync(file.Id, Encoding.UTF8);
}
