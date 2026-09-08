namespace AzerothQuesting.Companion;

internal static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AzerothQuesting",
        "Companion");

    public static string Outbox { get; } = Path.Combine(Root, "Outbox");
    public static string Backups { get; } = Path.Combine(Root, "Backups");
    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Outbox);
        Directory.CreateDirectory(Backups);
    }
}
