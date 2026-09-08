$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$env:DOTNET_TieredCompilation = '0'
foreach ($case in @('ConstructAndHash', 'TypedConstruction')) {
    dotnet "$root/bin-B-pinned/Dekaf.Benchmarks.dll" $case "$root/smoke-B-pinned-$case" --smoke *> "$root/smoke-B-pinned-$case.log"
    if ($LASTEXITCODE -ne 0) { throw "Pinned candidate smoke failed: $case" }
}
Write-Output 'Both pinned candidate smoke cases passed.'
foreach ($phase in @('A1', 'B', 'A2')) {
    $binary = if ($phase -eq 'B') { "$root/bin-B-pinned/Dekaf.Benchmarks.dll" } else { "$root/bin-A/Dekaf.Benchmarks.dll" }
    foreach ($case in @('ConstructAndHash', 'TypedConstruction')) {
        Write-Output "Starting $phase $case at $([DateTime]::UtcNow.ToString('O'))"
        dotnet $binary $case "$root/$phase-$case" *> "$root/$phase-$case.log"
        if ($LASTEXITCODE -ne 0) { throw "Measurement failed: $phase $case" }
        Write-Output "Completed $phase $case at $([DateTime]::UtcNow.ToString('O'))"
    }
}
