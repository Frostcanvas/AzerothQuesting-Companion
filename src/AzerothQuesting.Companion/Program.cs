namespace AzerothQuesting.Companion;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        CompanionUpdateService.CleanupStaleUpdateDirectories();

        var form = new MainForm();
        UpdateChannelUi.Attach(form);
        UiTerminology.Apply(form);
        Application.Run(form);
    }
}
