using System.Text.Json;

namespace AzerothQuesting.Companion;

internal sealed class CompanionSettings
{
    public string? WowRetailPath { get; set; }
    public bool ServiceSyncEnabled { get; set; } = true;
    public string ServiceBaseUrl { get; set; } = "http://10.0.10.246:8766";
    public string? ClientInstanceId { get; set; }
    public string? InstallationId { get; set; }
    public string? InstallationToken { get; set; }
}

internal static class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static CompanionSettings Load()
    {
        AppPaths.EnsureCreated();

        try
        {
            if (!File.Exists(AppPaths.SettingsFile))
            {
                return new CompanionSettings();
            }

            var json = File.ReadAllText(AppPaths.SettingsFile);
            return JsonSerializer.Deserialize<CompanionSettings>(json, Options) ?? new CompanionSettings();
        }
        catch
        {
            return new CompanionSettings();
        }
    }

    public static void Save(CompanionSettings settings)
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(AppPaths.SettingsFile, json);
    }
}
