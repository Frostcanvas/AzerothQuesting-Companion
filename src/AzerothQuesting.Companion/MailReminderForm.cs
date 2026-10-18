using System.Text.Json;

namespace AzerothQuesting.Companion;

// Mail reminders are private, local-only data. They are never included in the research Outbox.
internal sealed class MailReminderService
{
    private static readonly string FilePath = Path.Combine(AppPaths.Root, "mail-reminders.json");
    private readonly List<MailReminder> _items = new();
    private readonly object _gate = new();

    public MailReminderService()
    {
        try
        {
            if (File.Exists(FilePath))
                _items = JsonSerializer.Deserialize<List<MailReminder>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Never block Companion startup because of a local reminder cache.
        }
    }

    public IReadOnlyList<MailReminder> List()
    {
        lock (_gate)
            return _items.OrderBy(x => x.ExpiresAt).Select(x => x with { }).ToArray();
    }

    public void Add(string character, string realm, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(realm))
            throw new ArgumentException("Character and realm are required.");
        if (expiresAt <= DateTimeOffset.Now)
            throw new ArgumentException("Expiration must be in the future.");
        lock (_gate)
        {
            _items.Add(new MailReminder(Guid.NewGuid(), character.Trim(), realm.Trim(), expiresAt, 0));
            Save();
        }
    }

    public void Remove(Guid id)
    {
        lock (_gate)
        {
            _items.RemoveAll(x => x.Id == id);
            Save();
        }
    }

    public IReadOnlyList<MailReminder> Due()
    {
        var due = new List<MailReminder>();
        var now = DateTimeOffset.Now;
        lock (_gate)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var left = item.ExpiresAt - now;
                // Distinct local alerts when crossing seven days, three days and one day.
                var stage = left <= TimeSpan.Zero ? 4
                    : left <= TimeSpan.FromDays(1) ? 3
                    : left <= TimeSpan.FromDays(3) ? 2
                    : left <= TimeSpan.FromDays(7) ? 1 : 0;
                if (stage > item.NotifiedStage)
                {
                    due.Add(item);
                    _items[i] = item with { NotifiedStage = stage };
                }
            }
            if (due.Count > 0)
                Save();
        }
        return due;
    }

    private void Save()
    {
        Directory.CreateDirectory(AppPaths.Root);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, true);
    }
}

internal sealed record MailReminder(Guid Id, string Character, string Realm, DateTimeOffset ExpiresAt, int NotifiedStage);

internal sealed class MailReminderForm : Form
{
    public MailReminderForm(MailReminderService service)
    {
        Text = "Character Mail Reminders";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(750, 500);
        MinimumSize = new Size(650, 420);
        BackColor = Color.FromArgb(20, 23, 34);
        ForeColor = Color.White;

        var character = new TextBox { Width = 140, PlaceholderText = "Character" };
        var realm = new TextBox { Width = 150, PlaceholderText = "Realm" };
        var expiry = new DateTimePicker
        {
            Width = 175,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "MMM dd yyyy  h:mm tt",
            Value = DateTime.Now.AddDays(30)
        };
        var add = new Button { Text = "Add reminder", AutoSize = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true };
        var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 86, Padding = new Padding(12), WrapContents = true };
        form.Controls.AddRange(new Control[] { character, realm, expiry, add, remove });

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.FromArgb(24, 28, 40),
            ForeColor = Color.Black
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Character", HeaderText = "Character", DataPropertyName = "Character", Width = 140 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Realm", HeaderText = "Realm", DataPropertyName = "Realm", Width = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Expires", HeaderText = "Mail expires (local time)", DataPropertyName = "Expires", Width = 190 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Remaining", HeaderText = "Remaining", DataPropertyName = "Remaining", Width = 140 });

        var footer = new Label
        {
            Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(12),
            ForeColor = Color.Silver,
            Text = "Local-only reminders. Enter the mail expiry shown in WoW; the Companion will alert from the Windows tray while running. Re-check your mailbox to confirm the timer."
        };
        Controls.Add(grid);
        Controls.Add(form);
        Controls.Add(footer);

        List<MailReminder> current = new();
        void Refresh()
        {
            current = service.List().ToList();
            grid.DataSource = current.Select(item => new
            {
                item.Character, item.Realm,
                Expires = item.ExpiresAt.ToLocalTime().ToString("yyyy-MM-dd h:mm tt"),
                Remaining = item.ExpiresAt <= DateTimeOffset.Now ? "Expired" :
                    $"{Math.Ceiling((item.ExpiresAt - DateTimeOffset.Now).TotalHours)} hours"
            }).ToList();
        }

        add.Click += (_, _) =>
        {
            try
            {
                service.Add(character.Text, realm.Text, new DateTimeOffset(expiry.Value));
                Refresh();
            }
            catch (ArgumentException ex)
            {
                System.Windows.Forms.MessageBox.Show(this, ex.Message, "Mail Reminder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        remove.Click += (_, _) =>
        {
            if (grid.CurrentRow is null || grid.CurrentRow.Index < 0 || grid.CurrentRow.Index >= current.Count)
                return;
            service.Remove(current[grid.CurrentRow.Index].Id);
            Refresh();
        };
        Refresh();
    }
}
