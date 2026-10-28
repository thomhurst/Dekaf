# Assert-PrGreen.ps1
# Fail-closed merge gate for the issue-pr-loop skill.
#
# Exits 0 ONLY when PR #<N> is provably safe to merge:
#   - state OPEN and not a draft
#   - mergeable == MERGEABLE
#   - mergeStateStatus == CLEAN
#   - every current status check is terminal AND passing (CheckRun: status=COMPLETED and
#     conclusion in SUCCESS/SKIPPED/NEUTRAL; StatusContext: state=SUCCESS). Only the newest
#     execution of each check counts, so a run superseded on the same head (a skipped draft
#     run, or one cancelled by `converted_to_draft`) does not deny forever.
#   - the performance gate measured this head (its required check also passes a head pushed
#     after the PR left draft, which it does not measure)
#   - no current-head top-level review has actionable finding headings
#   - no unresolved review threads
#
# -ForAutoMerge is the gate for arming native auto-merge (Enable-AutoMerge.ps1). GitHub's
# auto-merge waits only for the `Main` ruleset's required checks, so this mode lets checks of
# the workflows those aggregate (CI, Performance gate, Dependency Review) still be pending, and
# nothing else:
#   - mergeStateStatus may be BLOCKED or UNSTABLE while required checks run
#   - every other check is terminal and passing, and no check has failed
#   - the performance gate is measuring (or has measured) this head
#   - the Claude review workflow has posted its review of the current head
#   - no actionable top-level review and no unresolved review threads
#
# Any other state — pending/queued/in-progress checks, an empty rollup, a failed
# check, an unresolved thread, or an API hiccup — exits 1 so the caller MUST NOT
# merge. The default on uncertainty is DENY. The caller still judges review content.
#
# Usage: pwsh scripts/Assert-PrGreen.ps1 -Pr 1234 [-Repo owner/name] [-ForAutoMerge]
# Exit:  0 = safe to merge (or arm); 1 = DO NOT MERGE (reason printed to stderr).

[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$Pr,
    [string]$Repo,
    [switch]$ForAutoMerge
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AssertPrGreenChecks.ps1')
. (Join-Path $PSScriptRoot 'AssertPrGreenReviewHeuristics.ps1')
. (Join-Path $PSScriptRoot 'AssertPrGreenReviewThreads.ps1')

function Deny([string]$reason) {
    [Console]::Error.WriteLine("DO NOT MERGE #${Pr} -- $reason")
    exit 1
}

$repoArgs = @()
if ($Repo) { $repoArgs = @('--repo', $Repo) }
if ($Repo) {
    $owner, $name = $Repo -split '/', 2
}
else {
    $nwo = gh repo view --json nameWithOwner --jq '.nameWithOwner' 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $nwo) { Deny "could not resolve the repository" }
    $owner, $name = $nwo.Trim() -split '/', 2
}

# Re-fetch fresh — survey output goes stale within seconds.
$raw = gh pr view $Pr @repoArgs --json number,state,isDraft,headRefOid,mergeable,mergeStateStatus,statusCheckRollup,latestReviews,commits 2>$null
if ($LASTEXITCODE -ne 0) { Deny "gh pr view failed (exit $LASTEXITCODE)" }
try {
    $view = $raw | ConvertFrom-Json
}
catch {
    Deny "could not parse PR state: $($_.Exception.Message)"
}

if ($view.state -ne 'OPEN') { Deny "state=$($view.state) (need OPEN)" }
if ($view.isDraft) { Deny "PR is a draft; mark it ready only after the draft review lifecycle approves its head" }
if ($view.mergeable -ne 'MERGEABLE') { Deny "mergeable=$($view.mergeable) (need MERGEABLE)" }
$allowedMergeStates = if ($ForAutoMerge) { @('CLEAN', 'BLOCKED', 'UNSTABLE', 'HAS_HOOKS') } else { @('CLEAN') }
if ($allowedMergeStates -notcontains $view.mergeStateStatus) {
    Deny "mergeStateStatus=$($view.mergeStateStatus) (need $($allowedMergeStates -join '/'))"
}
$head = [string]$view.headRefOid
if (-not $head) { Deny "PR state omitted headRefOid" }

$checks = @(Select-CurrentCheck @($view.statusCheckRollup))
if ($checks.Count -eq 0) {
    Deny "statusCheckRollup is empty -- no checks registered yet; refusing to merge blind"
}

