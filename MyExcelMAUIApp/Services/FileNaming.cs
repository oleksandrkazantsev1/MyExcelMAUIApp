namespace MyExcelMAUIApp.Services;

public static class FileNaming
{
    /// <summary>Розширення власного формату таблиць.</summary>
    public const string Suffix = ".sheet.json";

    public static string Normalize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim();
        return name.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) ? name : name + Suffix;
    }

    public static string StripSuffix(string name) =>
        name.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) ? name[..^Suffix.Length] : name;
}
