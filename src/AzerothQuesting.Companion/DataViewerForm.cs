namespace AzerothQuesting.Companion;

internal sealed class DataViewerForm : Form
{
    private static readonly Color Background = Color.FromArgb(13, 16, 24);
    private static readonly Color Surface = Color.FromArgb(24, 28, 40);
    private static readonly Color SurfaceAlt = Color.FromArgb(30, 34, 49);
    private static readonly Color Border = Color.FromArgb(50, 56, 78);
    private static readonly Color TextPrimary = Color.FromArgb(238, 240, 248);
    private static readonly Color TextSecondary = Color.FromArgb(163, 170, 194);
    private static readonly Color Gold = Color.FromArgb(231, 181, 67);
    private static readonly Color Green = Color.FromArgb(78, 214, 142);

    private readonly Service01Client _serviceClient;
    private readonly string _companionVersion;

    private readonly Label _totalObservations = CreateMetricValue();
    private readonly Label _uniqueQuests = CreateMetricValue();
    private readonly Label _activeInstallations = CreateMetricValue();
    private readonly Label _yourObservations = CreateMetricValue();
    private readonly Label _status = new()
    {
        AutoSize = true,
        ForeColor = TextSecondary,
        Font = new Font("Segoe UI", 9, FontStyle.Regular),
        Text = "Loading submitted data...",
    };

    private readonly DataGridView _recentGrid = CreateGrid();
    private readonly DataGridView _classGrid = CreateGrid();
    private readonly DataGridView _versionsGrid = CreateGrid();
    private readonly Button _refreshButton = CreateActionButton("Refresh", Gold);
    private readonly Button _exportButton = CreateActionButton("Export Collected Quests", Gold);
    private bool _loading;

