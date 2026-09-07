param(
    [string]$ParserPath = (Join-Path $PSScriptRoot 'summarize-run.ps1'),
    [string]$ArtifactRoot = (Join-Path $PSScriptRoot '.artifacts/parser-validation')
)
$ErrorActionPreference = 'Stop'
$raw = Get-Content (Join-Path $PSScriptRoot 'benchmark-measured-batched.log') -Raw
$allocation = 'STEADY_ALLOCATION pattern=PendingPairs batchSize=16 largestBatch=16 records=261888 bytes=0'
$lastCase = $raw.LastIndexOf('// Benchmark:')
$cases = @(
    @{ Name = 'valid'; Accept = $true; Log = $raw },
    @{ Name = 'duplicate-case'; Accept = $false; Log = $raw.Replace('[Pattern=Repeated, BatchSize=1]', '[Pattern=Distinct, BatchSize=1]').Replace('pattern=Repeated batchSize=1 largestBatch', 'pattern=Distinct batchSize=1 largestBatch') },
    @{ Name = 'missing-case'; Accept = $false; Log = $raw.Substring(0, $lastCase) },
    @{ Name = 'wrong-largest-batch'; Accept = $false; Log = $raw.Replace($allocation, $allocation.Replace('largestBatch=16', 'largestBatch=1')) },
    @{ Name = 'missing-allocation'; Accept = $false; Log = $raw.Replace($allocation, '') },
    @{ Name = 'wrong-allocation-case'; Accept = $false; Log = $raw.Replace($allocation, $allocation.Replace('pattern=PendingPairs', 'pattern=Distinct')) },
    @{ Name = 'duplicate-allocation'; Accept = $false; Log = $raw.Replace($allocation, "$allocation`n$allocation") }
)
$failures = [System.Collections.Generic.List[string]]::new()
foreach ($case in $cases) {
    $folder = Join-Path $ArtifactRoot ($case.Name + '-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    Copy-Item -LiteralPath $ParserPath -Destination (Join-Path $folder 'summarize-run.ps1')
    $case.Log | Set-Content (Join-Path $folder 'benchmark-measured-batched.log')
    & pwsh -NoProfile -File (Join-Path $folder 'summarize-run.ps1') *> (Join-Path $folder 'run.log')
    $accepted = $LASTEXITCODE -eq 0
    $csv = Join-Path $folder 'warmup-verification.csv'
    if ($accepted -ne $case.Accept -or (Test-Path $csv) -ne $case.Accept) {
        $failures.Add($case.Name)
    }
    elseif ($case.Accept) {
        $actual = Import-Csv $csv | ConvertTo-Json -Compress
        $expected = Import-Csv (Join-Path $PSScriptRoot 'warmup-verification.csv') | ConvertTo-Json -Compress
        if ($actual -ne $expected) { $failures.Add('valid-result-changed') }
    }
    Write-Output "$($case.Name): accepted=$accepted expected=$($case.Accept)"
}
if ($failures.Count -ne 0) { throw "Parser validation failed: $($failures -join ', ')" }
Write-Output "All $($cases.Count) parser scenarios passed."
