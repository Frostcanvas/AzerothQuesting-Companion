using System.Text.Json.Serialization;

namespace AzerothQuesting.Companion;

internal sealed class ResearchDashboardData
{
    [JsonPropertyName("summary")]
    public ResearchSummary Summary { get; set; } = new();

    [JsonPropertyName("your_installation")]
    public ResearchInstallationSummary YourInstallation { get; set; } = new();

    [JsonPropertyName("recent_observations")]
    public List<ResearchObservationRow> RecentObservations { get; set; } = [];

    [JsonPropertyName("class_evidence")]
    public List<ResearchClassEvidenceRow> ClassEvidence { get; set; } = [];

    [JsonPropertyName("addon_versions")]
    public List<ResearchVersionRow> AddonVersions { get; set; } = [];

    [JsonPropertyName("companion_versions")]
    public List<ResearchVersionRow> CompanionVersions { get; set; } = [];

    [JsonPropertyName("generated_at")]
    public DateTimeOffset GeneratedAt { get; set; }
}

internal sealed class ResearchSummary
{
    [JsonPropertyName("total_observations")]
    public int TotalObservations { get; set; }

    [JsonPropertyName("unique_quests")]
    public int UniqueQuests { get; set; }

    [JsonPropertyName("installations")]
    public int Installations { get; set; }

    [JsonPropertyName("connected_installations")]
    public int ConnectedInstallations { get; set; }

    [JsonPropertyName("active_installations_30d")]
    public int ActiveInstallations30Days { get; set; }

    [JsonPropertyName("last_received_at")]
    public DateTimeOffset? LastReceivedAt { get; set; }
}

internal sealed class ResearchInstallationSummary
{
    [JsonPropertyName("observations")]
    public int Observations { get; set; }

    [JsonPropertyName("unique_quests")]
    public int UniqueQuests { get; set; }

    [JsonPropertyName("last_received_at")]
    public DateTimeOffset? LastReceivedAt { get; set; }
}

internal sealed class ResearchObservationRow
{
    [JsonPropertyName("quest_id")]
    public int QuestId { get; set; }

    [JsonPropertyName("map_id")]
    public int MapId { get; set; }

    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = string.Empty;

    [JsonPropertyName("faction")]
    public string Faction { get; set; } = string.Empty;

    [JsonPropertyName("class_file")]
    public string ClassFile { get; set; } = string.Empty;

    [JsonPropertyName("player_level")]
    public int PlayerLevel { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("addon_version")]
    public string? AddonVersion { get; set; }

    [JsonPropertyName("companion_version")]
    public string? CompanionVersion { get; set; }

    [JsonPropertyName("observed_at")]
    public DateTimeOffset? ObservedAt { get; set; }

    [JsonPropertyName("received_at")]
    public DateTimeOffset ReceivedAt { get; set; }
}

internal sealed class ResearchClassEvidenceRow
{
    [JsonPropertyName("quest_id")]
    public int QuestId { get; set; }

    [JsonPropertyName("map_id")]
    public int MapId { get; set; }

    [JsonPropertyName("faction")]
    public string Faction { get; set; } = string.Empty;

    [JsonPropertyName("class_file")]
    public string ClassFile { get; set; } = string.Empty;

    [JsonPropertyName("observations")]
    public int Observations { get; set; }

    [JsonPropertyName("strong_observations")]
    public int StrongObservations { get; set; }

    [JsonPropertyName("installations")]
    public int Installations { get; set; }

    [JsonPropertyName("any_completed")]
    public bool AnyCompleted { get; set; }

    [JsonPropertyName("last_received_at")]
    public DateTimeOffset LastReceivedAt { get; set; }
}

internal sealed class ResearchVersionRow
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "unknown";

    [JsonPropertyName("installations")]
    public int Installations { get; set; }
}
