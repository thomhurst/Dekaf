[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'src'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

$projects = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.csproj'
foreach ($project in $projects) {
    $expectedFiles = if ($project.BaseName -in @('Dekaf', 'Dekaf.Abstractions')) {
        @(
            'PublicAPI.Shipped.net10.0.txt',
            'PublicAPI.Unshipped.net10.0.txt',
            'PublicAPI.Shipped.netstandard2.0.txt',
            'PublicAPI.Unshipped.netstandard2.0.txt'
        )
    }
    else {
        @('PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt')
    }

    foreach ($expectedFile in $expectedFiles) {
        $expectedPath = Join-Path $project.DirectoryName $expectedFile
        if (-not (Test-Path -LiteralPath $expectedPath -PathType Leaf)) {
            throw "Shipping project '$($project.FullName)' is missing '$expectedFile'."
        }
    }
}

[xml]$packageVersions = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Packages.props')
$analyzerVersion = [string]($packageVersions.Project.ItemGroup.PackageVersion |
    Where-Object { $_.Include -eq 'Microsoft.CodeAnalysis.PublicApiAnalyzers' } |
    Select-Object -ExpandProperty Version -First 1)
if (-not $analyzerVersion) {
    throw 'Microsoft.CodeAnalysis.PublicApiAnalyzers has no central package version.'
}

$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "dekaf-public-api-gate-$([System.Guid]::NewGuid().ToString('N'))"
[void](New-Item -ItemType Directory -Path $fixtureRoot)

try {
    [System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'PublicApiFixture.csproj'), @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="$analyzerVersion" PrivateAssets="all" />
  </ItemGroup>
</Project>
"@, $utf8NoBom)
    [System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'PublicAPI.Shipped.txt'), @"
#nullable enable
PublicApiFixture.KnownApi
PublicApiFixture.KnownApi.KnownApi() -> void
"@, $utf8NoBom)
    [System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'PublicAPI.Unshipped.txt'), "#nullable enable`n", $utf8NoBom)
    [System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'KnownApi.cs'), @"
namespace PublicApiFixture;

public sealed class KnownApi { }
"@, $utf8NoBom)

    $acceptedOutput = & dotnet build (Join-Path $fixtureRoot 'PublicApiFixture.csproj') --configuration Release --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Public API fixture baseline failed unexpectedly:`n$($acceptedOutput -join "`n")"
    }

    [System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'AccidentalApi.cs'), @"
namespace PublicApiFixture;

public sealed class AccidentalApi { }
"@, $utf8NoBom)
    $rejectedOutput = & dotnet build (Join-Path $fixtureRoot 'PublicApiFixture.csproj') --configuration Release --no-restore --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -eq 0 -or ($rejectedOutput -join "`n") -notmatch 'RS0016') {
        throw "Public API gate did not reject an undeclared public type:`n$($rejectedOutput -join "`n")"
    }

    Remove-Item -LiteralPath (Join-Path $fixtureRoot 'AccidentalApi.cs')
    [System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'KnownApi.cs'), @"
namespace PublicApiFixture;

internal sealed class KnownApi { }
"@, $utf8NoBom)
    $removalOutput = & dotnet build (Join-Path $fixtureRoot 'PublicApiFixture.csproj') --configuration Release --no-restore --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -eq 0 -or ($removalOutput -join "`n") -notmatch 'RS0017') {
        throw "Public API gate did not reject a removed public type:`n$($removalOutput -join "`n")"
    }
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host "Public API gate covers $($projects.Count) shipping project(s) and rejects additions/removals."
exit 0
