namespace AzerothQuesting.Companion;

internal static class AppIcon
{
    public static Icon Load()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AzerothQuesting.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        return Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            ?? (Icon)SystemIcons.Application.Clone();
    }
}
