namespace UpdateKit.Core.Tests.Options;

public sealed class UpdateClientOptionsTests
{
    [Fact]
    public void Defaults_AreStableAndConservative()
    {
        var options = new UpdateClientOptions();

        Assert.Equal(UpdateClientOptions.DefaultUserAgent, options.UserAgent);
        Assert.Equal(UpdateClientOptions.DefaultRequestTimeout, options.RequestTimeout);
        Assert.False(options.IncludePrereleases);
        Assert.Null(options.AccessToken);
    }

    [Fact]
    public void Validate_AcceptsValidConfiguration()
    {
        var options = CreateValidOptions();

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
        Assert.Empty(options.GetValidationErrors());
    }

    [Fact]
    public void Validate_ReportsAllMissingRequiredValues()
    {
        var options = new UpdateClientOptions();

        var exception = Assert.Throws<UpdateConfigurationException>(options.Validate);

        Assert.Contains(
            exception.ValidationErrors,
            error => error.StartsWith(nameof(UpdateClientOptions.RepositoryOwner), StringComparison.Ordinal));
        Assert.Contains(
            exception.ValidationErrors,
            error => error.StartsWith(nameof(UpdateClientOptions.RepositoryName), StringComparison.Ordinal));
        Assert.Equal(2, exception.ValidationErrors.Count);
    }

    [Theory]
    [InlineData("owner/name", "repository", "RepositoryOwner")]
    [InlineData("owner", "folder\\repository", "RepositoryName")]
    public void Validate_RejectsPathSeparators(
        string owner,
        string repository,
        string expectedProperty)
    {
        var options = new UpdateClientOptions
        {
            RepositoryOwner = owner,
            RepositoryName = repository,
        };

        var errors = options.GetValidationErrors();

        Assert.Contains(errors, error => error.StartsWith(expectedProperty, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2147483648d)]
    public void Validate_RejectsUnsupportedTimeouts(double milliseconds)
    {
        var valid = CreateValidOptions();
        var options = new UpdateClientOptions
        {
            RepositoryOwner = valid.RepositoryOwner,
            RepositoryName = valid.RepositoryName,
            RequestTimeout = TimeSpan.FromMilliseconds(milliseconds),
        };

        var errors = options.GetValidationErrors();

        Assert.Contains(
            errors,
            error => error.StartsWith(nameof(UpdateClientOptions.RequestTimeout), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("token\r\nInjected: value")]
    public void Validate_RejectsInvalidAccessTokens(string token)
    {
        var valid = CreateValidOptions();
        var options = new UpdateClientOptions
        {
            RepositoryOwner = valid.RepositoryOwner,
            RepositoryName = valid.RepositoryName,
            AccessToken = token,
        };

        var errors = options.GetValidationErrors();

        Assert.Contains(
            errors,
            error => error.StartsWith(nameof(UpdateClientOptions.AccessToken), StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationErrors_AreReadOnlySnapshots()
    {
        var options = new UpdateClientOptions();

        var errors = options.GetValidationErrors();
        var mutableView = Assert.IsAssignableFrom<IList<string>>(errors);

        Assert.Throws<NotSupportedException>(() => mutableView.Add("another error"));
    }

    private static UpdateClientOptions CreateValidOptions() =>
        new()
        {
            RepositoryOwner = "octocat",
            RepositoryName = "Hello-World",
        };
}
