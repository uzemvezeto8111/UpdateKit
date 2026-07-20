# Releasing UpdateKit

The repository's `Release` workflow creates GitHub Releases from version tags. It does not publish packages to NuGet.org.

## Release contract

- Only a pushed tag matching `v*` starts the workflow.
- The text after `v` must be a valid Semantic Versioning value without build metadata, such as `0.1.0` or `0.2.0-beta.1`.
- The tag version must exactly match the evaluated `PackageVersion` of both `UpdateKit.Core` and `UpdateKit.WinForms`.
- Stable tags create stable GitHub Releases. A version containing a prerelease suffix creates or updates a prerelease.
- The workflow restores, builds, and tests the complete solution before packing anything.
- Only `UpdateKit.Core` and `UpdateKit.WinForms` are packed. NuGet.org publishing is intentionally out of scope.
- The release contains both `.nupkg` files and `SHA256SUMS.txt`. Each checksum line uses lowercase SHA-256, a binary marker, and the package filename.
- Re-running the workflow for an existing tag compares the expected and published asset names and bytes before updating release metadata. Any difference fails the job instead of deleting or replacing a published asset. A new release receives GitHub-generated notes.

The build job has read-only repository access. A separate release job receives only `contents: write`, downloads the verified build artifact, and creates or updates the GitHub Release. No package-feed API key is configured or required.

## Prepare the version

Set the stable version in `Directory.Build.props`:

```xml
<VersionPrefix>0.2.0</VersionPrefix>
```

For a prerelease, also set `VersionSuffix`:

```xml
<VersionPrefix>0.2.0</VersionPrefix>
<VersionSuffix>beta.1</VersionSuffix>
```

This evaluates to package version `0.2.0-beta.1`, which requires tag `v0.2.0-beta.1`. Remove `VersionSuffix` for a stable release. Commit and push the version change before creating the tag.

## Perform a dry run

A dry run performs every local validation and packaging step but does not push a tag, create a GitHub Release, or publish to NuGet.org. From a clean repository root on Windows, run:

```powershell
dotnet restore UpdateKit.sln
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\Prepare-Release.ps1 -Tag v0.1.0 -VerifyOnly
dotnet build UpdateKit.sln --configuration Release --no-restore
dotnet test UpdateKit.sln --configuration Release --no-build --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\Prepare-Release.ps1 -Tag v0.1.0
```

Replace `v0.1.0` with the intended tag. The final command recreates `artifacts/release` and writes:

```text
UpdateKit.Core.<version>.nupkg
UpdateKit.WinForms.<version>.nupkg
SHA256SUMS.txt
```

Inspect the packages and verify the checksums locally:

```powershell
Get-Content artifacts/release/SHA256SUMS.txt
Get-ChildItem artifacts/release/*.nupkg | ForEach-Object {
    Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
}
```

The preparation script rejects malformed tags, package-version mismatches, missing packages, and mismatched nuspec identity or version metadata. Its output directory must remain under the repository's `artifacts` directory.

## Publish the GitHub Release

After reviewing the dry-run output:

```powershell
git tag -a v0.1.0 -m "UpdateKit v0.1.0"
git push origin v0.1.0
```

The tag push is the only release trigger. The workflow verifies that the pushed tag already exists, then creates the matching GitHub Release or safely refreshes the title and prerelease state of an existing byte-for-byte-identical release. If any restore, version validation, build, test, pack, checksum, artifact-transfer, or existing-asset comparison step fails, publication stops.

Do not move or reuse an already published version tag. If a release must be corrected, use a new version.
