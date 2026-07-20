[CmdletBinding()]
param(
    [string] $Tag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Resolve-DotNetCommand
{
    $candidates = @()
    if ($env:DOTNET_ROOT)
    {
        $candidates += Join-Path $env:DOTNET_ROOT "dotnet.exe"
    }

    if ($env:USERPROFILE)
    {
        $candidates += Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
    }

    $pathCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($pathCommand)
    {
        $candidates += $pathCommand.Source
    }

    foreach ($candidate in $candidates | Select-Object -Unique)
    {
        if (!(Test-Path -LiteralPath $candidate -PathType Leaf))
        {
            continue
        }

        & $candidate --version *> $null
        if ($LASTEXITCODE -eq 0)
        {
            return $candidate
        }
    }

    throw "The .NET 8 SDK could not be found. Install it or set DOTNET_ROOT."
}

$dotnet = Resolve-DotNetCommand
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts/release"))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$artifactPrefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (!$releaseRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase))
{
    throw "Release output must resolve beneath '$artifactRoot'."
}

Push-Location $repositoryRoot
try
{
    if (Test-Path -LiteralPath $releaseRoot)
    {
        Remove-Item -LiteralPath $releaseRoot -Recurse -Force
    }

    & $dotnet restore UpdateKit.sln --force --no-cache
    if ($LASTEXITCODE -ne 0)
    {
        throw "Solution restore failed."
    }

    & $dotnet restore `
        "samples/UpdateKit.Example.WinForms/UpdateKit.Example.WinForms.csproj" `
        --runtime win-x64
    if ($LASTEXITCODE -ne 0)
    {
        throw "Windows x64 publish restore failed."
    }

    if ([string]::IsNullOrWhiteSpace($Tag))
    {
        $metadataJson = & $dotnet msbuild `
            "src/UpdateKit.Core/UpdateKit.Core.csproj" `
            -nologo `
            -getProperty:PackageVersion
        if ($LASTEXITCODE -ne 0)
        {
            throw "Could not evaluate the current package version."
        }

        $metadata = $metadataJson | ConvertFrom-Json
        $Tag = "v$($metadata.Properties.PackageVersion)"
    }

    & $PSScriptRoot\Prepare-Release.ps1 `
        -Tag $Tag `
        -DotNetCommand $dotnet `
        -VerifyOnly

    & $dotnet build UpdateKit.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0)
    {
        throw "Release build failed."
    }

    & $dotnet test UpdateKit.sln `
        --configuration Release `
        --no-build `
        --no-restore
    if ($LASTEXITCODE -ne 0)
    {
        throw "Release tests failed."
    }

    & $PSScriptRoot\Prepare-Release.ps1 `
        -Tag $Tag `
        -DotNetCommand $dotnet

    Write-Host "Release artifacts for $Tag are ready in '$repositoryRoot\artifacts\release'."
}
finally
{
    Pop-Location
}
