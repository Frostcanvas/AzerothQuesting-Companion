using System.Text.Json;

namespace AzerothQuesting.Companion;

internal enum UpdateChannel
{
    Stable,
    Beta,
}

internal static class UpdateChannelSettings
{
    public static UpdateChannel Parse(string? value) =>
        string.Equals(value, "beta", StringComparison.OrdinalIgnoreCase)
            ? UpdateChannel.Beta
            : UpdateChannel.Stable;

    public static string Serialize(UpdateChannel channel) =>
        channel == UpdateChannel.Beta ? "beta" : "stable";

    public static string DisplayName(UpdateChannel channel) =>
        channel == UpdateChannel.Beta ? "Beta" : "Stable";
}

internal sealed class CompanionSettings
{
    public string? WowRetailPath { get; set; }
    public bool ServiceSyncEnabled { get; set; } = true;
    public string ServiceBaseUrl { get; set; } = SettingsService.DefaultServiceBaseUrl;
    public string UpdateChannel { get; set; } = "stable";
    public string? ClientInstanceId { get; set; }
    public string? InstallationId { get; set; }
    public string? InstallationToken { get; set; }
}

internal static class SettingsService
{
    public const string DefaultServiceBaseUrl = "https://aq.frostlabs.dev";
    private const string LegacyLanServiceBaseUrl = "http://10.0.10.246:8766";

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
            var settings = JsonSerializer.Deserialize<CompanionSettings>(json, Options) ?? new CompanionSettings();

            if (string.IsNullOrWhiteSpace(settings.ServiceBaseUrl) ||
                string.Equals(
                    settings.ServiceBaseUrl.Trim().TrimEnd('/'),
                    LegacyLanServiceBaseUrl,
                    StringComparison.OrdinalIgnoreCase))
            {
                settings.ServiceBaseUrl = DefaultServiceBaseUrl;
                Save(settings);
            }

            return settings;
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
