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

For WPF hosts, reference the WPF package instead:

```xml
<ProjectReference Include="path/to/UpdateKit/src/UpdateKit.Wpf/UpdateKit.Wpf.csproj" />
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
        DownloadRetry = new DownloadRetryOptions
        {
            MaxRetryAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaximumDelay = TimeSpan.FromSeconds(8),
            JitterFactor = 0.2,
            RetryProgress = new Progress<DownloadRetryAttempt>(attempt =>
                Console.WriteLine(
                    $"Retry {attempt.RetryNumber}/{attempt.MaximumRetryAttempts} " +
                    $"after {attempt.StatusCode?.ToString() ?? attempt.Exception?.GetType().Name}")),
        },
    });
```

`UpdateClient` borrows the supplied `HttpClient`; it never disposes it. The host must keep the client alive until all release, download, and checksum-file operations finish. An optional token enables private repository metadata and authenticated release-asset downloads, but it is not persisted or forwarded to arbitrary asset hosts.

For a private asset, UpdateKit validates GitHub's asset API URL against `https://api.github.com/repos/{owner}/{repository}/releases/assets/{id}` and sends the token only on the initial verified API request. Redirect requests constructed by UpdateKit do not receive library-added credentials or GitHub API headers, and an HTTPS download cannot redirect to plain HTTP. Public downloads without a token continue to use `browser_download_url` without authentication. Checksum-file assets follow the same rule.

Do not set a token on `HttpClient.DefaultRequestHeaders`; defaults belong to the caller and can affect unrelated requests. Custom handlers are also inside the host's security boundary and must preserve .NET's rule that authorization is not copied across redirects to another host.

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
    "MyProduct-setup.exe");

// Alternatives:
// client.SelectAssetByExtension(check.LatestRelease, ".msi");
// client.SelectAssetByExtension(check.LatestRelease, "nupkg");
// client.SelectAssetByExtension(check.LatestRelease, ".tar.gz");
// client.SelectAssetByPredicate(check.LatestRelease, asset => asset.Name.Contains("win-x64"));

if (!assetResult.IsSuccess)
{
    ShowError(assetResult.Error);
    return;
}

var asset = assetResult.Value;
```

Every selector returns the first match in release-asset order. Exact names and predicate behavior are case-sensitive unless the predicate chooses otherwise. Extension matching is case-insensitive, accepts values with or without a leading dot, and supports arbitrary and multi-part suffixes such as `.exe`, `.msi`, `.nupkg`, `.7z`, and `.tar.gz`. The selected asset's original filename is preserved.

## 5. Download with progress and cancellation

```csharp
var destination = Path.GetFullPath(asset.Name);
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

The Core download API requires an explicit file path whose parent directory already exists. UpdateKit streams to a unique temporary file beside the destination, then commits the destination only after success. Cancellation is reported as `DownloadCanceled`, temporary-file cleanup is attempted on handled failures, and a pre-existing destination remains intact if the operation fails before replacement. Cleanup is best-effort if another process locks the temporary file or permissions change during the operation.

UpdateKit only downloads the asset bytes. It does not install or execute installers, run scripts, or extract archives. Host applications decide what to do with the successfully downloaded file.

Retries are disabled by default. `MaxRetryAttempts` counts retries after the initial request, so `3` allows no more than four total attempts. The valid range is 0–100 retries; delays must be non-negative, no greater than `Int32.MaxValue` milliseconds, and ordered so `MaximumDelay >= InitialDelay`; jitter must be finite and between 0 and 1. Invalid settings throw `UpdateConfigurationException` when the client or standalone downloader is constructed.

UpdateKit retries only HTTP `408`, `429`, `500`, `502`, `503`, and `504`, transport `HttpRequestException` failures without a permanent status, and failures opening or reading the response stream. It never retries cancellation, invalid configuration, invalid redirects, authentication failures, other HTTP responses, destination file failures, checksum errors, or checksum mismatches.

The delay before one-based retry `n` is `min(MaximumDelay, InitialDelay × 2^(n-1))`. Optional symmetric jitter multiplies that delay by a random value in `[1 - JitterFactor, 1 + JitterFactor]` and reapplies the maximum bound. `RetryProgress` runs immediately before the delay. Cancellation during backoff returns `DownloadCanceled` without starting another request.

Retries are full transfers, not resumptions. Each attempt restarts at byte zero with a new request and unique temporary file; `DownloadProgress` restarts accordingly. Failed temporary files are cleaned up before retrying, and the final destination is still committed only after a complete successful transfer.

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

