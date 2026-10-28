# Exercises the merge/auto-merge check gate in AssertPrGreenChecks.ps1 and pins the workflow
# contracts it relies on, without GitHub.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AssertPrGreenChecks.ps1')
$workflows = Join-Path $PSScriptRoot '../.github/workflows'

function Assert-Equal($Actual, $Expected, [string]$Message) {
    $actualJson = ConvertTo-Json -InputObject $Actual -Compress -Depth 10
    $expectedJson = ConvertTo-Json -InputObject $Expected -Compress -Depth 10
    if ($actualJson -ne $expectedJson) { throw "$Message. Expected $expectedJson, got $actualJson." }
}

function New-Check([string]$Name, [string]$Workflow, [string]$Status, [string]$Conclusion = '', $StartedAt = '2026-10-28T12:00:00Z') {
    [pscustomobject]@{ __typename = 'CheckRun'; name = $Name; workflowName = $Workflow; status = $Status; conclusion = $Conclusion; startedAt = $StartedAt }
}

# Superseded executions on the same head: the newest per workflow and name wins.
$draftSkip = New-Check 'Detect Changes' 'CI' 'COMPLETED' 'SKIPPED' '2026-10-28T10:00:00Z'
$cancelled = New-Check 'Detect Changes' 'CI' 'COMPLETED' 'CANCELLED' '2026-10-28T11:00:00Z'
$current = New-Check 'Detect Changes' 'CI' 'COMPLETED' 'SUCCESS' ([DateTime]::new(2026, 10, 28, 12, 0, 0, [DateTimeKind]::Utc))
$selected = @(Select-CurrentCheck @($cancelled, $current, $draftSkip))
Assert-Equal $selected.Count 1 'Only the newest execution of a check counts'
Assert-Equal $selected[0].conclusion 'SUCCESS' 'A cancelled or skipped superseded execution must not deny'
$queued = New-Check 'Detect Changes' 'CI' 'QUEUED' '' '0001-01-01T00:00:00Z'
Assert-Equal (@(Select-CurrentCheck @($current, $queued))[0].status) 'QUEUED' 'A check that has not started is the newest execution'
$status = [pscustomobject]@{ __typename = 'StatusContext'; context = 'legacy'; state = 'SUCCESS' }
Assert-Equal (@(Select-CurrentCheck @($status, $current)).Count) 2 'Status contexts pass through'
Assert-Equal (@(Select-CurrentCheck @(New-Check 'Build' 'CI' 'COMPLETED' 'SUCCESS'; New-Check 'Build' 'Other' 'COMPLETED' 'FAILURE')).Count) 2 'Same-named checks of different workflows are distinct'

# Merge mode: everything terminal and passing.
$green = @((New-Check 'CI Passed' 'CI' 'COMPLETED' 'SUCCESS'), (New-Check 'Docs' 'CI' 'COMPLETED' 'SKIPPED'), (New-Check 'CodeRabbit' '' 'COMPLETED' 'NEUTRAL'))
Assert-Equal @(Get-CheckDenial $green).Count 0 'Passing, skipped and neutral checks allow a merge'
$pendingCi = New-Check 'Build & Pack' 'CI' 'IN_PROGRESS'
Assert-Equal @(Get-CheckDenial @($pendingCi)) @('Build & Pack: state=IN_PROGRESS') 'Merge mode denies pending checks'
Assert-Equal @(Get-CheckDenial @([pscustomobject]@{ context = 'legacy'; state = 'PENDING' })) @('legacy: state=PENDING') 'Merge mode denies a pending status context'

# Auto-merge mode: only the required workflows may still be pending.
Assert-Equal @(Get-CheckDenial @($pendingCi, (New-Check 'Dependency Review' 'Dependency Review' 'QUEUED')) -ForAutoMerge).Count 0 'Required workflows may be pending when arming'
Assert-Equal @(Get-CheckDenial @(New-Check 'Unit Tests' 'CI' 'COMPLETED' 'FAILURE') -ForAutoMerge) @('Unit Tests: state=FAILURE') 'A failed check denies arming'
Assert-Equal @(Get-CheckDenial @(New-Check 'Micro A1/B/A2 screen' 'Performance gate' 'IN_PROGRESS') -ForAutoMerge) @('Micro A1/B/A2 screen: state=IN_PROGRESS') 'A pending non-required check denies arming'
Assert-Equal @(Get-CheckDenial @(New-Check 'Greptile Review' '' 'IN_PROGRESS') -ForAutoMerge) @('Greptile Review: state=IN_PROGRESS') 'A pending app check denies arming'

# Path filters and deferred verdicts, read from the real workflows.
$filters = @($DeferredVerdictWorkflows | ForEach-Object { Get-WorkflowPathFilter -Path (Join-Path $workflows $_) })
Assert-Equal @($filters.Name) @('Performance gate', 'Performance tooling tests', 'Review helper tests') 'Deferred-verdict workflows are read by name'
$gate = $filters[0]
Assert-Equal ($gate.Patterns -contains 'src/**') $true 'The performance gate filters on src/**'
Assert-Equal (Test-PathMatchesPattern -Path 'src/Dekaf/Producer/A.cs' -Patterns $gate.Patterns) $true 'src/** matches nested files'
Assert-Equal (Test-PathMatchesPattern -Path 'docs/docs/intro.md' -Patterns $gate.Patterns) $false 'Docs do not trigger the performance gate'
Assert-Equal (Test-PathMatchesPattern -Path 'global.json' -Patterns $gate.Patterns) $true 'Exact root files match'
Assert-Equal (Test-PathMatchesPattern -Path 'a.csproj' -Patterns @('**/*.csproj')) $true '**/ also matches the root'
Assert-Equal (Test-PathMatchesPattern -Path 'x/global.json.bak' -Patterns @('global.json')) $false 'Patterns are anchored'

