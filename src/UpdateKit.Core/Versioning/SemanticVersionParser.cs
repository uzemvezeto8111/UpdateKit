using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace UpdateKit;

internal static partial class SemanticVersionParser
{
    private const string SemanticVersionPattern =
        "^(?<major>0|[1-9][0-9]*)\\.(?<minor>0|[1-9][0-9]*)\\.(?<patch>0|[1-9][0-9]*)" +
        "(?:-(?<prerelease>(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)" +
        "(?:\\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?" +
        "(?:\\+(?<build>[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?\\z";

    public static UpdateResult<SemanticVersion> ParseTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return Invalid("A version tag is required.");
        }

        var normalizedTag = tag[0] == 'v' ? tag[1..] : tag;
        var match = SemanticVersionRegex().Match(normalizedTag);

        if (!match.Success)
        {
            return Invalid("The tag is not a valid Semantic Version 2.0.0 value.");
        }

        var version = new SemanticVersion(
            ParseNumericComponent(match.Groups["major"].Value),
            ParseNumericComponent(match.Groups["minor"].Value),
            ParseNumericComponent(match.Groups["patch"].Value),
            GetOptionalGroupValue(match, "prerelease"),
            GetOptionalGroupValue(match, "build"));

        return UpdateResult<SemanticVersion>.Success(version);
    }

    private static BigInteger ParseNumericComponent(string value) =>
        BigInteger.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

    private static string? GetOptionalGroupValue(Match match, string groupName)
    {
        var group = match.Groups[groupName];
        return group.Success ? group.Value : null;
    }

    private static UpdateResult<SemanticVersion> Invalid(string message) =>
        UpdateResult<SemanticVersion>.Failure(
            new UpdateError(UpdateErrorCode.InvalidVersion, message));

    [GeneratedRegex(
        SemanticVersionPattern,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SemanticVersionRegex();
}
