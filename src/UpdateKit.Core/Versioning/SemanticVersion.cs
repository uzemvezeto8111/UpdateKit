using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace UpdateKit;

public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private readonly string[] _prereleaseIdentifiers;

    internal SemanticVersion(
        BigInteger major,
        BigInteger minor,
        BigInteger patch,
        string? prerelease,
        string? buildMetadata)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        BuildMetadata = buildMetadata;
        _prereleaseIdentifiers = prerelease?.Split('.') ?? [];
    }

    public BigInteger Major { get; }

    public BigInteger Minor { get; }

    public BigInteger Patch { get; }

    public string? Prerelease { get; }

    public string? BuildMetadata { get; }

    public bool IsPrerelease => Prerelease is not null;

    public static UpdateResult<SemanticVersion> ParseTag(string? tag) =>
        SemanticVersionParser.ParseTag(tag);

    public static bool TryParseTag(
        string? tag,
        [NotNullWhen(true)] out SemanticVersion? version)
    {
        var result = ParseTag(tag);
        version = result.IsSuccess ? result.Value : null;
        return result.IsSuccess;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var coreComparison = Major.CompareTo(other.Major);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        coreComparison = Minor.CompareTo(other.Minor);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        coreComparison = Patch.CompareTo(other.Patch);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        return ComparePrereleaseIdentifiers(_prereleaseIdentifiers, other._prereleaseIdentifiers);
    }

    public bool Equals(SemanticVersion? other) =>
        ReferenceEquals(this, other) || other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) =>
        obj is SemanticVersion other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Major);
        hash.Add(Minor);
        hash.Add(Patch);

        foreach (var identifier in _prereleaseIdentifiers)
        {
            hash.Add(identifier, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        var normalized = string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");

        if (Prerelease is not null)
        {
            normalized += $"-{Prerelease}";
        }

        if (BuildMetadata is not null)
        {
            normalized += $"+{BuildMetadata}";
        }

        return normalized;
    }

    private static int ComparePrereleaseIdentifiers(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        if (left.Count == 0)
        {
            return right.Count == 0 ? 0 : 1;
        }

        if (right.Count == 0)
        {
            return -1;
        }

        var sharedLength = Math.Min(left.Count, right.Count);
        for (var index = 0; index < sharedLength; index++)
        {
            var identifierComparison = ComparePrereleaseIdentifier(left[index], right[index]);
            if (identifierComparison != 0)
            {
                return identifierComparison;
            }
        }

        return left.Count.CompareTo(right.Count);
    }

    private static int ComparePrereleaseIdentifier(string left, string right)
    {
        var leftIsNumeric = IsNumericIdentifier(left);
        var rightIsNumeric = IsNumericIdentifier(right);

        if (leftIsNumeric && rightIsNumeric)
        {
            var lengthComparison = left.Length.CompareTo(right.Length);
            return lengthComparison != 0
                ? lengthComparison
                : string.CompareOrdinal(left, right);
        }

        if (leftIsNumeric != rightIsNumeric)
        {
            return leftIsNumeric ? -1 : 1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static bool IsNumericIdentifier(string value)
    {
        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
