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
        Name = 'blocks before-merging action'
        Body = @'
## Review

This should be addressed before merging.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks prior-to-merge action'
        Body = @'
## Review

This must be fixed prior to merge.
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
        Name = 'allows positive category section headings'
        Body = @'
## Review

### Correctness
No bugs found. This is a faithful refactor.

### Design / architecture
No design issues found. This is a genuine improvement, not just churn.

### Zero-allocation / hot path
No concerns.

### Test coverage
Existing tests cover the behavior.

No blocking issues.
'@
        Blocks = $false
    },
    @{
        Name = 'allows positive category section heading with colon'
        Body = @'
## Review

### Correctness:
No issues found.
'@
        Blocks = $false
    },
    @{
        Name = 'blocks category section heading with finding body'
        Body = @'
## Review

### Correctness
`MetadataManager.RefreshAsync` leaks the cached `Task` when the request is
cancelled, causing a slow unbounded memory leak in long-running consumers.

### Design
The retry loop duplicates logic already present in `BackoffPolicy` and
should be consolidated.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks category section when all-clear is not first paragraph'
        Body = @'
## Review

### Correctness
This leaks cached tasks under cancellation.

No issues found after that.
'@
        Blocks = $true
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
    },
    @{
        Name = 'allows numbered non-blocking heading'
        Body = @'
## Review

### 1. Non-blocking: rename variable
This is optional.
'@
        Blocks = $false
    },
    @{
        Name = 'allows previously flagged issues verified fixed heading'
        Body = @'
## Review

### Previously-flagged issues - verified fixed

The prior findings are now resolved.
'@
        Blocks = $false
    },
    @{
        Name = 'blocks previously flagged issue still not fixed'
        Body = @'
## Review

### Previously-flagged issue - still not fixed

The finding remains open.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks previously flagged concerns not yet resolved'
        Body = @'
## Review

### Previously flagged concerns, not yet resolved

The finding remains open.
'@
        Blocks = $true
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
