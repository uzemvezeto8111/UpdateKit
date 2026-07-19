# UpdateKit

UpdateKit is a .NET 8 library for checking GitHub releases, selecting an update asset, downloading it safely, optionally verifying its SHA-256 checksum, and presenting the workflow through a reusable Windows Forms dialog.

The repository contains:

- `UpdateKit.Core` — platform-neutral release checking, Semantic Versioning, asset selection, streaming downloads, and SHA-256 verification.
- `UpdateKit.WinForms` — a reusable, DPI-aware Windows Forms update dialog built on the Core API.
- `UpdateKit.Example.WinForms` — an interactive host application demonstrating configuration, ownership, validation, and dialog use.

## Features

- GitHub REST release retrieval with pagination and optional bearer authentication.
- Draft filtering and opt-in prerelease support.
- Semantic Versioning 2.0.0 comparison for tags such as `1.2.3` and `v1.2.3`.
- Asset selection by exact name, case-insensitive extension, or caller predicate.
- Streaming, cancellable downloads with byte and percentage progress.
- Unique temporary files and atomic final-file replacement after a successful transfer.
- Direct SHA-256 verification or standard checksum-file lookup.
- Invalid-download deletion after a checksum mismatch.
- Result-based operational errors with stable `UpdateErrorCode` values.
- A responsive Windows Forms dialog with release details, progress, cancellation, safe closure, and actionable errors.
- Deterministic tests that use custom HTTP handlers instead of the real GitHub API.

## Supported platforms

| Project | Target | Runtime support |
| --- | --- | --- |
| `UpdateKit.Core` | `net8.0` | Any .NET 8 platform providing the required HTTP and file-system APIs |
| `UpdateKit.WinForms` | `net8.0-windows` | Windows with the .NET 8 Desktop Runtime |
| `UpdateKit.Example.WinForms` | `net8.0-windows` | Windows with the .NET 8 Desktop Runtime |

Only GitHub-hosted repositories are supported by the release source. Tags must be valid Semantic Versioning 2.0.0 values with an optional lowercase `v` prefix. Draft releases are always excluded; prereleases are excluded unless `IncludePrereleases` is enabled.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows when building or running the WinForms projects
- Git for cloning and contributing

## Build and test

```powershell
git clone <repository-url>
cd UpdateKit
dotnet restore UpdateKit.sln
dotnet build UpdateKit.sln --configuration Release --no-restore
dotnet test UpdateKit.sln --configuration Release --no-build --no-restore
```

Run the example on Windows:

```powershell
dotnet run --project samples/UpdateKit.Example.WinForms/UpdateKit.Example.WinForms.csproj
```

## Installation

No public package feed is configured by this repository. During source development, reference the projects directly:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/UpdateKit/src/UpdateKit.Core/UpdateKit.Core.csproj" />
  <ProjectReference Include="path/to/UpdateKit/src/UpdateKit.WinForms/UpdateKit.WinForms.csproj" />
</ItemGroup>
```

The two library projects contain NuGet metadata and can be packed locally:

```powershell
dotnet pack src/UpdateKit.Core/UpdateKit.Core.csproj --configuration Release
dotnet pack src/UpdateKit.WinForms/UpdateKit.WinForms.csproj --configuration Release
```

## Core API quick start

The caller owns the `HttpClient`. Keep it alive for every update operation that uses the `UpdateClient`, then dispose it according to the host application's normal lifetime policy.

```csharp
using UpdateKit;

using var httpClient = new HttpClient();
var client = new UpdateClient(
    httpClient,
    new UpdateClientOptions
    {
        RepositoryOwner = "owner",
        RepositoryName = "repository",
        AccessToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
        IncludePrereleases = false,
        UserAgent = "MyProduct-Updater",
    });

using var cancellation = new CancellationTokenSource();
var check = await client.CheckForUpdateAsync("1.2.3", cancellation.Token);

if (!check.IsSuccess)
{
    Console.Error.WriteLine($"{check.Error.Code}: {check.Error.Message}");
    return;
}

if (!check.Value.IsUpdateAvailable)
{
    Console.WriteLine("The application is current.");
    return;
}

var asset = client.SelectAssetByExtension(check.Value.LatestRelease, ".zip");
if (!asset.IsSuccess)
{
    Console.Error.WriteLine(asset.Error.Message);
    return;
}

var destination = Path.GetFullPath("MyProduct-update.zip");
var progress = new Progress<DownloadProgress>(value =>
{
    var display = value.Percentage is { } percentage
        ? $"{percentage:F1}%"
        : $"{value.BytesDownloaded:N0} bytes";
    Console.WriteLine(display);
});

var download = await client.DownloadAsync(
    asset.Value,
    destination,
    progress,
    cancellation.Token);

