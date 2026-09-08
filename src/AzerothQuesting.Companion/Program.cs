namespace AzerothQuesting.Companion;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        CompanionUpdateService.CleanupStaleUpdateDirectories();

        var form = new MainForm();
        UiTerminology.Apply(form);
        Application.Run(form);
    }
}
