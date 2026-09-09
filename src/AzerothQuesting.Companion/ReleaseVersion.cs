namespace AzerothQuesting.Companion;

internal readonly record struct ParsedReleaseVersion(
    int Major,
    int Minor,
    int Patch,
    int Revision,
    string? PreRelease) : IComparable<ParsedReleaseVersion>
{
    public int CompareTo(ParsedReleaseVersion other)
    {
        var numeric = Major.CompareTo(other.Major);
        if (numeric != 0) return numeric;
        numeric = Minor.CompareTo(other.Minor);
        if (numeric != 0) return numeric;
        numeric = Patch.CompareTo(other.Patch);
        if (numeric != 0) return numeric;
        numeric = Revision.CompareTo(other.Revision);
        if (numeric != 0) return numeric;

        if (PreRelease is null && other.PreRelease is null) return 0;
        if (PreRelease is null) return 1;
        if (other.PreRelease is null) return -1;

        var left = PreRelease.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var right = other.PreRelease.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var count = Math.Min(left.Length, right.Length);

        for (var i = 0; i < count; i++)
        {
            var leftNumeric = int.TryParse(left[i], out var leftNumber);
            var rightNumeric = int.TryParse(right[i], out var rightNumber);

            int result;
            if (leftNumeric && rightNumeric)
            {
                result = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric)
            {
                result = -1;
            }
            else if (rightNumeric)
            {
                result = 1;
            }
            else
            {
                result = string.Compare(left[i], right[i], StringComparison.OrdinalIgnoreCase);
            }

            if (result != 0)
            {
                return result;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}

internal static class ReleaseVersionUtility
{
    public static bool TryParse(string? value, out ParsedReleaseVersion version)
    {
        version = default;
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var buildIndex = normalized.IndexOf('+');
        if (buildIndex >= 0)
        {
            normalized = normalized[..buildIndex];
        }

        string? preRelease = null;
        var prereleaseIndex = normalized.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            preRelease = normalized[(prereleaseIndex + 1)..].Trim();
            normalized = normalized[..prereleaseIndex];
            if (string.IsNullOrWhiteSpace(preRelease))
            {
                return false;
            }
        }

        var parts = normalized.Split('.', StringSplitOptions.None);
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
            {
                return false;
            }
        }

        version = new ParsedReleaseVersion(
            numbers[0],
            numbers[1],
            numbers[2],
            numbers[3],
            preRelease);
        return true;
    }

    public static bool IsNewer(string current, string candidate)
    {
        return Compare(candidate, current) > 0;
    }

    public static bool Matches(string left, string right)
    {
        return Compare(left, right) == 0;
    }

    public static int Compare(string? left, string? right)
    {
        var leftParsed = TryParse(left, out var leftVersion);
        var rightParsed = TryParse(right, out var rightVersion);

        if (leftParsed && rightParsed)
        {
            return leftVersion.CompareTo(rightVersion);
        }

        if (leftParsed) return 1;
        if (rightParsed) return -1;

        return string.Compare(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        foreach (var prefix in new[] { "companion-v", "addon-v", "azerothquesting-v" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[prefix.Length..];
            }
        }

        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            return value[1..];
        }

        return value;
    }
}
