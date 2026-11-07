namespace AzerothQuesting.Companion;

internal sealed record WowGameClient(string Folder, string Name, string TocSuffix, int[] ProductIds)
{
    public static readonly WowGameClient[] All =
    [
        new("_retail_", "Retail", "", [1]),
        new("_forever_", "Forever", "_Camelot", [18]),
        new("_classic_era_", "Classic Era", "_Vanilla", [2]),
        new("_anniversary_", "Anniversary", "_TBC", [2, 5]),
        new("_classic_", "Mists Classic", "_Mists", [19]),
    ];

    public override string ToString() => Name;
}
