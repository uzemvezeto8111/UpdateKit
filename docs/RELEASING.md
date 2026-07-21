# Releasing UpdateKit

The repository's `Release` workflow creates GitHub Releases from version tags. It does not publish packages to NuGet.org.

## Release contract

- Only a pushed tag matching `v*` starts the workflow.
- The text after `v` must be a valid Semantic Versioning value without build metadata, such as `0.1.0` or `0.2.0-beta.1`.
- The tag version must exactly match the evaluated `PackageVersion` of `UpdateKit.Core`, `UpdateKit.WinForms`, and `UpdateKit.Wpf`.
- Stable tags create stable GitHub Releases. A version containing a prerelease suffix creates or updates a prerelease.
- The workflow restores, builds, and tests the complete solution before packing anything.
- `UpdateKit.Core`, `UpdateKit.WinForms`, and `UpdateKit.Wpf` are packed. NuGet.org publishing is intentionally out of scope.
- `UpdateKit.Example.WinForms` is published for Windows x64 as a self-contained, untrimmed, single-file GUI executable and packaged with `README.txt` and `LICENSE.txt`. ReadyToRun remains disabled to avoid unnecessary size and complexity in the compressed single-file artifact.
- The release contains all three `.nupkg` files, `UpdateKit.Example.WinForms-win-x64.zip`, and `SHA256SUMS.txt`. Each checksum line uses lowercase SHA-256, a binary marker, and the asset filename.
- Re-running the workflow for an existing tag compares the expected and published asset names and bytes before updating release metadata. Any difference fails the job instead of deleting or replacing a published asset. A new release receives GitHub-generated notes.

The build job has read-only repository access. A separate release job receives only `contents: write`, downloads the verified build artifact, and creates or updates the GitHub Release. No package-feed API key is configured or required.

## Prepare the version

Set the stable version in `Directory.Build.props`:

```xml
<VersionPrefix>0.3.0</VersionPrefix>
```

For a prerelease, also set `VersionSuffix`:

```xml
<VersionPrefix>0.3.0</VersionPrefix>
<VersionSuffix>beta.1</VersionSuffix>
```

This evaluates to package version `0.3.0-beta.1`, which requires tag `v0.3.0-beta.1`. Remove `VersionSuffix` for a stable release. Commit and push the version change before creating the tag.

## Perform a dry run

A dry run performs every local validation and packaging step but does not push a tag, create a GitHub Release, or publish to NuGet.org. From a clean repository root on Windows, run:

```cmd
eng\build-release.cmd -Tag v0.3.0
```

Omit `-Tag` to use the currently evaluated package version. The script locates the .NET 8 SDK, restores the complete solution and Windows x64 runtime assets, verifies the requested tag, builds in Release mode, runs every test, validates all three NuGet packages, publishes and security-scans the example, creates the ZIP, and generates checksums. It cleans only `artifacts/release` before packaging.

The resulting tree is:

```text
artifacts/release/
|-- UpdateKit.Core.<version>.nupkg
|-- UpdateKit.WinForms.<version>.nupkg
|-- UpdateKit.Wpf.<version>.nupkg
|-- UpdateKit.Example.WinForms-win-x64.zip
|-- SHA256SUMS.txt
`-- UpdateKit.Example.WinForms-win-x64/
    |-- UpdateKit.Example.WinForms.exe
    |-- README.txt
    `-- LICENSE.txt
```

Inspect the packages and verify the checksums locally:

```powershell
Get-Content artifacts/release/SHA256SUMS.txt
Get-ChildItem artifacts/release/*.nupkg, artifacts/release/*.zip | ForEach-Object {
    Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
}
```

The preparation script rejects malformed tags, package-version mismatches, missing packages, mismatched nuspec identity or version metadata, unexpected publish sidecars, PDB/test/source files, likely GitHub-token material, and embedded absolute repository or user-profile paths. The distributable never includes per-user settings; the running application stores those under `%LocalAppData%\UpdateKit\Example.WinForms`. The output directory must remain under the repository's ignored `artifacts` directory.

The application is not digitally signed because the project has no authentic code-signing certificate. Windows SmartScreen may show an unknown-publisher warning. Do not bypass checksum verification and never substitute a fabricated certificate or icon. A final icon can be added at `samples\UpdateKit.Example.WinForms\Assets\UpdateKit.ico` when an authentic project asset is available.

## Publish the GitHub Release

After reviewing the dry-run output:

```powershell
git tag -a v0.3.0 -m "UpdateKit v0.3.0"
git push origin v0.3.0
```

The tag push is the only release trigger. The workflow restores the solution and Windows x64 runtime assets, verifies that the pushed tag already exists and matches package metadata, builds, tests, packs, publishes, scans, and uploads exactly these assets:

- `UpdateKit.Core.<version>.nupkg`
- `UpdateKit.WinForms.<version>.nupkg`
- `UpdateKit.Wpf.<version>.nupkg`
- `UpdateKit.Example.WinForms-win-x64.zip`
- `SHA256SUMS.txt`

It then creates the matching GitHub Release or safely refreshes the title and prerelease state of an existing byte-for-byte-identical release. A Semantic Versioning prerelease suffix marks the GitHub Release as a prerelease. If any validation, transfer, or existing-asset comparison fails, publication stops. The workflow never publishes to NuGet.org.

Do not move or reuse an already published version tag. If a release must be corrected, use a new version.
