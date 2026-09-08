namespace AzerothQuesting.Companion;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (CompanionUpdateService.TryRunUpdaterMode(args))
        {
            return;
        }

        CompanionUpdateService.CleanupStaleUpdateDirectories();
        Application.Run(new MainForm());
    }
}
