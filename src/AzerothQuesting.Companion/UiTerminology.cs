namespace AzerothQuesting.Companion;

internal static class UiTerminology
{
    public static void Apply(Form form)
    {
        ApplyControl(form);

        var timer = new System.Windows.Forms.Timer
        {
            Interval = 500,
        };
        timer.Tick += (_, _) => RefreshListBoxes(form);
        timer.Start();
        form.FormClosed += (_, _) => timer.Dispose();
    }

    private static void ApplyControl(Control control)
    {
        ReplaceControlText(control);
        control.TextChanged += (_, _) => ReplaceControlText(control);

        if (control is StatusStrip strip)
        {
            foreach (ToolStripItem item in strip.Items)
            {
                ReplaceToolStripText(item);
                item.TextChanged += (_, _) => ReplaceToolStripText(item);
            }
        }

        foreach (Control child in control.Controls)
        {
            ApplyControl(child);
        }
    }

    private static void RefreshListBoxes(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is ListBox listBox)
            {
                for (var i = 0; i < listBox.Items.Count; i++)
                {
                    if (listBox.Items[i] is not string value)
                    {
                        continue;
                    }

                    var replacement = ReplaceTerms(value);
                    if (!string.Equals(value, replacement, StringComparison.Ordinal))
                    {
                        listBox.Items[i] = replacement;
                    }
                }
            }

            RefreshListBoxes(child);
        }
    }

    private static void ReplaceControlText(Control control)
    {
        var replacement = ReplaceTerms(control.Text);
        if (!string.Equals(control.Text, replacement, StringComparison.Ordinal))
        {
            control.Text = replacement;
        }
    }

    private static void ReplaceToolStripText(ToolStripItem item)
    {
        var replacement = ReplaceTerms(item.Text);
        if (!string.Equals(item.Text, replacement, StringComparison.Ordinal))
        {
            item.Text = replacement;
        }
    }

    private static string ReplaceTerms(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var replacement = value
            .Replace("Service01 API", "Azeroth Questing Server", StringComparison.Ordinal)
            .Replace("Service01", "Azeroth Questing Server", StringComparison.Ordinal)
            .Replace("Scan / Queue Now", "Scan for Observations", StringComparison.Ordinal)
            .Replace("Queued Snapshots", "Pending Observations", StringComparison.Ordinal)
            .Replace("queued snapshots", "pending observations", StringComparison.Ordinal)
            .Replace("Snapshots", "Observations", StringComparison.Ordinal)
            .Replace("snapshots", "observations", StringComparison.Ordinal)
            .Replace("Snapshot", "Observation", StringComparison.Ordinal)
            .Replace("snapshot", "observation", StringComparison.Ordinal);

        if (replacement.Contains("Azeroth Questing Server", StringComparison.Ordinal))
        {
            var endpointStart = replacement.IndexOf(" (http", StringComparison.OrdinalIgnoreCase);
            if (endpointStart >= 0)
            {
                var endpointEnd = replacement.IndexOf(')', endpointStart);
                if (endpointEnd > endpointStart)
                {
                    replacement = replacement.Remove(endpointStart, endpointEnd - endpointStart + 1);
                }
            }
        }

        return replacement;
    }
}
