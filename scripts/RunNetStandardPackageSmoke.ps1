[CmdletBinding()]
param(
    [string]$PackageVersion = '',
    [string]$Configuration = 'Release',
    [string]$PackageSource = '',
    [string]$RunnerFramework = 'net10.0'
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
if ($PackageSource) {
    $packageSource = (Resolve-Path -LiteralPath $PackageSource).Path
    $packageSearchRoot = $packageSource
}
else {
    $packageSource = Join-Path $repoRoot "src/Dekaf/bin/$Configuration"
    $packageSearchRoot = Join-Path $repoRoot 'src'
}

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
        Where-Object { $_.BaseName -match '^Dekaf\.\d' } |
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

    $nuspecEntry = $zip.Entries | Where-Object { $_.Name -eq 'Dekaf.nuspec' } | Select-Object -First 1
    if (-not $nuspecEntry) {
        throw "Dekaf package is missing Dekaf.nuspec: $($corePackage.FullName)"
    }

    $nuspecReader = [System.IO.StreamReader]::new($nuspecEntry.Open())
    try {
        [xml]$nuspec = $nuspecReader.ReadToEnd()
    }
    finally {
        $nuspecReader.Dispose()
    }

    $abstractionsDependencies = @(
        $nuspec.package.metadata.dependencies.group.dependency |
            Where-Object { $_.id -eq 'Dekaf.Abstractions' })
    $expectedDependencyVersion = "[$PackageVersion]"
    $unexpectedAbstractionsDependencies = @(
        $abstractionsDependencies |
            Where-Object { $_.version -ne $expectedDependencyVersion })
    if ($abstractionsDependencies.Count -ne 2 -or
        $unexpectedAbstractionsDependencies.Count -ne 0) {
        throw "Dekaf must depend on Dekaf.Abstractions $expectedDependencyVersion for both target frameworks."
    }
}
finally {
    $zip.Dispose()
}

$abstractionsPackage = Get-ChildItem -Path $packageSearchRoot -Recurse -Filter "Dekaf.Abstractions.$PackageVersion.nupkg" |
    Select-Object -First 1
if (-not $abstractionsPackage) {
    throw "Dekaf.Abstractions package version $PackageVersion was not found under $packageSearchRoot"
}

$abstractionsZip = [System.IO.Compression.ZipFile]::OpenRead($abstractionsPackage.FullName)
try {
    $abstractionsEntries = [string[]]$abstractionsZip.Entries.FullName
    Assert-PackageEntry -Entries $abstractionsEntries -Entry 'lib/net10.0/Dekaf.Abstractions.dll' -PackagePath $abstractionsPackage.FullName
    Assert-PackageEntry -Entries $abstractionsEntries -Entry 'lib/netstandard2.0/Dekaf.Abstractions.dll' -PackagePath $abstractionsPackage.FullName
}
finally {
    $abstractionsZip.Dispose()
}

$otherPackages = Get-ChildItem -Path $packageSearchRoot -Recurse -Filter 'Dekaf*.nupkg' |
    Where-Object { $_.FullName -ne $corePackage.FullName -and $_.BaseName -notmatch '^Dekaf\.\d' }

foreach ($package in $otherPackages) {
    $otherZip = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        if ($package.FullName -ne $abstractionsPackage.FullName -and $otherZip.Entries.FullName -like 'lib/netstandard2.0/*') {
            throw "Only the Dekaf and Dekaf.Abstractions packages should contain netstandard2.0 assets: $($package.FullName)"
        }
    }
    finally {
        $otherZip.Dispose()
    }
}

$smokeProject = Join-Path $repoRoot 'samples/PackageSmoke/Dekaf.PackageSmoke.Runner/Dekaf.PackageSmoke.Runner.csproj'
$abstractionsAdapterProject = Join-Path $repoRoot 'samples/PackageSmoke/Dekaf.PackageSmoke.AbstractionsAdapter/Dekaf.PackageSmoke.AbstractionsAdapter.csproj'
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
        '-p:UseDekafPackages=true',
        "-p:RestoreAdditionalProjectSources=$packageSource"
    )

    Invoke-DotNet -Step 'package smoke build' -Arguments @(
        'build',
        $smokeProject,
        '--configuration',
        $Configuration,
        '--framework',
        $RunnerFramework,
        '--no-restore',
        "-p:DekafPackageVersion=$PackageVersion",
        '-p:UseDekafPackages=true',
        '-p:TreatWarningsAsErrors=true',
        '-p:EnforceCodeStyleInBuild=false'
    )

    Invoke-DotNet -Step 'netstandard2.0 abstractions adapter build' -Arguments @(
        'build',
        $abstractionsAdapterProject,
        '--configuration',
        $Configuration,
        '--framework',
        'netstandard2.0',
        '--no-restore',
        "-p:DekafPackageVersion=$PackageVersion",
        '-p:UseDekafPackages=true',
        '-p:TreatWarningsAsErrors=true',
        '-p:EnforceCodeStyleInBuild=false'
    )

    Invoke-DotNet -Step 'package smoke run' -Arguments @(
        'run',
        '--project',
        $smokeProject,
        '--configuration',
        $Configuration,
        '--framework',
        $RunnerFramework,
        '--no-build'
    )
}
finally {
    $env:NUGET_PACKAGES = $previousNuGetPackages
}
