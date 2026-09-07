$ErrorActionPreference = 'Stop'
$root = 'C:/git/Dekaf-worktrees/pr-3085-disabled-metrics/.artifacts/metrics-review/results'
$rows = @{}
foreach ($phase in 'A1','B','A2') {
    foreach ($mode in 'sync-off','pending-off','sync-on','pending-on') {
        $files = @(Get-ChildItem -LiteralPath "$root/$phase-$mode/results" -Filter '*report.csv')
        if ($files.Count -ne 1) { throw "Missing report: $phase-$mode" }
        $rows["$phase-$mode"] = Import-Csv -LiteralPath $files[0].FullName
    }
}
$summary = foreach ($mode in 'sync-off','pending-off','sync-on','pending-on') {
    $a1 = $rows["A1-$mode"]
    $b = $rows["B-$mode"]
    $a2 = $rows["A2-$mode"]
    $first = [double](($a1.Mean -split ' ')[0])
    $candidate = [double](($b.Mean -split ' ')[0])
    $second = [double](($a2.Mean -split ' ')[0])
    [pscustomobject]@{
        Mode=$mode;A1=$a1.Mean;B=$b.Mean;A2=$a2.Mean
        ErrorA1=$a1.Error;ErrorB=$b.Error;ErrorA2=$a2.Error
        BvsA1=[Math]::Round(($candidate/$first-1)*100,3)
        BvsA2=[Math]::Round(($candidate/$second-1)*100,3)
        Drift=[Math]::Round(($second/$first-1)*100,3)
        AllocationA1=$a1.Allocated;AllocationB=$b.Allocated;AllocationA2=$a2.Allocated
        SamplesPerPhase=15
    }
}
$summary | ConvertTo-Json -Depth 4 | Set-Content "$root/summary.json"
$summary | Format-Table Mode,A1,B,A2,BvsA1,BvsA2,Drift,AllocationA1,AllocationB,AllocationA2
