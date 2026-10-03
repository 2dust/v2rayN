namespace ServiceLib.Models.Dto;

public class SemanticVersion : IEquatable<SemanticVersion>, IComparable
{
    private readonly string? build;
    private readonly int major;
    private readonly int minor;
    private readonly int patch;
    private readonly string? prerelease;
    private readonly string raw;

    public SemanticVersion(int major, int minor, int patch)
    {
        this.major = major;
        this.minor = minor;
        this.patch = patch;
        raw = $"{major}.{minor}.{patch}";
    }

    public SemanticVersion(string? version)
    {
        try
        {
            if (string.IsNullOrEmpty(version))
            {
                major = 0;
                minor = 0;
                patch = 0;
                raw = $"{major}.{minor}.{patch}";
                return;
            }
            raw = version;

            var span = version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? version.AsSpan(1)
                : version.AsSpan();
            var plusIdx = span.IndexOf('+');
            var metadataSpan = plusIdx >= 0 ? span[(plusIdx + 1)..] : [];
            var leftOfPlus = plusIdx >= 0 ? span[..plusIdx] : span;
            var dashIdx = leftOfPlus.IndexOf('-');
            var versionSpan = dashIdx >= 0 ? leftOfPlus[..dashIdx] : leftOfPlus;
            var preReleaseSpan = dashIdx >= 0 ? leftOfPlus[(dashIdx + 1)..] : [];

            build = metadataSpan.Length > 0 ? metadataSpan.ToString() : null;
            prerelease = preReleaseSpan.Length > 0 ? preReleaseSpan.ToString() : null;

            var parts = versionSpan.ToString().Split('.');
            switch (parts.Length)
            {
                case 2:
                    major = int.Parse(parts.First());
                    minor = int.Parse(parts.Last());
                    patch = 0;
                    break;

                case 3 or 4:
                    major = int.Parse(parts[0]);
                    minor = int.Parse(parts[1]);
                    patch = int.Parse(parts[2]);
                    break;

                default:
                    throw new ArgumentException("Invalid version string");
            }
        }
        catch
        {
            major = 0;
            minor = 0;
            patch = 0;
            raw = $"{major}.{minor}.{patch}";
        }
    }

    public bool Equals(SemanticVersion? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        return major == other.major && minor == other.minor && patch == other.patch && prerelease == other.prerelease;
    }

    public static bool TryParse(string? version, out SemanticVersion? result)
    {
        try
        {
            result = new SemanticVersion(version);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is SemanticVersion other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(major, minor, patch, prerelease);
    }

    public override string ToString()
    {
        return raw;
    }

    public string ToStandardVersionString(string? prefix = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(prefix))
        {
            sb.Append(prefix);
        }
        sb.Append($"{major}.{minor}.{patch}");
        if (!string.IsNullOrEmpty(prerelease))
        {
            sb.Append($"-{prerelease}");
        }
        if (!string.IsNullOrEmpty(build))
        {
            sb.Append($"+{build}");
        }
        return sb.ToString();
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(SemanticVersion left, SemanticVersion right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(SemanticVersion left, SemanticVersion right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(SemanticVersion left, SemanticVersion right)
    {
        return left.CompareTo(right) >= 0;
    }

    #region Private

    public int CompareTo(SemanticVersion other)
    {
        if (major != other.major)
        {
            return major.CompareTo(other.major);
        }
        if (minor != other.minor)
        {
            return minor.CompareTo(other.minor);
        }
        if (patch != other.patch)
        {
            return patch.CompareTo(other.patch);
        }
        return ComparePreRelease(prerelease, other.prerelease);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }
        if (obj is SemanticVersion other)
        {
            return CompareTo(other);
        }
        throw new ArgumentException("Object is not a SemanticVersion");
    }

    private static int ComparePreRelease(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
        {
            return 0;
        }
        if (string.IsNullOrEmpty(left))
        {
            return 1;
        }
        if (string.IsNullOrEmpty(right))
        {
            return -1;
        }

        var leftSpan = left.AsSpan();
        var rightSpan = right.AsSpan();
        using var leftEnum = leftSpan.Split('.').GetEnumerator();
        using var rightEnum = rightSpan.Split('.').GetEnumerator();

        while (true)
        {
            var hasLeft = leftEnum.MoveNext();
            var hasRight = rightEnum.MoveNext();

            if (!hasLeft && !hasRight)
            {
                return 0;
            }
            if (!hasLeft)
            {
                return -1;
            }
            if (!hasRight)
            {
                return 1;
            }

            var leftSegment = leftSpan[leftEnum.Current];
            var rightSegment = rightSpan[rightEnum.Current];

            var segCmp = CompareSegment(leftSegment, rightSegment);
            if (segCmp != 0)
            {
                return segCmp;
            }
        }
    }

    private static int CompareSegment(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var leftIsNum = ulong.TryParse(left, out var leftNum);
        var rightIsNum = ulong.TryParse(right, out var rightNum);

        return (leftIsNum, rightIsNum) switch
        {
            (true, true) => leftNum.CompareTo(rightNum),

            (false, true) => 1,
            (true, false) => -1,

            (false, false) => left.SequenceCompareTo(right),
        };
    }

    #endregion Private
}
