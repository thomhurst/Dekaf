[CmdletBinding()]
param(
    [string]$PackageSource = '',
    [string]$PackageVersion = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $PackageSource) { $PackageSource = Join-Path $repoRoot 'src' }
$PackageSource = (Resolve-Path -LiteralPath $PackageSource).Path
$packages = @(Get-ChildItem -LiteralPath $PackageSource -Recurse -Filter 'Dekaf.*.nupkg' |
    Where-Object { $_.BaseName -match '^Dekaf\.\d' } |
    Sort-Object LastWriteTimeUtc -Descending)
if ($PackageVersion) { $packages = @($packages | Where-Object { $_.Name -eq "Dekaf.$PackageVersion.nupkg" }) }
if ($packages.Count -eq 0) { throw 'Pack Dekaf and Dekaf.Abstractions before building the GSSAPI package smoke client.' }
$core = $packages[0]
$PackageVersion = $core.BaseName.Substring('Dekaf.'.Length)
$abstractions = Get-ChildItem -LiteralPath $PackageSource -Recurse -Filter "Dekaf.Abstractions.$PackageVersion.nupkg" |
    Select-Object -First 1
if (-not $abstractions) { throw "Dekaf.Abstractions $PackageVersion was not found." }

$outputRoot = Join-Path $repoRoot 'artifacts/gssapi-package-smoke'
$cache = Join-Path $outputRoot ('nuget-cache-' + [guid]::NewGuid().ToString('N'))
$project = Join-Path $repoRoot 'samples/PackageSmoke/Dekaf.PackageSmoke.Gssapi/Dekaf.PackageSmoke.Gssapi.csproj'
$sources = "$($core.DirectoryName)%3B$($abstractions.DirectoryName)"
& dotnet restore $project --packages $cache "-p:DekafPackageVersion=$PackageVersion" "-p:RestoreAdditionalProjectSources=$sources"
if ($LASTEXITCODE -ne 0) { throw 'GSSAPI package smoke restore failed.' }

$assetsPath = Join-Path (Split-Path $project -Parent) 'obj/project.assets.json'
$assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
foreach ($framework in @('net8.0', 'net10.0')) {
    foreach ($package in @('Dekaf', 'Dekaf.Abstractions')) {
        $library = $assets.targets[$framework]["$package/$PackageVersion"]
        foreach ($assetKind in @('compile', 'runtime')) {
            if (-not $library[$assetKind].ContainsKey("lib/$framework/$package.dll")) {
                throw "$package did not select its $framework $assetKind asset."
            }
        }
        if ($assets.libraries["$package/$PackageVersion"].type -ne 'package') {
            throw "$package was not restored from a NuGet package."
        }
    }
    if ($assets.targets[$framework].Keys | Where-Object { $_ -like 'Polyfill/*' }) {
        throw 'Internal Polyfill sources must not become a package consumer dependency.'
    }
    $output = Join-Path $outputRoot $framework
    & dotnet build $project -c Release -f $framework --no-restore --output $output "-p:DekafPackageVersion=$PackageVersion"
    if ($LASTEXITCODE -ne 0) { throw "GSSAPI package smoke build failed for $framework." }
    & dotnet (Join-Path $output 'Dekaf.PackageSmoke.Gssapi.dll') --verify-assets
    if ($LASTEXITCODE -ne 0) { throw "GSSAPI runtime asset validation failed for $framework." }
}
