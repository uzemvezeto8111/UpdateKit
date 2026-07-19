# Getting started

This guide walks through the complete UpdateKit workflow. It assumes .NET 8 and a GitHub repository whose release tags follow Semantic Versioning 2.0.0.

## 1. Reference UpdateKit

Until packages are published to a feed, add project references to the cloned source:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/UpdateKit/src/UpdateKit.Core/UpdateKit.Core.csproj" />
</ItemGroup>
```

For Windows Forms hosts, also reference:

```xml
<ProjectReference Include="path/to/UpdateKit/src/UpdateKit.WinForms/UpdateKit.WinForms.csproj" />
```

## 2. Configure the client

```csharp
using UpdateKit;

var httpClient = new HttpClient();
var client = new UpdateClient(
    httpClient,
    new UpdateClientOptions
    {
        RepositoryOwner = "owner",
        RepositoryName = "repository",
        AccessToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
        IncludePrereleases = false,
        UserAgent = "MyProduct-Updater",
        RequestTimeout = TimeSpan.FromSeconds(30),
    });
```

`UpdateClient` borrows the supplied `HttpClient`; it never disposes it. The host must keep the client alive until all release, download, and checksum-file operations finish. An optional token is useful for private repository metadata or higher API limits, but it is not persisted or forwarded to arbitrary asset hosts.

## 3. Check for an update

```csharp
var result = await client.CheckForUpdateAsync("1.4.0", cancellationToken);
if (!result.IsSuccess)
{
    ShowError(result.Error);
    return;
}

var check = result.Value;
if (!check.IsUpdateAvailable)
{
    Console.WriteLine($"Already current; latest is {check.LatestRelease.TagName}.");
    return;
}

Console.WriteLine($"Update available: {check.LatestRelease.TagName}");
Console.WriteLine(check.LatestRelease.Body);
```

Tags may be `1.4.1` or `v1.4.1`. An uppercase `V`, whitespace, missing version components, or other non-SemVer text produces `InvalidVersion`. Build metadata does not affect precedence. Drafts are ignored, and prereleases require `IncludePrereleases = true`.

## 4. Select an asset

```csharp
var assetResult = client.SelectAssetByExactName(
    check.LatestRelease,
    "MyProduct-win-x64.zip");

// Alternatives:
// client.SelectAssetByExtension(check.LatestRelease, ".zip");
// client.SelectAssetByPredicate(check.LatestRelease, asset => asset.Name.Contains("win-x64"));

if (!assetResult.IsSuccess)
{
    ShowError(assetResult.Error);
    return;
}

var asset = assetResult.Value;
```

Every selector returns the first match in release-asset order. Exact names and predicate behavior are case-sensitive unless the predicate chooses otherwise. Extension matching is case-insensitive and accepts either `zip` or `.zip`.

## 5. Download with progress and cancellation

```csharp
var destination = Path.GetFullPath("MyProduct-win-x64.zip");
using var cancellation = new CancellationTokenSource();
var progress = new Progress<DownloadProgress>(value =>
{
    if (value.Percentage is { } percentage)
    {
        Console.WriteLine($"{percentage:F1}% ({value.BytesDownloaded:N0} bytes)");
    }
    else
    {
        Console.WriteLine($"{value.BytesDownloaded:N0} bytes");
    }
});

var download = await client.DownloadAsync(
    asset,
    destination,
    progress,
    cancellation.Token);

if (!download.IsSuccess)
{
    ShowError(download.Error);
    return;
}
```

The parent directory must already exist. UpdateKit streams to a unique temporary file beside the destination, then commits the destination only after success. Cancellation is reported as `DownloadCanceled`, temporary-file cleanup is attempted on handled failures, and a pre-existing destination remains intact if the operation fails before replacement. Cleanup is best-effort if another process locks the temporary file or permissions change during the operation.

## 6. Verify a direct checksum

Replace the plain download call when a trusted source supplies the expected hash:

```csharp
var download = await client.DownloadAndVerifyAsync(
    asset,
    destination,
    expectedSha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    progress,
    cancellation.Token);
```

The value must contain exactly 64 hexadecimal characters. A mismatch returns `ChecksumMismatch` and removes the downloaded file.

## 7. Verify through a checksum-file asset

```csharp
var checksumAssetResult = client.SelectAssetByExactName(
    check.LatestRelease,
    "SHA256SUMS.txt");

if (!checksumAssetResult.IsSuccess)
{
    ShowError(checksumAssetResult.Error);
    return;
}

var download = await client.DownloadAndVerifyFromChecksumFileAsync(
    asset,
    destination,
    checksumAssetResult.Value,
    progress,
    cancellation.Token);
```

A supported checksum-file entry looks like either of these:

```text
0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  MyProduct-win-x64.zip
0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef *MyProduct-win-x64.zip
```

Filenames can contain spaces. Matching is ordinal and case-sensitive. Missing entries return `ChecksumNotFound`; malformed or conflicting entries return `InvalidChecksum`.

## 8. Host the Windows Forms dialog

The reusable dialog composes all previous steps:

```csharp
using UpdateKit.WinForms;

var options = new UpdateDialogOptions(
    client,
    currentVersion: "1.4.0",
    destinationFilePath: destination,
    assetSelector: release => client.SelectAssetByExtension(release, ".zip"))
{
    DialogTitle = "MyProduct Update",
    CheckForUpdateOnShown = true,
    ChecksumAssetSelector = release =>
        client.SelectAssetByExactName(release, "SHA256SUMS.txt"),
};

using var dialog = new UpdateDialog(options);
dialog.ShowDialog(this);

if (dialog.DownloadResult is { } completed)
{
    Console.WriteLine($"Saved {completed.BytesDownloaded:N0} bytes to {completed.FilePath}.");
}
else if (dialog.LastError is { } error)
{
    ShowError(error);
}
```

Use `ExpectedSha256` instead of `ChecksumAssetSelector` for a direct checksum. A dialog instance is single-use. Create a new instance each time, show it with the host form as owner, and dispose it afterward. It prevents duplicate operations and safely cancels active work when the user closes it.

## 9. Handle errors consistently

```csharp
static void ShowError(UpdateError error)
{
    Console.Error.WriteLine($"{error.Code}: {error.Message}");
}
```

Branch on `UpdateErrorCode`, not message text. The nested exception is diagnostic context and may contain implementation details, so avoid displaying or logging it where secrets or sensitive paths could be exposed.

For a complete interactive implementation, see [UpdateKit.Example.WinForms](../samples/UpdateKit.Example.WinForms).