if (!download.IsSuccess)
{
    Console.Error.WriteLine($"{download.Error.Code}: {download.Error.Message}");
}
```

Exact-name and predicate selection are also available through `SelectAssetByExactName` and `SelectAssetByPredicate`. Selection returns the first matching release asset in GitHub response order. Exact names are case-sensitive; extensions are normalized to a leading dot and matched without case sensitivity.

## SHA-256 verification

For a checksum supplied by a trusted source:

```csharp
var verified = await client.DownloadAndVerifyAsync(
    asset.Value,
    destination,
    expectedSha256,
    progress,
    cancellation.Token);
```

For a checksum stored in another asset on the same release:

```csharp
var checksumAsset = client.SelectAssetByExactName(
    check.Value.LatestRelease,
    "SHA256SUMS.txt");

if (!checksumAsset.IsSuccess)
{
    Console.Error.WriteLine(checksumAsset.Error.Message);
    return;
}

var verified = await client.DownloadAndVerifyFromChecksumFileAsync(
    asset.Value,
    destination,
    checksumAsset.Value,
    progress,
    cancellation.Token);
```

Checksum files accept standard lines containing a 64-character hexadecimal SHA-256 value, whitespace, an optional `*` binary marker, and a filename. Filename matching is ordinal and case-sensitive. Duplicate identical entries are accepted; conflicting duplicates fail as `InvalidChecksum`. A mismatch returns `ChecksumMismatch` and deletes the downloaded file. If that deletion fails, the operation returns `FileSystemError` and the caller should treat the file as untrusted.

## Windows Forms dialog

Create a new `UpdateDialog` for every display. The dialog does not own or dispose its `UpdateClient` or the client's `HttpClient`.

```csharp
using UpdateKit;
using UpdateKit.WinForms;

using var httpClient = new HttpClient();
var client = new UpdateClient(httpClient, clientOptions);

var dialogOptions = new UpdateDialogOptions(
    client,
    currentVersion: "1.2.3",
    destinationFilePath: Path.GetFullPath("MyProduct-update.zip"),
    assetSelector: release => client.SelectAssetByExtension(release, ".zip"))
{
    DialogTitle = "MyProduct Update",
    ChecksumAssetSelector = release =>
        client.SelectAssetByExactName(release, "SHA256SUMS.txt"),
};

using var dialog = new UpdateDialog(dialogOptions);
dialog.ShowDialog(this);

if (dialog.DownloadResult is { } completed)
{
    MessageBox.Show($"Saved to {completed.FilePath}");
}
else if (dialog.LastError is { } error)
{
    MessageBox.Show($"{error.Code}: {error.Message}");
}
```

Set either `ExpectedSha256` or `ChecksumAssetSelector`, never both. By default the dialog checks when first shown. Set `CheckForUpdateOnShown = false` when the host needs to call `CheckForUpdateAsync` itself. The dialog is single-use, prevents concurrent operations, marshals state to the UI thread, cancels active work before closing, and exposes the final check, selected asset, download, and error results.

## Authentication and security

`AccessToken` is optional for public repositories. When supplied, it is sent as a bearer token only on GitHub API release-list requests. UpdateKit does not persist credentials and does not add that token to arbitrary asset-download URLs. Hosts should obtain tokens from a protected credential source such as an environment variable or operating-system credential store—never source code or logs.

Always obtain expected checksums through a trusted channel. A checksum published beside a compromised binary protects against transfer corruption but cannot establish publisher authenticity by itself.

## Cancellation, progress, and file safety

- Caller cancellation during a GitHub release check propagates `OperationCanceledException`.
- Download and verification cancellation returns a failed result with `UpdateErrorCode.DownloadCanceled`.
- `DownloadProgress.TotalBytes` and `Percentage` are `null` when the server omits `Content-Length`.
- Downloads use a unique temporary file in the destination directory.
- The destination is replaced only after the complete transfer succeeds.
- Existing destination files survive HTTP, streaming, cancellation, and pre-commit file-system failures.
- Incomplete temporary-file cleanup is attempted on every handled failure path; an external file lock or permission change can prevent best-effort cleanup.

The destination must be an absolute file path whose parent directory already exists.

## Error handling

Expected operational failures use `UpdateResult<T>`. Check `IsSuccess` before reading `Value` or `Error`. Configuration supplied to the `UpdateClient` constructor is validated immediately and can throw `UpdateConfigurationException`; null required objects can throw the usual argument exceptions.

Stable error categories include configuration, authentication, repository lookup, rate limits, malformed responses, invalid versions, missing assets, download/cancellation failures, file-system failures, and checksum failures. Use `UpdateErrorCode` for branching and `UpdateError.Message` for user-facing context.

## More documentation

- [Getting started](docs/GETTING_STARTED.md)
- [WinForms example project](samples/UpdateKit.Example.WinForms)
- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)

## License

UpdateKit is licensed under the [MIT License](LICENSE).