The value must contain exactly 64 hexadecimal characters. A mismatch returns `ChecksumMismatch` and removes the downloaded file. A match verifies integrity against the supplied digest but does not establish publisher identity.

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
0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  MyProduct-setup.exe
0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef *MyProduct-setup.exe
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
    assetSelector: release => client.SelectAssetByExtension(release, ".exe"))
{
    DialogTitle = "MyProduct Update",
    CheckForUpdateOnShown = true,
    Theme = ApplicationTheme.System,
    ConfirmBeforeDownload = true,
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

`DestinationFilePath` may be an explicit full file path or an existing directory. Explicit paths are preserved; for a directory, the dialog appends the selected asset's original filename. The directory must already exist.

For eligible GitHub releases, the standard dialog displays **View release** before the download action. It opens only a validated, credential-free GitHub HTTPS release page; launch failures are shown without disabling the update workflow.

`Theme` is optional. Leaving it unset retains the native appearance used by existing hosts. `ApplicationTheme.System` resolves the current Windows application theme, while `Light` and `Dark` select explicit UpdateKit palettes. Hosts may also call `WinFormsThemeManager.ApplyTheme(formOrControl, theme)` to apply the same centralized palette to their own WinForms tree. `ConfirmBeforeDownload` is also opt-in and defaults to `false` for backward compatibility.

### Example settings and persistence

Run the full example and open **Tools > Settings** to explore persisted host preferences:

```powershell
dotnet run --project samples/UpdateKit.Example.WinForms/UpdateKit.Example.WinForms.csproj
```

To create a self-contained, single-file Windows x64 build for users who do not have .NET installed, run `eng\publish-example.cmd` from the repository root. It publishes the clickable executable to `artifacts\publish\UpdateKit.Example.WinForms\win-x64\UpdateKit.Example.WinForms.exe`. The publish uses Release configuration, omits debug symbols, and deliberately keeps trimming disabled. See the root [README](../README.md#publish-a-standalone-windows-executable) for the exact underlying command.

The example stores versioned JSON at `%LocalAppData%\UpdateKit\Example.WinForms\settings.json`. It can remember appearance, prerelease and startup-check preferences, download confirmation and folder-opening choices, repository fields, asset selection, destination directories, and bounded retry values. Missing, malformed, partial, unreadable, and future-version files fall back to safe defaults. Saves use a temporary file followed by atomic replacement, and **access tokens are never represented in or written to the settings file**. Use **Clear saved settings** in the dialog to reset the current UI and remove the saved file after confirmation.

## 9. Host the WPF update window

The WPF window uses the same Core workflow and host-selected asset and verification strategies:

```csharp
using UpdateKit.Wpf;

var options = new UpdateWindowOptions(
    client,
    currentVersion: "1.4.0",
    destinationFilePath: destination,
    assetSelector: release => client.SelectAssetByExactName(release, "MyProduct-setup.exe"))
{
    WindowTitle = "MyProduct Update",
    CheckForUpdateOnLoaded = true,
    ChecksumAssetSelector = release =>
        client.SelectAssetByExactName(release, "SHA256SUMS.txt"),
};

var window = new UpdateWindow(options)
{
    Owner = this,
};
window.ShowDialog();

if (window.DownloadResult is { } completed)
{
    Console.WriteLine($"Saved {completed.BytesDownloaded:N0} bytes to {completed.FilePath}.");
}
else if (window.LastError is { } error)
{
    ShowError(error);
}
```

Use a new window for every display. It borrows the `UpdateClient`, blocks duplicate operations, and cancels active work before closing. Its destination follows the same explicit-file-or-existing-directory rule as the WinForms dialog. For custom MVVM rendering, bind to `UpdateWindowViewModel`; it exposes release and asset details, presentation text, progress, errors, state flags, and separate check, download, view-release, primary-action, and cancellation commands. `IsViewReleaseVisible` and `CanViewRelease` support custom rendering of the HTTPS-only GitHub release-page action.

The minimal complete host is [UpdateKit.Minimal.Wpf](../samples/UpdateKit.Minimal.Wpf).

## 10. Handle errors consistently

```csharp
static void ShowError(UpdateError error)
{
    Console.Error.WriteLine($"{error.Code}: {error.Message}");
}
```

Branch on `UpdateErrorCode`, not message text. The nested exception is diagnostic context and may contain implementation details, so avoid displaying or logging it where secrets or sensitive paths could be exposed.

For a complete interactive implementation, see [UpdateKit.Example.WinForms](../samples/UpdateKit.Example.WinForms).
