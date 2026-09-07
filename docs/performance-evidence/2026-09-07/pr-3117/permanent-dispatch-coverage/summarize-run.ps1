$ErrorActionPreference = 'Stop'
$runLog = Join-Path $PSScriptRoot 'benchmark-measured-batched.log'
$cases = [System.Collections.Generic.List[object]]::new()
$case = $null
foreach ($line in Get-Content -LiteralPath $runLog) {
    if ($line -match '^// Benchmark:.*\[Pattern=(\w+), BatchSize=(\d+)\]') {
        $case = [pscustomobject]@{
            Pattern = $Matches[1]
            BatchSize = [int]$Matches[2]
            WarmupSeconds = 0.0
            WarmupLifetimes = [long]0
            WarmupIterations = 0
            Samples = 0
            SteadyBytes = $null
            LargestBatch = $null
        }
        $cases.Add($case)
    }
    elseif ($line -match '^WorkloadWarmup\s+\d+: (\d+) op, ([\d.]+) ns,') {
        $case.WarmupLifetimes += [long]$Matches[1]
        $case.WarmupSeconds += [double]::Parse($Matches[2], [Globalization.CultureInfo]::InvariantCulture) / 1e9
        $case.WarmupIterations++
    }
    elseif ($line -match '^WorkloadActual\s+\d+:') {
        $case.Samples++
    }
    elseif ($line -match '^STEADY_ALLOCATION pattern=\w+ batchSize=\d+ largestBatch=(\d+) records=261888 bytes=(\d+)') {
        $case.LargestBatch = [int]$Matches[1]
        $case.SteadyBytes = [long]$Matches[2]
    }
}
if ($cases.Count -ne 6) { throw "Expected six cases, found $($cases.Count)." }
foreach ($case in $cases) {
    if ($case.WarmupIterations -ne 30 -or $case.WarmupSeconds -lt 20 -or $case.Samples -ne 15 -or $null -eq $case.SteadyBytes -or $case.SteadyBytes -ne 0) {
        throw "Incomplete diagnostic case: $($case | ConvertTo-Json -Compress)"
    }
}
$cases | Export-Csv (Join-Path $PSScriptRoot 'warmup-verification.csv') -NoTypeInformation
$cases | Format-Table
