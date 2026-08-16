[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'src'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

$unshippedFiles = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter 'PublicAPI.Unshipped*.txt'
if ($unshippedFiles.Count -eq 0) {
    throw 'No PublicAPI.Unshipped files were found.'
}

foreach ($unshippedFile in $unshippedFiles) {
    $shippedName = $unshippedFile.Name.Replace('PublicAPI.Unshipped', 'PublicAPI.Shipped')
    $shippedPath = Join-Path $unshippedFile.DirectoryName $shippedName
    if (-not (Test-Path -LiteralPath $shippedPath -PathType Leaf)) {
        throw "Missing shipped API file for '$($unshippedFile.FullName)'."
    }

    $shippedEntries = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($line in [System.IO.File]::ReadAllLines($shippedPath)) {
        if ($line -and $line -ne '#nullable enable') {
            [void]$shippedEntries.Add($line)
        }
    }

    $newEntries = [System.Collections.Generic.List[string]]::new()
    $removedEntries = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($line in [System.IO.File]::ReadAllLines($unshippedFile.FullName)) {
        if (-not $line -or $line -eq '#nullable enable') {
            continue
        }

        if ($line.StartsWith('*REMOVED*', [System.StringComparison]::Ordinal)) {
            [void]$removedEntries.Add($line.Substring('*REMOVED*'.Length))
            continue
        }

        $newEntries.Add($line)
    }

    foreach ($removedEntry in $removedEntries) {
        [void]$shippedEntries.Remove($removedEntry)
    }

    foreach ($newEntry in $newEntries) {
        [void]$shippedEntries.Add($newEntry)
    }

    $sortedEntries = [string[]]$shippedEntries
    [System.Array]::Sort($sortedEntries, [System.StringComparer]::Ordinal)
    [System.IO.File]::WriteAllLines($shippedPath, @('#nullable enable') + $sortedEntries, $utf8NoBom)
    [System.IO.File]::WriteAllLines($unshippedFile.FullName, @('#nullable enable'), $utf8NoBom)
}

Write-Host "Promoted $($unshippedFiles.Count) public API baseline file(s)."
