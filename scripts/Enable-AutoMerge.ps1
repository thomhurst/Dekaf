# Enable-AutoMerge.ps1
# Arms GitHub native auto-merge (squash) for PR #<N>, pinned to the reviewed head.
#
#   1. captures the head, then runs  Assert-PrGreen.ps1 -ForAutoMerge  (ready, no failed
#      check, nothing pending outside the required CI and Dependency Review workflows,
#      finished verdicts from the non-required path-filtered workflows the PR triggers, a
#      Claude review of this head, no unresolved threads) and stops on any denial
#   2. arms  gh pr merge --auto --squash --match-head-commit <head>
#
# GitHub then merges once the `Main` ruleset's required checks (`CI Passed`,
# `Dependency Review`) pass on that head. A draft run of ci.yml never reports the required
# `CI Passed` name, so arming right after `gh pr ready` waits for the ready run. A later push
# disarms auto-merge (the `disarm-auto-merge` job in ci.yml), so each new head needs its own
# review cycle and a new call to this script.
#
# The caller must read and dispose of every review body, inline finding and issue comment,
# including both required reviewers' verdicts on this head, BEFORE running this; the gate
# cannot judge review content.
#
# Auto-merge leaves the PR's worktree in place; scripts/Remove-MergedWorktrees.ps1 removes it
# after the merge. Use scripts/Merge-Pr.ps1 instead to merge an already green PR immediately.
#
# Usage: pwsh scripts/Enable-AutoMerge.ps1 -Pr 1234 [-Repo owner/name]
# Exit:  0 = armed; 1 = not armed (reason printed to stderr).

[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$Pr,
    [string]$Repo
)

$ErrorActionPreference = 'Stop'

function Fail([string]$reason) {
    [Console]::Error.WriteLine("NOT ARMED #${Pr} -- $reason")
    exit 1
}

$repoArgs = @()
if ($Repo) { $repoArgs = @('--repo', $Repo) }

# Capture the head before the gate so a push racing the gate cannot be armed unreviewed:
# --match-head-commit makes GitHub refuse if the head moved since.
$head = gh pr view $Pr @repoArgs --json headRefOid --jq '.headRefOid' 2>$null
if ($LASTEXITCODE -ne 0 -or -not $head) { Fail "could not read PR head (exit $LASTEXITCODE)" }
$head = $head.Trim()

$gateArgs = @('-Pr', $Pr, '-ForAutoMerge')
if ($Repo) { $gateArgs += @('-Repo', $Repo) }
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Assert-PrGreen.ps1') @gateArgs
if ($LASTEXITCODE -ne 0) { Fail "auto-merge gate denied (see above)" }

gh pr merge $Pr @repoArgs --auto --squash --match-head-commit $head
if ($LASTEXITCODE -ne 0) { Fail "gh pr merge --auto failed (exit $LASTEXITCODE)" }

Write-Host "Armed auto-merge for #${Pr} at $head."
exit 0
