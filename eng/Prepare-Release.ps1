[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Tag,

    [string] $PackageDirectory = "artifacts/release",

    [string] $DotNetCommand = "dotnet",

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
    },
    [pscustomobject]@{
        Id = "UpdateKit.Wpf"
        Path = "src/UpdateKit.Wpf/UpdateKit.Wpf.csproj"
    }
)

Push-Location $repositoryRoot
try
{
    foreach ($project in $projects)
    {
        $metadataJson = & $DotNetCommand msbuild $project.Path `
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
        & $DotNetCommand pack $project.Path `
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

            if ($project.Id -cne "UpdateKit.Core")
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

    $applicationName = "UpdateKit.Example.WinForms-win-x64"
    $applicationDirectory = Join-Path $packageRoot $applicationName
    $applicationZipPath = Join-Path $packageRoot "$applicationName.zip"

    & $DotNetCommand publish `
        "samples/UpdateKit.Example.WinForms/UpdateKit.Example.WinForms.csproj" `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        -p:PublishProfile=WinX64SelfContained `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        --output $applicationDirectory

    if ($LASTEXITCODE -ne 0)
    {
        throw "Publishing UpdateKit.Example.WinForms for win-x64 failed."
    }

    $applicationExecutable = Join-Path $applicationDirectory "UpdateKit.Example.WinForms.exe"
    if (!(Test-Path -LiteralPath $applicationExecutable -PathType Leaf))
    {
        throw "The expected application executable '$applicationExecutable' was not created."
    }

    Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE") `
        -Destination (Join-Path $applicationDirectory "LICENSE.txt")

    $applicationReadme = @"
UpdateKit Example $releaseVersion
===============================

UpdateKit.Example.WinForms.exe is the ready-to-run demonstration and
configuration application for UpdateKit, a .NET toolkit for updates through
GitHub Releases.

HOW TO LAUNCH
-------------
Extract the complete ZIP, then double-click UpdateKit.Example.WinForms.exe.
This Windows x64 build is self-contained; no .NET runtime or SDK installation
is required.

The example lets you configure a GitHub repository and exercise UpdateKit's
release checking, asset selection, safe downloading, progress, cancellation,
retry, and checksum-verification behavior. It does not add update support to
other installed applications. Developers integrate the UpdateKit libraries in
their own application source code.

SOURCE CODE
-----------
https://github.com/uzemvezeto8111/UpdateKit

SECURITY NOTE
-------------
This executable is not digitally signed. Windows SmartScreen may show an
unknown-publisher warning. Verify UpdateKit.Example.WinForms-win-x64.zip with
the SHA-256 value in SHA256SUMS.txt before extracting it.
"@
    [IO.File]::WriteAllText(
        (Join-Path $applicationDirectory "README.txt"),
        $applicationReadme.TrimStart(),
        [Text.UTF8Encoding]::new($false))

    $applicationFiles = @(Get-ChildItem -LiteralPath $applicationDirectory -File)
    $expectedApplicationFiles = @(
        "LICENSE.txt",
        "README.txt",
        "UpdateKit.Example.WinForms.exe"
    )
    $actualApplicationFiles = @($applicationFiles.Name | Sort-Object)
    if (Compare-Object $expectedApplicationFiles $actualApplicationFiles)
    {
        throw "The application directory contains unexpected or missing files: $($actualApplicationFiles -join ', ')."
    }

    $forbiddenReleaseFiles = @($applicationFiles | Where-Object {
        $_.Extension -in @(".pdb", ".cs", ".csproj", ".dll", ".json") -or
        $_.Name -match "(?i)testhost|tests?\.dll"
    })
    if ($forbiddenReleaseFiles.Count -gt 0)
    {
        throw "Forbidden development or test files were found: $($forbiddenReleaseFiles.Name -join ', ')."
    }

    function Assert-FileDoesNotContainSensitiveText
    {
        param(
            [Parameter(Mandatory)]
            [string] $Path,

            [Parameter(Mandatory)]
            [string[]] $Patterns
        )

        $stream = [IO.File]::OpenRead($Path)
        try
        {
            $buffer = [byte[]]::new(1MB)
            $tail = ""
            while (($count = $stream.Read($buffer, 0, $buffer.Length)) -gt 0)
            {
                $text = $tail + [Text.Encoding]::ASCII.GetString($buffer, 0, $count)
                foreach ($pattern in $Patterns)
                {
                    if ([Text.RegularExpressions.Regex]::IsMatch(
                        $text,
                        $pattern,
                        [Text.RegularExpressions.RegexOptions]::IgnoreCase))
                    {
                        throw "Sensitive content matching a forbidden pattern was found in '$Path'."
                    }
                }

                $tailLength = [Math]::Min(512, $text.Length)
                $tail = $text.Substring($text.Length - $tailLength, $tailLength)
            }
        }
        finally
        {
            $stream.Dispose()
        }
    }

    $sensitivePatterns = @(
        "github_pat_[A-Za-z0-9_]{10,}",
        "gh[pousr]_[A-Za-z0-9]{20,}",
        [Text.RegularExpressions.Regex]::Escape($repositoryRoot)
    )
    if ($env:USERPROFILE)
    {
        $sensitivePatterns += [Text.RegularExpressions.Regex]::Escape($env:USERPROFILE)
    }

    foreach ($file in $applicationFiles)
    {
        Assert-FileDoesNotContainSensitiveText -Path $file.FullName -Patterns $sensitivePatterns
    }

    Compress-Archive `
        -Path (Join-Path $applicationDirectory "*") `
        -DestinationPath $applicationZipPath `
        -CompressionLevel Optimal

    if (!(Test-Path -LiteralPath $applicationZipPath -PathType Leaf))
    {
        throw "The expected application ZIP '$applicationZipPath' was not created."
    }

    $applicationArchive = [IO.Compression.ZipFile]::OpenRead($applicationZipPath)
    try
    {
        $archiveFiles = @($applicationArchive.Entries |
            Where-Object { !$_.FullName.EndsWith("/", [StringComparison]::Ordinal) } |
            ForEach-Object { $_.FullName } |
            Sort-Object)
        if (Compare-Object $expectedApplicationFiles $archiveFiles)
        {
            throw "The application ZIP contains unexpected or missing files: $($archiveFiles -join ', ')."
        }
    }
    finally
    {
        $applicationArchive.Dispose()
    }

    $checksumPath = Join-Path $packageRoot "SHA256SUMS.txt"
    $checksumLines = Get-ChildItem -LiteralPath $packageRoot -File |
        Where-Object { $_.Extension -ceq ".nupkg" -or $_.Name -ceq "$applicationName.zip" } |
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

    Write-Host "Prepared $($projects.Count) packages, $applicationName.zip, and SHA256SUMS.txt in '$packageRoot'."
}
finally
{
    Pop-Location
}
