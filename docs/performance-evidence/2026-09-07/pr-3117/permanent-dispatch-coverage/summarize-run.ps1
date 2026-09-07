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
    elseif ($line -match '^STEADY_ALLOCATION pattern=(\w+) batchSize=(\d+) largestBatch=(\d+) records=261888 bytes=(\d+)') {
        if ($null -eq $case -or $case.Pattern -ne $Matches[1] -or $case.BatchSize -ne [int]$Matches[2] -or
            $null -ne $case.SteadyBytes) {
            throw "Unexpected or duplicate allocation result: $line"
        }
        $case.LargestBatch = [int]$Matches[3]
        $case.SteadyBytes = [long]$Matches[4]
    }
}
$expectedCases = @('Repeated:1', 'Repeated:16', 'Distinct:1', 'Distinct:16', 'PendingPairs:1', 'PendingPairs:16')
$actualCases = @($cases | ForEach-Object { "$($_.Pattern):$($_.BatchSize)" })
if ((($actualCases | Sort-Object) -join ',') -ne (($expectedCases | Sort-Object) -join ',')) {
    throw 'Unexpected benchmark cases.'
}
foreach ($case in $cases) {
    $expectedLargestBatch = if ($case.Pattern -eq 'PendingPairs') { $case.BatchSize } else { 1 }
    if ($case.WarmupIterations -ne 30 -or $case.WarmupSeconds -lt 20 -or $case.Samples -ne 15 -or
        $null -eq $case.SteadyBytes -or $case.SteadyBytes -ne 0 -or $case.LargestBatch -ne $expectedLargestBatch) {
        throw "Incomplete diagnostic case: $($case | ConvertTo-Json -Compress)"
    }
}
$cases | Export-Csv (Join-Path $PSScriptRoot 'warmup-verification.csv') -NoTypeInformation
$cases | Format-Table
