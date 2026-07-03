$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AssertPrGreenReviewHeuristics.ps1')

$cases = @(
    @{
        Name = 'blocks correctness heading'
        Body = @'
## Review

### Correctness / Design — pool resize drops cached items
This needs a fix.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks numbered finding heading'
        Body = @'
## Review

### 1. `MetadataManager.cs` — shared MetadataManager leaks a background loop
Failure scenario follows.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks before-merge action'
        Body = @'
## Review

No correctness or security issues found.

Two things worth addressing before merge:
- Missing direct pool coverage.
'@
        Blocks = $true
    },
    @{
        Name = 'allows no-issue review'
        Body = @'
## Review

**Correctness:** No issues. No correctness or security issues found.

Scope check passed.
'@
        Blocks = $false
    },
    @{
        Name = 'allows optional follow-up'
        Body = @'
## Review

No issues found.

Minor/optional, not blocking:
- Worth a follow-up later, but not required for this PR.
'@
        Blocks = $false
    }
)

foreach ($case in $cases) {
    $reason = Get-ActionableReviewBodyReason -Body $case.Body
    $blocked = -not [string]::IsNullOrWhiteSpace($reason)
    if ($blocked -ne $case.Blocks) {
        throw "Case '$($case.Name)' expected Blocks=$($case.Blocks), got Blocks=$blocked reason='$reason'"
    }
}

Write-Host "OK review heuristic tests passed ($($cases.Count) cases)."
