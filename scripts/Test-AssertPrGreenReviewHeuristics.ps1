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
        Name = 'allows positive category heading verdict'
        Body = @"
## Review

### Correctness $([char]0x2014) looks right
- The core fix is correct.

No blocking issues found.
"@
        Blocks = $false
    },
    @{
        Name = 'allows verified category heading parenthetical'
        Body = @'
## Review

### Correctness (verified against code, not just description)
- Confirmed the fix runs outside the lock.

No blocking issues found.
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
        Name = 'allows positive correctness section with sound fix'
        Body = @'
## Review

### Correctness
The core fix is sound: the guard now checks the right invariant.

No blocking issues.
'@
        Blocks = $false
    },
    @{
        Name = 'allows positive category heading verdict with colon'
        Body = @'
## Review

### Correctness: No issues found.
'@
        Blocks = $false
    },
    @{
        Name = 'allows positive category heading bare verdict'
        Body = @'
## Review

### Correctness / Security No bugs found.
'@
        Blocks = $false
    },
    @{
        Name = 'allows positive design architecture paragraph'
        Body = @'
## Review

### Design / Architecture
Genuine improvement, not just churn: this removes duplicate shim code.

No blocking issues.
'@
        Blocks = $false
    },
    @{
        Name = 'allows positive design conventions paragraph'
        Body = @'
## Review

### Design / CLAUDE.md conventions
- Fix is scoped to only the paths that needed it.

No blocking issues.
'@
        Blocks = $false
    },
    @{
        Name = 'allows positive design compliance paragraph'
        Body = @'
## Review

### Design / CLAUDE.md compliance
- Fix is scoped to only the paths that needed it.

No blocking issues.
'@
        Blocks = $false
    },
    @{
        Name = 'allows allocation-free design compliance paragraph'
        Body = @'
## Review

### Design / CLAUDE.md compliance
`ValidateReadableLength` is a small, allocation-free helper reused consistently.

No outstanding issues.
'@
        Blocks = $false
    },
    @{
        Name = 'blocks positive phrase followed by incorrect scope'
        Body = @'
## Review

### Correctness - fix is scoped incorrectly, still misses the edge case for empty batches
'@
        Blocks = $true
    },
    @{
        Name = 'blocks genuine improvement followed by race'
        Body = @'
## Review

### Design / Architecture
Genuine improvement in the abstraction, but this introduces a new race condition when two threads call Dispose concurrently.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks sound fix followed by leak'
        Body = @'
## Review

### Correctness
The core fix is sound for the happy path, but it leaks a socket on the cancellation branch.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks allocation-free helper followed by unsafe text'
        Body = @'
## Review

### Design / CLAUDE.md compliance
Nice allocation-free helper, but it is not thread-safe and will corrupt the buffer under concurrent access.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks not-a-regression heading with real bug'
        Body = @'
## Review

### Not a regression, but this is a real bug that leaks memory
'@
        Blocks = $true
    },
    @{
        Name = 'blocks category parenthetical finding'
        Body = @'
## Review

### Correctness (pool resize drops cached items)
This needs a fix.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks category bare finding'
        Body = @'
## Review

### Correctness / Security leaks buffers
This needs a fix.
'@
        Blocks = $true
    },
    @{
        Name = 'blocks design conventions finding'
        Body = @'
## Review

### Design / CLAUDE.md conventions
This introduces a broad abstraction that should be fixed.
'@
        Blocks = $true
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
        Name = 'allows not a regression heading'
        Body = @'
## Review

### Not a regression, just noting scope

This matches pre-PR behavior.
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
        Name = 'allows fix resolves issue heading'
        Body = @'
## Review

### Fix in `62546f5` resolves the concurrency issue

The new commit avoids the unsafe queue path.
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
        Name = 'blocks fix still not resolving issue heading'
        Body = @'
## Review

### Fix still does not resolve the concurrency issue

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
