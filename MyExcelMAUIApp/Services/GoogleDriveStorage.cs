using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Microsoft.Maui.Storage;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace MyExcelMAUIApp.Services;

/// <summary>
/// Зберігання таблиць на Google Drive (OAuth 2.0, область drive.file —
/// програма бачить лише файли, створені нею самою).
/// Потрібен файл Resources/Raw/credentials.json (OAuth client ID типу "Desktop app").
/// </summary>
public sealed class GoogleDriveStorage : ISpreadsheetStorage
{
    private const string CredentialsFile = "credentials.json";
    private const string ApplicationName = "MyExcelMAUIApp";
    private const string JsonMime = "application/json";

    private DriveService? _service;

    public string DisplayName => "Google Drive";

    private async Task<DriveService> GetServiceAsync()
    {
        if (_service != null) return _service;

        GoogleClientSecrets secrets;
        try
        {
            using Stream stream = await FileSystem.OpenAppPackageFileAsync(CredentialsFile);
            secrets = GoogleClientSecrets.FromStream(stream);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException(
                "Не знайдено файл credentials.json. Додайте його до Resources/Raw (див. інструкцію з налаштування Google Drive).");
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets.Secrets,
            new[] { DriveService.Scope.DriveFile },
            "user",
            cts.Token,
            new FileDataStore(Path.Combine(FileSystem.AppDataDirectory, "google-token"), true));

        _service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });
        return _service;
    }

    public async Task<string> SaveAsync(string fileName, string content)
    {
        DriveService service = await GetServiceAsync();
        string name = FileNaming.Normalize(fileName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        DriveFile? existing = await FindByNameAsync(service, name);
        IUploadProgress progress;
        if (existing == null)
        {
            var request = service.Files.Create(new DriveFile { Name = name, MimeType = JsonMime }, stream, JsonMime);
            request.Fields = "id";
            progress = await request.UploadAsync();
        }
        else
        {
            var request = service.Files.Update(new DriveFile(), existing.Id, stream, JsonMime);
            request.Fields = "id";
            progress = await request.UploadAsync();
        }

        if (progress.Status != UploadStatus.Completed)
            throw new IOException(progress.Exception?.Message ?? "Не вдалося завантажити файл на Google Drive.");

        return $"Google Drive: {name}";
    }

    public async Task<IReadOnlyList<StoredFile>> ListAsync()
    {
        DriveService service = await GetServiceAsync();
        var request = service.Files.List();
        request.Q = $"trashed = false and mimeType = '{JsonMime}'";
        request.Fields = "files(id, name)";
        request.OrderBy = "modifiedTime desc";
        request.PageSize = 20;

        var response = await request.ExecuteAsync();
        return response.Files
            .Where(f => f.Name != null && f.Name.EndsWith(FileNaming.Suffix, StringComparison.OrdinalIgnoreCase))
            .Select(f => new StoredFile(f.Id, f.Name))
            .ToList();
    }

    public async Task<string> ReadAsync(StoredFile file)
    {
        DriveService service = await GetServiceAsync();
        using var stream = new MemoryStream();
        IDownloadProgress progress = await service.Files.Get(file.Id).DownloadAsync(stream);
        if (progress.Status != DownloadStatus.Completed)
            throw new IOException(progress.Exception?.Message ?? "Не вдалося завантажити файл з Google Drive.");
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task<DriveFile?> FindByNameAsync(DriveService service, string name)
    {
        var request = service.Files.List();
        request.Q = $"trashed = false and name = '{name.Replace("\\", "\\\\").Replace("'", "\\'")}'";
        request.Fields = "files(id, name)";
        request.PageSize = 1;
        var response = await request.ExecuteAsync();
        return response.Files.FirstOrDefault();
    }
}
