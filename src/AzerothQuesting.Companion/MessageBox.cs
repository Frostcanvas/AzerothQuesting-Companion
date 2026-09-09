namespace AzerothQuesting.Companion;

internal static class MessageBox
{
    public static DialogResult Show(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        return System.Windows.Forms.MessageBox.Show(
            owner,
            UiTerminology.CleanForPlayer(text),
            UiTerminology.CleanForPlayer(caption),
            buttons,
            icon);
    }

    public static DialogResult Show(
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        return System.Windows.Forms.MessageBox.Show(
            UiTerminology.CleanForPlayer(text),
            UiTerminology.CleanForPlayer(caption),
            buttons,
            icon);
    }
}
