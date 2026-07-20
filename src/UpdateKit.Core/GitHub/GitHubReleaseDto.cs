using System.Text.Json.Serialization;

namespace UpdateKit.GitHub;

internal sealed class GitHubReleaseDto
{
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("tag_name")]
    public string? TagName { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }

    [JsonPropertyName("prerelease")]
    public bool? IsPrerelease { get; init; }

    [JsonPropertyName("draft")]
    public bool? IsDraft { get; init; }

    [JsonPropertyName("assets")]
    public IReadOnlyList<GitHubReleaseAssetDto>? Assets { get; init; }
}

internal sealed class GitHubReleaseAssetDto
{
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("url")]
    public string? ApiUrl { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; init; }
}

internal sealed class GitHubErrorDto
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("documentation_url")]
    public string? DocumentationUrl { get; init; }
}