$hotPath = @('src/Dekaf/Producer/A.cs')
$draftOnly = @((New-Check 'Select benchmark classes' 'Performance gate' 'COMPLETED' 'SKIPPED'), (New-Check 'test' 'Performance tooling tests' 'COMPLETED' 'SKIPPED'))
Assert-Equal @(Get-DeferredVerdictDenial -Checks $draftOnly -ChangedFiles $hotPath -Workflows $filters).Count 2 'Skipped draft runs are not a verdict'
Assert-Equal @(Get-DeferredVerdictDenial -Checks @() -ChangedFiles $hotPath -Workflows $filters).Count 2 'A workflow that has not registered yet is not a verdict'
$measuring = @((New-Check 'Select benchmark classes' 'Performance gate' 'COMPLETED' 'SUCCESS'), (New-Check 'Micro A1/B/A2 screen' 'Performance gate' 'QUEUED'),
    (New-Check 'test' 'Performance tooling tests' 'COMPLETED' 'SUCCESS'))
Assert-Equal @(Get-DeferredVerdictDenial -Checks $measuring -ChangedFiles $hotPath -Workflows $filters).Count 1 'A measurement in flight is not a verdict'
$measured = @($measuring[0], (New-Check 'Micro A1/B/A2 screen' 'Performance gate' 'COMPLETED' 'SUCCESS'), $measuring[2])
Assert-Equal @(Get-DeferredVerdictDenial -Checks $measured -ChangedFiles $hotPath -Workflows $filters).Count 0 'Finished runs are verdicts'
Assert-Equal @(Get-DeferredVerdictDenial -Checks @() -ChangedFiles @('README.md') -Workflows $filters).Count 0 'Untriggered workflows need no verdict'

# Claude review of the current head.
$review = [pscustomobject]@{ user = [pscustomobject]@{ login = 'github-actions[bot]' }; body = "<!-- REVIEW_RUN: 1/1; HEAD: abc -->`nLooks good." }
Assert-Equal (Test-ClaudeReviewForHead -Comments @($review) -Head 'abc') $true 'A review comment for the head counts'
Assert-Equal (Test-ClaudeReviewForHead -Comments @($review) -Head 'def') $false 'A review of an older head does not count'
$forged = [pscustomobject]@{ user = [pscustomobject]@{ login = 'someone' }; body = $review.body }
Assert-Equal (Test-ClaudeReviewForHead -Comments @($forged) -Head 'abc') $false 'Only the workflow account posts reviews'

# Workflow contracts the auto-merge gate relies on.
$ci = Get-Content -Raw (Join-Path $workflows 'ci.yml')
Assert-Equal ([regex]::Match($ci, '(?m)^name:\s*(.+)$').Groups[1].Value.Trim()) 'CI' 'ci.yml is the awaited CI workflow'
Assert-Equal ([regex]::Match((Get-Content -Raw (Join-Path $workflows 'dependency-review.yml')), '(?m)^name:\s*(.+)$').Groups[1].Value.Trim()) 'Dependency Review' 'dependency-review.yml is the awaited Dependency Review workflow'
Assert-Equal (@($AutoMergeAwaitedWorkflows) -join ',') 'CI,Dependency Review' 'Only required-check workflows are awaited'
$aggregator = [regex]::Match($ci, '(?ms)^  ci-passed:\r?\n(.*?)(?=^  \S|\z)').Groups[1].Value
if (-not $aggregator) { throw 'ci.yml has no ci-passed job' }
if ($aggregator -notmatch "(?m)^    name: \$\{\{ needs\.changes\.result == 'success' && 'CI Passed' \|\| 'CI Passed \(incomplete\)' \}\}\s*$") {
    throw 'The required CI Passed name must be computed so a skipped draft run cannot report it'
}
if ($aggregator -notmatch "(?m)^    if: always\(\) && \(github\.event_name != 'pull_request' \|\| github\.event\.pull_request\.draft == false\)\s*$") {
    throw 'ci-passed must skip draft PR events'
}
$jobsSection = [regex]::Match($ci, '(?ms)^jobs:\s*$(.*)').Groups[1].Value
$jobs = @([regex]::Matches($jobsSection, '(?m)^  ([A-Za-z0-9_-]+):\s*$') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -ne 'ci-passed' })
$needs = @(([regex]::Match($aggregator, '(?m)^    needs: \[(.*)\]').Groups[1].Value -split ',') | ForEach-Object { $_.Trim() })
$missing = @($jobs | Where-Object { $needs -notcontains $_ })
Assert-Equal $missing @() 'ci-passed must need every ci.yml job, or the required check misses its failure'

Write-Host 'Assert-PrGreen check gate tests passed.'
