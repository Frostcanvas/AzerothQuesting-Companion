namespace AzerothQuesting.Companion;

internal static class UpdateChannelUi
{
    private static readonly Color StableAccent = Color.FromArgb(78, 214, 142);
    private static readonly Color BetaAccent = Color.FromArgb(231, 181, 67);
    private static readonly Color SurfaceAlt = Color.FromArgb(30, 34, 49);
    private static readonly Color TextPrimary = Color.FromArgb(238, 240, 248);

    public static void Attach(Form form)
    {
        var topBar = FindTopBar(form);
        if (topBar is null)
        {
            return;
        }

        var button = new Button
        {
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceAlt,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0, 0, 9, 0),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 47, 66);

        void RefreshButton()
        {
            var settings = SettingsService.Load();
            var channel = UpdateChannelSettings.Parse(settings.UpdateChannel);
            button.Text = channel == UpdateChannel.Beta
                ? "Update Channel: BETA"
                : "Update Channel: Stable";
            button.FlatAppearance.BorderColor = channel == UpdateChannel.Beta ? BetaAccent : StableAccent;
        }

        button.Click += (_, _) =>
        {
            var settings = SettingsService.Load();
            var current = UpdateChannelSettings.Parse(settings.UpdateChannel);
            var next = current == UpdateChannel.Stable ? UpdateChannel.Beta : UpdateChannel.Stable;
            settings.UpdateChannel = UpdateChannelSettings.Serialize(next);
            SettingsService.Save(settings);
            RefreshButton();

            var description = next == UpdateChannel.Beta
                ? "Beta channel enabled for both the Companion and Azeroth Questing addon. Check for Updates will include GitHub prereleases as well as stable releases."
                : "Stable channel enabled for both the Companion and Azeroth Questing addon. Check for Updates will ignore GitHub prereleases.";

            MessageBox.Show(
                form,
                description + "\n\nClick Check for Updates to refresh the available versions.",
                "Azeroth Questing Update Channel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };

        RefreshButton();
        topBar.Controls.Add(button);
        topBar.Controls.SetChildIndex(button, Math.Max(0, topBar.Controls.Count - 2));
    }

    private static FlowLayoutPanel? FindTopBar(Control root)
    {
        if (root is FlowLayoutPanel flow
            && flow.Controls.OfType<Button>().Any(button =>
                string.Equals(button.Text, "Check for Updates", StringComparison.OrdinalIgnoreCase)))
        {
            return flow;
        }

        foreach (Control child in root.Controls)
        {
            var found = FindTopBar(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
