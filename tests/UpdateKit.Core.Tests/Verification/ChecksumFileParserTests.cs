namespace UpdateKit.Core.Tests.Verification;

public sealed class ChecksumFileParserTests
{
    private static readonly string FirstChecksum = new('a', 64);
    private static readonly string SecondChecksum = new('B', 64);

    [Fact]
    public void FindSha256_FindsExactTargetAmongMultipleEntries()
    {
        var content = $"\n{FirstChecksum}  other.zip\r\n{SecondChecksum}\tUpdateKit.zip\n";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit.zip");

        Assert.True(result.IsSuccess);
        Assert.Equal(Convert.FromHexString(SecondChecksum), result.Value);
    }

    [Fact]
    public void FindSha256_AcceptsBinaryMarkerAndFilenameContainingSpaces()
    {
        var content = $"{FirstChecksum} *UpdateKit portable package.zip";

        var result = ChecksumFileParser.FindSha256(
            content,
            "UpdateKit portable package.zip");

        Assert.True(result.IsSuccess);
        Assert.Equal(Convert.FromHexString(FirstChecksum), result.Value);
    }

    [Fact]
    public void FindSha256_AcceptsUtf8BomAndIdenticalDuplicates()
    {
        var content = $"\uFEFF{FirstChecksum}  UpdateKit.zip\n{FirstChecksum.ToUpperInvariant()} *UpdateKit.zip";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit.zip");

        Assert.True(result.IsSuccess);
        Assert.Equal(Convert.FromHexString(FirstChecksum), result.Value);
    }

    [Fact]
    public void FindSha256_UsesOrdinalCaseSensitiveFilenameMatching()
    {
        var content = $"{FirstChecksum}  updatekit.zip";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit.zip");

        AssertError(result, UpdateErrorCode.ChecksumNotFound);
    }

    [Fact]
    public void FindSha256_ReturnsNotFoundForEmptyOrBlankFile()
    {
        var result = ChecksumFileParser.FindSha256("\r\n \t\n", "UpdateKit.zip");

        AssertError(result, UpdateErrorCode.ChecksumNotFound);
    }

    [Fact]
    public void FindSha256_ReturnsNotFoundWhenOnlyOtherFilesAreListed()
    {
        var content = $"{FirstChecksum}  other.zip\n{SecondChecksum}  another.zip";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit.zip");

        AssertError(result, UpdateErrorCode.ChecksumNotFound);
    }

    [Theory]
    [InlineData(63)]
    [InlineData(65)]
    public void FindSha256_RejectsMalformedChecksumLength(int checksumLength)
    {
        var content = $"{new string('a', checksumLength)}  UpdateKit.zip";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit.zip");

        AssertError(result, UpdateErrorCode.InvalidChecksum);
    }

    [Fact]
    public void FindSha256_RejectsNonHexadecimalChecksum()
    {
        var content = $"{new string('g', 64)}  UpdateKit.zip";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit.zip");

        AssertError(result, UpdateErrorCode.InvalidChecksum);
    }

    [Theory]
    [InlineData("missing delimiter")]
    [InlineData("# checksum comment")]
    public void FindSha256_RejectsAnyMalformedNonBlankLine(string malformedLine)
    {
        var content = $"{FirstChecksum}  UpdateKit.zip\n{malformedLine}";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit.zip");

        AssertError(result, UpdateErrorCode.InvalidChecksum);
    }

    [Fact]
    public void FindSha256_RejectsConflictingDuplicateEntries()
    {
        var content = $"{FirstChecksum}  UpdateKit.zip\n{SecondChecksum} *UpdateKit.zip";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit.zip");

        AssertError(result, UpdateErrorCode.InvalidChecksum);
        Assert.Contains("conflicting", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindSha256_RejectsConflictingDuplicatesForUnrelatedFiles()
    {
        var content =
            $"{FirstChecksum}  other.zip\n{SecondChecksum}  other.zip\n{FirstChecksum}  UpdateKit.zip";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit.zip");

        AssertError(result, UpdateErrorCode.InvalidChecksum);
    }

    [Fact]
    public void FindSha256_RejectsControlCharactersInFilename()
    {
        var content = $"{FirstChecksum}  UpdateKit\tpackage.zip";

        var result = ChecksumFileParser.FindSha256(content, "UpdateKit\tpackage.zip");

        AssertError(result, UpdateErrorCode.InvalidChecksum);
    }

    private static void AssertError(
        UpdateResult<byte[]> result,
        UpdateErrorCode expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }
}
