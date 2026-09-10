using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AzerothQuesting.Companion;

internal sealed record CompletedQuestImportResult(
    bool Found,
    bool Changed,
    string Character,
    string Realm,
    int QuestCount);

internal sealed class CompletedQuestStore
{
    private static readonly Regex WireRegex = new(
        @"AQC1\|1\|(?:Alliance|Horde|Neutral|Unknown)\|[A-Z]+\|\d{1,3}\|\d+\|[A-Za-z0-9._-]+\|[0-9A-F~,]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly object _sync = new();
    private readonly Dictionary<string, CompletedQuestCharacterSnapshot> _characters =
        new(StringComparer.OrdinalIgnoreCase);

    public CompletedQuestStore()
    {
        AppPaths.EnsureCreated();
        Load();
        lock (_sync)
        {
            PersistUnlocked();
        }
    }

    public int CharacterCount
    {
        get
        {
            lock (_sync)
            {
                return _characters.Count;
            }
        }
    }

    public int QuestRecordCount
    {
        get
        {
            lock (_sync)
            {
                return _characters.Values.Sum(item => item.Quests.Count);
            }
        }
    }

    public string TsvPath => AppPaths.CompletedQuestsTsv;

    public static bool TryGetCharacterIdentity(string path, out string realm, out string character)
    {
        realm = string.Empty;
        character = string.Empty;

        var file = new FileInfo(path);
        if (!string.Equals(file.Name, "AzerothQuesting.lua", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var savedVariables = file.Directory;
        var characterDirectory = savedVariables?.Parent;
        var realmDirectory = characterDirectory?.Parent;
        var accountDirectory = realmDirectory?.Parent;
        var accountRoot = accountDirectory?.Parent;

        if (savedVariables is null
            || !string.Equals(savedVariables.Name, "SavedVariables", StringComparison.OrdinalIgnoreCase)
            || characterDirectory is null
            || realmDirectory is null
            || accountDirectory is null
            || accountRoot is null
            || !string.Equals(accountRoot.Name, "Account", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        character = characterDirectory.Name;
        realm = realmDirectory.Name;
        return !string.IsNullOrWhiteSpace(character) && !string.IsNullOrWhiteSpace(realm);
    }

    public async Task<CompletedQuestImportResult> ImportFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCharacterIdentity(path, out var realm, out var character))
        {
            return new CompletedQuestImportResult(false, false, string.Empty, string.Empty, 0);
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return ImportSnapshot(text, realm, character);
    }

    private CompletedQuestImportResult ImportSnapshot(string text, string realm, string character)
    {
        CompletedQuestCharacterSnapshot? newest = null;
        foreach (Match match in WireRegex.Matches(text))
        {
            var parsed = ParseWire(match.Value, realm, character);
            if (parsed is not null && (newest is null || parsed.CapturedAt > newest.CapturedAt))
            {
                newest = parsed;
            }
        }

        if (newest is null)
        {
            return new CompletedQuestImportResult(false, false, character, realm, 0);
        }

        lock (_sync)
        {
            var key = realm + "\0" + character;
            if (_characters.TryGetValue(key, out var existing) && existing.CapturedAt > newest.CapturedAt)
            {
                return new CompletedQuestImportResult(true, false, character, realm, existing.Quests.Count);
            }

            var changed = existing is null || !SnapshotsEqual(existing, newest);
            if (changed)
            {
                _characters[key] = newest;
                PersistUnlocked();
            }

            return new CompletedQuestImportResult(true, changed, character, realm, newest.Quests.Count);
        }
    }

    private static CompletedQuestCharacterSnapshot? ParseWire(string wire, string realm, string character)
    {
        var parts = wire.Split('|', 8, StringSplitOptions.None);
        if (parts.Length != 8 || parts[0] != "AQC1" || parts[1] != "1")
        {
            return null;
        }

        if (!int.TryParse(parts[4], out var level) || level < 0 || level > 255
            || !long.TryParse(parts[5], out var capturedUnix) || capturedUnix <= 0)
        {
            return null;
        }

        DateTimeOffset capturedAt;
        try
        {
            capturedAt = DateTimeOffset.FromUnixTimeSeconds(capturedUnix);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }

        var quests = new Dictionary<int, string>();
        if (!string.IsNullOrWhiteSpace(parts[7]))
        {
            foreach (var item in parts[7].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = item.Split('~', 2, StringSplitOptions.None);
                if (pair.Length != 2 || !int.TryParse(pair[0], out var questId) || questId <= 0)
                {
                    continue;
                }

                quests[questId] = DecodeHex(pair[1]);
            }
        }

        return new CompletedQuestCharacterSnapshot
        {
            Character = character,
            Realm = realm,
            Faction = parts[2],
            ClassFile = parts[3],
            Level = level,
            CapturedAt = capturedAt,
            AddonVersion = parts[6],
            Quests = quests,
        };
    }

    private static string DecodeHex(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length % 2 != 0)
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromHexString(value));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static bool SnapshotsEqual(
        CompletedQuestCharacterSnapshot left,
        CompletedQuestCharacterSnapshot right)
    {
        if (!string.Equals(left.Character, right.Character, StringComparison.Ordinal)
            || !string.Equals(left.Realm, right.Realm, StringComparison.Ordinal)
            || !string.Equals(left.Faction, right.Faction, StringComparison.Ordinal)
            || !string.Equals(left.ClassFile, right.ClassFile, StringComparison.Ordinal)
            || left.Level != right.Level
            || left.CapturedAt != right.CapturedAt
            || !string.Equals(left.AddonVersion, right.AddonVersion, StringComparison.Ordinal)
            || left.Quests.Count != right.Quests.Count)
        {
            return false;
        }

        foreach (var pair in left.Quests)
        {
            if (!right.Quests.TryGetValue(pair.Key, out var name)
                || !string.Equals(pair.Value, name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void Load()
    {
        if (!File.Exists(AppPaths.CompletedQuestsFile))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(AppPaths.CompletedQuestsFile);
            var document = JsonSerializer.Deserialize<CompletedQuestStoreDocument>(json, JsonOptions);
            foreach (var snapshot in document?.Characters ?? [])
            {
                if (string.IsNullOrWhiteSpace(snapshot.Character) || string.IsNullOrWhiteSpace(snapshot.Realm))
                {
                    continue;
                }

                snapshot.Quests ??= new Dictionary<int, string>();
                _characters[snapshot.Realm + "\0" + snapshot.Character] = snapshot;
            }
        }
        catch (JsonException)
        {
            // Keep a damaged local cache from preventing the Companion from starting.
        }
        catch (IOException)
        {
            // A later SavedVariables scan can rebuild the local cache.
        }
    }

    private void PersistUnlocked()
    {
        AppPaths.EnsureCreated();
        var document = new CompletedQuestStoreDocument
        {
            Schema = 1,
            Characters = _characters.Values
                .OrderBy(item => item.Realm, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Character, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

        var json = JsonSerializer.Serialize(document, JsonOptions);
        var temporary = AppPaths.CompletedQuestsFile + ".tmp";
        File.WriteAllText(temporary, json, Encoding.UTF8);
        File.Move(temporary, AppPaths.CompletedQuestsFile, overwrite: true);
        File.WriteAllText(AppPaths.CompletedQuestsTsv, BuildTsvUnlocked(), Encoding.UTF8);
    }

    private string BuildTsvUnlocked()
    {
        var lines = new List<string>
        {
            "Character\tRealm\tFaction\tClass\tLevel\tQuest ID\tQuest Name\tCaptured At\tAddon Version",
        };

        foreach (var snapshot in _characters.Values
                     .OrderBy(item => item.Realm, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Character, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var quest in snapshot.Quests.OrderBy(item => item.Key))
            {
                lines.Add(string.Join('\t',
                    SafeField(snapshot.Character),
                    SafeField(snapshot.Realm),
                    SafeField(snapshot.Faction),
                    SafeField(snapshot.ClassFile),
                    snapshot.Level.ToString(),
                    quest.Key.ToString(),
                    SafeField(quest.Value),
                    snapshot.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"),
                    SafeField(snapshot.AddonVersion)));
            }
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string SafeField(string? value)
    {
        return (value ?? string.Empty)
            .Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    private sealed class CompletedQuestStoreDocument
    {
        public int Schema { get; set; } = 1;
        public List<CompletedQuestCharacterSnapshot> Characters { get; set; } = [];
    }

    private sealed class CompletedQuestCharacterSnapshot
    {
        public string Character { get; set; } = string.Empty;
        public string Realm { get; set; } = string.Empty;
        public string Faction { get; set; } = string.Empty;
        public string ClassFile { get; set; } = string.Empty;
        public int Level { get; set; }
        public DateTimeOffset CapturedAt { get; set; }
        public string AddonVersion { get; set; } = string.Empty;
        public Dictionary<int, string> Quests { get; set; } = new();
    }
}
