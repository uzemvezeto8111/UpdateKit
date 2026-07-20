[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Tag,

    [string] $PackageDirectory = "artifacts/release",

    [switch] $VerifyOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$semanticVersionPattern =
    "(?<version>(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)" +
    "(?:-(?:(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)" +
    "(?:\.(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*))*))?)"

if ($Tag -notmatch "^v$semanticVersionPattern$")
{
    throw "Release tag '$Tag' must be 'v' followed by a Semantic Versioning value without build metadata."
}

$releaseVersion = $Matches.version
$isPrerelease = $releaseVersion.IndexOf('-', [StringComparison]::Ordinal) -ge 0
$projects = @(
    [pscustomobject]@{
        Id = "UpdateKit.Core"
        Path = "src/UpdateKit.Core/UpdateKit.Core.csproj"
    },
    [pscustomobject]@{
        Id = "UpdateKit.WinForms"
        Path = "src/UpdateKit.WinForms/UpdateKit.WinForms.csproj"
    }
)

Push-Location $repositoryRoot
try
{
    foreach ($project in $projects)
    {
        $metadataJson = & dotnet msbuild $project.Path `
            -nologo `
            -getProperty:PackageId `
            -getProperty:PackageVersion

        if ($LASTEXITCODE -ne 0)
        {
            throw "Could not evaluate package metadata for '$($project.Path)'."
        }

        $metadata = $metadataJson | ConvertFrom-Json
        $packageId = $metadata.Properties.PackageId
        $packageVersion = $metadata.Properties.PackageVersion

        if ($packageId -cne $project.Id)
        {
            throw "Project '$($project.Path)' evaluated PackageId '$packageId'; expected '$($project.Id)'."
        }

        if ($packageVersion -cne $releaseVersion)
        {
            throw "Tag version '$releaseVersion' does not match $($project.Id) PackageVersion '$packageVersion'."
        }
    }

    Write-Host "Verified release tag $Tag against package metadata version $releaseVersion."

    if ($VerifyOnly)
    {
        return
    }

    $packageRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $PackageDirectory))
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
    $artifactPrefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar

    if (!$packageRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "PackageDirectory must resolve beneath '$artifactRoot'."
    }

    if (Test-Path -LiteralPath $packageRoot)
    {
        Remove-Item -LiteralPath $packageRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $packageRoot | Out-Null

    foreach ($project in $projects)
    {
        & dotnet pack $project.Path `
            --configuration Release `
            --no-build `
            --no-restore `
            --output $packageRoot

        if ($LASTEXITCODE -ne 0)
        {
            throw "Packing '$($project.Path)' failed."
        }
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    foreach ($project in $projects)
    {
        $packagePath = Join-Path $packageRoot "$($project.Id).$releaseVersion.nupkg"
        if (!(Test-Path -LiteralPath $packagePath -PathType Leaf))
        {
            throw "Expected package '$packagePath' was not created."
        }

        $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
        try
        {
            $nuspecEntry = $archive.Entries |
                Where-Object { $_.FullName -ceq "$($project.Id).nuspec" } |
                Select-Object -First 1

            if ($null -eq $nuspecEntry)
            {
                throw "Package '$packagePath' does not contain its expected nuspec."
            }

            $reader = [IO.StreamReader]::new($nuspecEntry.Open())
            try
            {
                [xml] $nuspec = $reader.ReadToEnd()
            }
            finally
            {
                $reader.Dispose()
            }

            if ($nuspec.package.metadata.id -cne $project.Id -or
                $nuspec.package.metadata.version -cne $releaseVersion)
            {
                throw "Package '$packagePath' metadata does not match '$($project.Id) $releaseVersion'."
            }

            if ($project.Id -ceq "UpdateKit.WinForms")
            {
                $coreDependency = $nuspec.package.metadata.dependencies.group.dependency |
                    Where-Object { $_.id -ceq "UpdateKit.Core" } |
                    Select-Object -First 1

                if ($null -eq $coreDependency -or $coreDependency.version -cne $releaseVersion)
                {
                    throw "Package '$packagePath' must depend on UpdateKit.Core $releaseVersion."
                }
            }
        }
        finally
        {
            $archive.Dispose()
        }
    }

    $checksumPath = Join-Path $packageRoot "SHA256SUMS.txt"
    $checksumLines = Get-ChildItem -LiteralPath $packageRoot -Filter *.nupkg -File |
        Sort-Object Name |
        ForEach-Object {
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash *$($_.Name)"
        }
    [IO.File]::WriteAllLines(
        $checksumPath,
        [string[]] $checksumLines,
        [Text.UTF8Encoding]::new($false))

    if ($env:GITHUB_OUTPUT)
    {
        "version=$releaseVersion" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
        "prerelease=$($isPrerelease.ToString().ToLowerInvariant())" |
            Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    }

    Write-Host "Prepared $($projects.Count) packages and SHA256SUMS.txt in '$packageRoot'."
}
finally
{
    Pop-Location
}
