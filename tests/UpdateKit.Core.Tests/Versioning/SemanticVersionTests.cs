using System.Numerics;

namespace UpdateKit.Core.Tests.Versioning;

public sealed class SemanticVersionTests
{
    [Fact]
    public void ParseTag_ParsesCoreVersion()
    {
        var result = SemanticVersion.ParseTag("12.34.56");

        Assert.True(result.IsSuccess);
        Assert.Equal(new BigInteger(12), result.Value.Major);
        Assert.Equal(new BigInteger(34), result.Value.Minor);
        Assert.Equal(new BigInteger(56), result.Value.Patch);
        Assert.False(result.Value.IsPrerelease);
        Assert.Null(result.Value.Prerelease);
        Assert.Null(result.Value.BuildMetadata);
        Assert.Equal("12.34.56", result.Value.ToString());
    }

    [Fact]
    public void ParseTag_NormalizesSingleLowercaseVPrefix()
    {
        var result = SemanticVersion.ParseTag("v1.2.3-beta.2+build.45");

        Assert.True(result.IsSuccess);
        Assert.Equal("1.2.3-beta.2+build.45", result.Value.ToString());
        Assert.Equal("beta.2", result.Value.Prerelease);
        Assert.Equal("build.45", result.Value.BuildMetadata);
        Assert.True(result.Value.IsPrerelease);
    }

    [Theory]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0-0.3.7")]
    [InlineData("1.0.0-x.7.z.92")]
    [InlineData("1.0.0-x-y-z.--")]
    [InlineData("1.0.0-alpha+001")]
    [InlineData("1.0.0+20130313144700")]
    [InlineData("1.0.0-beta+exp.sha.5114f85")]
    [InlineData("1.0.0+21AF26D3----117B344092BD")]
    [InlineData("1.0.0+001.000")]
    public void ParseTag_AcceptsValidSemVerExamples(string tag)
    {
        var result = SemanticVersion.ParseTag(tag);

        Assert.True(result.IsSuccess);
        Assert.Equal(tag, result.Value.ToString());
    }

    [Fact]
    public void TryParseTag_ReturnsVersionOnlyForValidInput()
    {
        Assert.True(SemanticVersion.TryParseTag("v2.3.4", out var valid));
        Assert.NotNull(valid);
        Assert.Equal("2.3.4", valid.ToString());

        Assert.False(SemanticVersion.TryParseTag("2.3", out var invalid));
        Assert.Null(invalid);
    }

    [Fact]
    public void CompareTo_FollowsOfficialPrereleasePrecedenceSequence()
    {
        string[] orderedTags =
        [
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11",
            "1.0.0-rc.1",
            "1.0.0",
        ];
        var orderedVersions = orderedTags.Select(Parse).ToArray();

        for (var index = 0; index < orderedVersions.Length - 1; index++)
        {
            Assert.True(
                orderedVersions[index].CompareTo(orderedVersions[index + 1]) < 0,
                $"Expected {orderedTags[index]} to precede {orderedTags[index + 1]}.");
            Assert.True(orderedVersions[index + 1].CompareTo(orderedVersions[index]) > 0);
        }
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("2.0.0", "2.1.0")]
    [InlineData("2.1.0", "2.1.1")]
    [InlineData("1.9.9", "1.10.0")]
    [InlineData("0.99.99", "1.0.0")]
    public void CompareTo_OrdersCoreComponentsNumerically(string lowerTag, string higherTag)
    {
        var lower = Parse(lowerTag);
        var higher = Parse(higherTag);

        Assert.True(lower.CompareTo(higher) < 0);
        Assert.True(higher.CompareTo(lower) > 0);
    }

    [Fact]
    public void CompareTo_UsesAsciiOrdinalOrderForNonNumericPrereleaseIdentifiers()
    {
        var uppercase = Parse("1.0.0-B");
        var lowercase = Parse("1.0.0-a");

        Assert.True(uppercase.CompareTo(lowercase) < 0);
    }

    [Fact]
    public void CompareTo_PlacesNumericPrereleaseBeforeNonNumericPrerelease()
    {
        var numeric = Parse("1.0.0-1");
        var nonNumeric = Parse("1.0.0-alpha");

        Assert.True(numeric.CompareTo(nonNumeric) < 0);
    }

    [Fact]
    public void CompareTo_PlacesLongerPrereleaseAfterMatchingShorterPrerelease()
    {
        var shorter = Parse("1.0.0-alpha");
        var longer = Parse("1.0.0-alpha.1");

        Assert.True(shorter.CompareTo(longer) < 0);
    }

    [Fact]
    public void ComparisonAndEquality_IgnoreBuildMetadata()
    {
        var first = Parse("1.2.3-alpha.1+build.100");
        var second = Parse("1.2.3-alpha.1+build.200");

        Assert.Equal(0, first.CompareTo(second));
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first.ToString(), second.ToString());
    }

    [Fact]
    public void Equality_IsCaseSensitiveForPrereleaseIdentifiers()
    {
        var uppercase = Parse("1.0.0-ALPHA");
        var lowercase = Parse("1.0.0-alpha");

        Assert.NotEqual(uppercase, lowercase);
        Assert.NotEqual(0, uppercase.CompareTo(lowercase));
    }

    [Fact]
    public void CompareTo_ConsidersAnyVersionGreaterThanNull()
    {
        Assert.True(Parse("1.0.0").CompareTo(null) > 0);
    }

    [Fact]
    public void ParseTag_HandlesCoreNumbersBeyondPrimitiveIntegerLimits()
    {
        var smallerComponent = new string('9', 200);
        var largerComponent = $"1{new string('0', 200)}";
        var smaller = Parse($"{smallerComponent}.0.0");
        var larger = Parse($"{largerComponent}.0.0");

        Assert.Equal(BigInteger.Parse(smallerComponent), smaller.Major);
        Assert.Equal(BigInteger.Parse(largerComponent), larger.Major);
        Assert.True(smaller.CompareTo(larger) < 0);
        Assert.Equal($"{largerComponent}.0.0", larger.ToString());
    }

    [Fact]
    public void CompareTo_HandlesPrereleaseNumbersBeyondPrimitiveIntegerLimits()
    {
        var smallerIdentifier = new string('9', 200);
        var largerIdentifier = $"1{new string('0', 200)}";
        var smaller = Parse($"1.0.0-{smallerIdentifier}");
        var larger = Parse($"1.0.0-{largerIdentifier}");

        Assert.True(smaller.CompareTo(larger) < 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 1.0.0")]
    [InlineData("1.0.0 ")]
    [InlineData("1.0.0\n")]
    [InlineData("v1.0.0\r\n")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.01.0")]
    [InlineData("1.0.01")]
    [InlineData("+1.0.0")]
    [InlineData("-1.0.0")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0-alpha..1")]
    [InlineData("1.0.0+build..1")]
    [InlineData("1.0.0-alpha_1")]
    [InlineData("1.0.0+build_1")]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0-alpha.01")]
    [InlineData("1.0.0-alpha+build+other")]
    [InlineData("1.0.0-alpha/beta")]
    [InlineData("1.0.0-α")]
    [InlineData("V1.0.0")]
    [InlineData("vv1.0.0")]
    [InlineData("v")]
    public void ParseTag_MapsInvalidOrUnsupportedTagsToInvalidVersion(string? tag)
    {
        var result = SemanticVersion.ParseTag(tag);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateErrorCode.InvalidVersion, result.Error.Code);
        Assert.Null(result.Error.Exception);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    private static SemanticVersion Parse(string tag) =>
        SemanticVersion.ParseTag(tag).Value;
}
