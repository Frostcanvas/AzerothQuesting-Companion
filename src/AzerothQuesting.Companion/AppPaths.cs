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
    public static string CompletedQuestsFile { get; } = Path.Combine(Root, "completed-quests.json");
    public static string CompletedQuestsTsv { get; } = Path.Combine(Root, "completed-quests.tsv");
    public static string CompletedQuestRepositoryHistory { get; } = Path.Combine(Root, "completed-quest-repository-hashes.txt");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Outbox);
        Directory.CreateDirectory(Backups);
    }
}