$bad = @(Get-CheckDenial -Checks $checks -ForAutoMerge:$ForAutoMerge)
if ($bad.Count -gt 0) {
    Deny ("checks not green: " + ($bad -join '; '))
}

$unmeasured = @(Get-PerformanceGateDenial -Checks $checks -ForAutoMerge:$ForAutoMerge)
if ($unmeasured.Count -gt 0) { Deny ($unmeasured -join '; ') }

if ($ForAutoMerge) {
    $firstLines = @(gh api "repos/$owner/$name/issues/$Pr/comments" --paginate `
            --jq '.[] | select(.user.login == "github-actions[bot]") | .body | split("\n")[0]' 2>$null)
    if ($LASTEXITCODE -ne 0) { Deny "could not fetch PR comments (exit $LASTEXITCODE)" }
    $comments = @($firstLines | ForEach-Object { [pscustomobject]@{ user = [pscustomobject]@{ login = 'github-actions[bot]' }; body = $_ } })
    if (-not (Test-ClaudeReviewForHead -Comments $comments -Head $head)) {
        Deny "no Claude review of head $head yet; wait for it (or dispatch claude-code-review.yml with pr_number=$Pr) before arming auto-merge"
    }
}

$headCommit = @($view.commits | Where-Object { $null -ne $_ }) | Select-Object -Last 1
$headCommittedAt = if ($headCommit) { ConvertTo-UtcDateTimeOffset $headCommit.committedDate } else { $null }

# Top-level review comments are not review threads, so GitHub does not expose a
# resolved flag for them. Fail closed on common review-finding shapes instead
# of treating a COMMENTED review as automatically safe. A stale Claude review
# from before the current head commit is ignored only when the current-head
# claude-review check completed green.
$latestReviews = @($view.latestReviews | Where-Object { $null -ne $_ })
$requestedChanges = @($latestReviews | Where-Object { $_.state -eq 'CHANGES_REQUESTED' })
if ($requestedChanges.Count -gt 0) {
    $authors = ($requestedChanges | ForEach-Object { $_.author.login }) -join ', '
    Deny "latest review requests changes from: $authors"
}

$actionableReviews = @()
foreach ($review in $latestReviews) {
    if ($review.state -ne 'COMMENTED') { continue }
    if (Test-StaleBotReviewCanBeIgnored -Review $review -HeadCommitCommittedAt $headCommittedAt -Checks $checks) {
        continue
    }

    $body = [string]$review.body
    $reason = Get-ActionableReviewBodyReason -Body $body
    if ($reason) {
        $actionableReviews += "$($review.author.login): $reason"
    }
}
if ($actionableReviews.Count -gt 0) {
    Deny ("latest top-level review comment has actionable finding(s): " + ($actionableReviews -join '; '))
}

# Unresolved review threads block merge even when a review's state is COMMENTED.
$query = 'query($owner:String!,$repo:String!,$pr:Int!,$cursor:String){repository(owner:$owner,name:$repo){pullRequest(number:$pr){reviewThreads(first:100,after:$cursor){pageInfo{hasNextPage endCursor} nodes{isResolved}}}}}'
try {
    $reviewThreads = @(Get-AllReviewThreads -FetchPage {
        param($cursor)

        $ghArgs = @(
            'api', 'graphql',
            '-f', "query=$query",
            '-F', "owner=$owner",
            '-F', "repo=$name",
            '-F', "pr=$Pr"
        )
        if ($null -ne $cursor) {
            $ghArgs += @('-f', "cursor=$cursor")
        }

        $threadsRaw = & gh @ghArgs 2>$null
        if ($LASTEXITCODE -ne 0) {
            throw "gh api graphql failed (exit $LASTEXITCODE)."
        }

        $page = ($threadsRaw | ConvertFrom-Json).data.repository.pullRequest.reviewThreads
        if ($null -eq $page) {
            throw 'GraphQL response did not contain review threads.'
        }

        return $page
    })
}
catch {
    Deny "could not fetch or parse review threads: $($_.Exception.Message)"
}
$unresolved = @($reviewThreads | Where-Object { -not $_.isResolved }).Count
if ($unresolved -gt 0) { Deny "$unresolved unresolved review thread(s)" }

if ($ForAutoMerge) {
    Write-Host "OK #${Pr} -- MERGEABLE, no failed or unawaited checks, Claude reviewed $head, no unresolved threads. Safe to arm auto-merge."
}
else {
    Write-Host "OK #${Pr} -- MERGEABLE, CLEAN, $($checks.Count) check(s) green, no unresolved threads. Safe to merge."
}
exit 0