    public DataViewerForm(Service01Client serviceClient, string companionVersion)
    {
        _serviceClient = serviceClient;
        _companionVersion = companionVersion;

        Text = "Azeroth Questing - Submitted Data";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 620);
        Size = new Size(1180, 720);
        BackColor = Background;
        ForeColor = TextPrimary;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        Shown += async (_, _) => await RefreshAsync();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildMetrics(), 0, 1);
        root.Controls.Add(BuildTabs(), 0, 2);

        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            Padding = new Padding(4, 8, 0, 0),
        };
        _status.Dock = DockStyle.Fill;
        footer.Controls.Add(_status);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 3,
            RowCount = 1,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Background,
        };
        title.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Submitted Research Data",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2),
        });
        title.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Anonymous Azeroth Questing observations stored on the Azeroth Questing Server.",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
        });

        _exportButton.Click += async (_, _) => await ExportCollectedQuestsAsync();
        _exportButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _exportButton.Margin = new Padding(0, 0, 8, 0);
        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(_exportButton, 1, 0);
        panel.Controls.Add(_refreshButton, 2, 0);
        return panel;
    }

    private Control BuildMetrics()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
        };
        for (var i = 0; i < 4; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        grid.Controls.Add(CreateMetricCard("Total Observations", _totalObservations, "All submitted structured observations"), 0, 0);
        grid.Controls.Add(CreateMetricCard("Unique Quests", _uniqueQuests, "Distinct quest IDs observed"), 1, 0);
        grid.Controls.Add(CreateMetricCard("Companions Online", _activeInstallations, "Heartbeat seen in the last 10 minutes"), 2, 0);
        grid.Controls.Add(CreateMetricCard("Your Observations", _yourObservations, "Submitted by this Companion installation"), 3, 0);
        return grid;
    }

    private Control BuildTabs()
    {
        ConfigureRecentGrid();
        ConfigureClassGrid();
        ConfigureVersionsGrid();

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
        };
        tabs.TabPages.Add(CreateTab("Recent Observations", _recentGrid));
        tabs.TabPages.Add(CreateTab("Class Evidence", _classGrid));
        tabs.TabPages.Add(CreateTab("Versions", _versionsGrid));
        return tabs;
    }

    private static TabPage CreateTab(string title, Control content)
    {
        var page = new TabPage(title)
        {
            BackColor = Surface,
            Padding = new Padding(8),
        };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    private void ConfigureRecentGrid()
    {
        AddTextColumn(_recentGrid, "Quest", 78);
        AddTextColumn(_recentGrid, "Map", 68);
        AddTextColumn(_recentGrid, "Evidence", 90);
        AddTextColumn(_recentGrid, "Faction", 82);
        AddTextColumn(_recentGrid, "Class", 92);
        AddTextColumn(_recentGrid, "Level", 60);
        AddTextColumn(_recentGrid, "Completed", 78);
        AddTextColumn(_recentGrid, "Source", 70);
        AddTextColumn(_recentGrid, "Addon", 105);
        AddTextColumn(_recentGrid, "Companion", 110);
        AddTextColumn(_recentGrid, "Received", 145, fill: true);
    }

    private void ConfigureClassGrid()
    {
        AddTextColumn(_classGrid, "Quest", 80);
        AddTextColumn(_classGrid, "Map", 70);
        AddTextColumn(_classGrid, "Faction", 90);
        AddTextColumn(_classGrid, "Class", 100);
        AddTextColumn(_classGrid, "Observations", 100);
        AddTextColumn(_classGrid, "Strong", 80);
        AddTextColumn(_classGrid, "Installations", 100);
        AddTextColumn(_classGrid, "Completed", 90);
        AddTextColumn(_classGrid, "Last Received", 150, fill: true);
    }

    private void ConfigureVersionsGrid()
    {
        AddTextColumn(_versionsGrid, "Product", 180);
        AddTextColumn(_versionsGrid, "Version", 220);
        AddTextColumn(_versionsGrid, "Active Installations (30d)", 190, fill: true);
    }

    private async Task RefreshAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        _refreshButton.Enabled = false;
        _status.Text = "Loading submitted data from the Azeroth Questing Server...";
        _status.ForeColor = TextSecondary;

        try
        {
            var data = await _serviceClient.GetResearchDashboardAsync(_companionVersion);
            ApplyData(data);
            _status.ForeColor = Green;
            _status.Text = $"Updated {FormatTime(data.GeneratedAt)}. No installation IDs, account names, or raw SavedVariables are shown.";
        }
        catch (Exception ex)
        {
            _status.ForeColor = Gold;
            _status.Text = $"Could not load submitted data: {ex.Message}";
        }
        finally
        {
            _loading = false;
            _refreshButton.Enabled = true;
        }
    }

    private async Task ExportCollectedQuestsAsync()
    {
        if (_loading)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export Azeroth Questing Collected Quests",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = "csv",
            AddExtension = true,
            FileName = $"AzerothQuesting-Collected-Quests-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _exportButton.Enabled = false;
        _status.ForeColor = TextSecondary;
        _status.Text = "Exporting every quest currently collected by Azeroth Questing research...";
        try
        {
            var csv = await _serviceClient.ExportCollectedQuestsCsvAsync(_companionVersion);
            await File.WriteAllTextAsync(dialog.FileName, csv);
            _status.ForeColor = Green;
            _status.Text = $"Collected quest export saved to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            _status.ForeColor = Gold;
            _status.Text = $"Could not export collected quests: {ex.Message}";
        }
        finally
        {
            _exportButton.Enabled = true;
        }
    }

    private void ApplyData(ResearchDashboardData data)
    {
        _totalObservations.Text = data.Summary.TotalObservations.ToString("N0");
        _uniqueQuests.Text = data.Summary.UniqueQuests.ToString("N0");
        _activeInstallations.Text = data.Summary.ConnectedInstallations.ToString("N0");
        _yourObservations.Text = data.YourInstallation.Observations.ToString("N0");

        _recentGrid.Rows.Clear();
        foreach (var item in data.RecentObservations)
        {
            _recentGrid.Rows.Add(
                item.QuestId,
                item.MapId,
                item.Evidence,
                item.Faction,
                item.ClassFile,
                item.PlayerLevel,
                item.Completed ? "Yes" : "No",
                item.Source,
                item.AddonVersion ?? "unknown",
                item.CompanionVersion ?? "unknown",
                FormatTime(item.ReceivedAt));
        }

        _classGrid.Rows.Clear();
        foreach (var item in data.ClassEvidence)
        {
            _classGrid.Rows.Add(
                item.QuestId,
                item.MapId,
                item.Faction,
                item.ClassFile,
                item.Observations,
                item.StrongObservations,
                item.Installations,
                item.AnyCompleted ? "Yes" : "No",
                FormatTime(item.LastReceivedAt));
        }

        _versionsGrid.Rows.Clear();
        foreach (var item in data.CompanionVersions)
        {
            _versionsGrid.Rows.Add("Companion", item.Version, item.Installations);
        }
        foreach (var item in data.AddonVersions)
        {
            _versionsGrid.Rows.Add("Addon", item.Version, item.Installations);
        }
    }

    private static string FormatTime(DateTimeOffset value) => value.ToLocalTime().ToString("g");

    private static Panel CreateMetricCard(string title, Label value, string subtitle)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(5),
            Padding = new Padding(14, 10, 12, 8),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = title,
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
        }, 0, 0);

        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(value, 0, 1);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = subtitle,
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
        }, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private static Label CreateMetricValue() => new()
    {
        AutoSize = false,
        Text = "-",
        ForeColor = TextPrimary,
        Font = new Font("Segoe UI", 16, FontStyle.Bold),
    };

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            BackgroundColor = Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            EnableHeadersVisualStyles = false,
            GridColor = Border,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
        };

        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceAlt;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextSecondary;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(42, 35, 74);
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        grid.RowTemplate.Height = 28;
        return grid;
    }

    private static void AddTextColumn(DataGridView grid, string title, int width, bool fill = false)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = title,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.Automatic,
        });
    }

    private static Button CreateActionButton(string text, Color accent)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceAlt,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Padding = new Padding(12, 5, 12, 5),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = accent;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }
}
