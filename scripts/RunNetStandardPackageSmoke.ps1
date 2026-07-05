[CmdletBinding()]
param(
    [string]$PackageVersion = '',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,
        [Parameter(Mandatory)]
        [string]$Step
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE"
    }
}

function Get-PackageVersionFromFile {
    param([Parameter(Mandatory)][System.IO.FileInfo]$PackageFile)

    $prefix = 'Dekaf.'
    $suffix = '.nupkg'
    return $PackageFile.Name.Substring($prefix.Length, $PackageFile.Name.Length - $prefix.Length - $suffix.Length)
}

function Assert-PackageEntry {
    param(
        [Parameter(Mandatory)]
        [string[]]$Entries,
        [Parameter(Mandatory)]
        [string]$Entry,
        [Parameter(Mandatory)]
        [string]$PackagePath
    )

    if ($Entries -notcontains $Entry) {
        throw "Package '$PackagePath' is missing '$Entry'"
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageSource = Join-Path $repoRoot "src/Dekaf/bin/$Configuration"

if (-not (Test-Path -LiteralPath $packageSource)) {
    throw "Package source directory not found: $packageSource"
}

if ($PackageVersion) {
    $packagePath = Join-Path $packageSource "Dekaf.$PackageVersion.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "Dekaf package not found: $packagePath"
    }

    $corePackage = Get-Item -LiteralPath $packagePath
}
else {
    $corePackage = Get-ChildItem -LiteralPath $packageSource -Filter 'Dekaf.*.nupkg' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if (-not $corePackage) {
        throw "No Dekaf package found under $packageSource"
    }

    $PackageVersion = Get-PackageVersionFromFile $corePackage
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::OpenRead($corePackage.FullName)
try {
    $entries = [string[]]$zip.Entries.FullName
    Assert-PackageEntry -Entries $entries -Entry 'lib/net10.0/Dekaf.dll' -PackagePath $corePackage.FullName
    Assert-PackageEntry -Entries $entries -Entry 'lib/netstandard2.0/Dekaf.dll' -PackagePath $corePackage.FullName
}
finally {
    $zip.Dispose()
}

$otherPackages = Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -Filter 'Dekaf*.nupkg' |
    Where-Object { $_.FullName -ne $corePackage.FullName }

foreach ($package in $otherPackages) {
    $otherZip = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        if ($otherZip.Entries.FullName -like 'lib/netstandard2.0/*') {
            throw "Only the Dekaf core package should contain a netstandard2.0 asset for this validation pass: $($package.FullName)"
        }
    }
    finally {
        $otherZip.Dispose()
    }
}

$smokeProject = Join-Path $repoRoot 'samples/PackageSmoke/Dekaf.PackageSmoke.Runner/Dekaf.PackageSmoke.Runner.csproj'
if (-not (Test-Path -LiteralPath $smokeProject)) {
    throw "Package smoke project not found: $smokeProject"
}

$artifactsDir = Join-Path $repoRoot 'artifacts/package-smoke'
$nugetPackages = Join-Path $artifactsDir 'nuget-cache'
New-Item -ItemType Directory -Force $nugetPackages | Out-Null

$previousNuGetPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = $nugetPackages

try {
    Invoke-DotNet -Step 'package smoke restore' -Arguments @(
        'restore',
        $smokeProject,
        "-p:DekafPackageVersion=$PackageVersion",
        "-p:RestoreAdditionalProjectSources=$packageSource"
    )

    Invoke-DotNet -Step 'package smoke build' -Arguments @(
        'build',
        $smokeProject,
        '--configuration',
        $Configuration,
        '--no-restore',
        "-p:DekafPackageVersion=$PackageVersion",
        '-p:TreatWarningsAsErrors=true'
    )

    Invoke-DotNet -Step 'package smoke run' -Arguments @(
        'run',
        '--project',
        $smokeProject,
        '--configuration',
        $Configuration,
        '--framework',
        'net10.0',
        '--no-build'
    )
}
finally {
    $env:NUGET_PACKAGES = $previousNuGetPackages
}
