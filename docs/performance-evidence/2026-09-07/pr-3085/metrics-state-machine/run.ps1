$ErrorActionPreference = 'Stop'
$env:DOTNET_TieredCompilation = '0'
$root = 'C:/git/Dekaf-worktrees/pr-3085-disabled-metrics/.artifacts/metrics-review'
foreach ($phase in 'A1','B','A2') {
    $binary = if ($phase -eq 'B') { 'B' } else { 'A' }
    foreach ($mode in 'sync-off','pending-off','sync-on','pending-on') {
        $artifact = "$root/results/$phase-$mode"
        New-Item -ItemType Directory -Force $artifact | Out-Null
        & dotnet "$root/$binary/harness.dll" $mode $artifact > "$artifact/console.log" 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Diagnostic failed: $phase $mode" }
        Write-Output "$phase $mode completed"
    }
}
