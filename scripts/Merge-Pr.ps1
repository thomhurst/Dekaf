# Merge-Pr.ps1
# One-command, fail-closed merge for the issue-pr-loop skill:
#   1. runs the pure gate  Assert-PrGreen.ps1  (read-only; exits 0 only when green)
#   2. merges with  gh pr merge --squash --delete-branch  (only if the gate passed)
#   3. removes the PR's isolated worktree so it cannot accumulate on disk
#
# Cleanup is *part of* the merge command — an agent cannot merge and then forget
# to remove the worktree, because it is the same call. This is the durable fix for
# the worktree pile-up the prose-only rule could not guarantee.
#
# The gate stays a separate, pure predicate ON PURPOSE: Assert-PrGreen.ps1 must be
# safe to run as a check without side effects. Merge-Pr COMPOSES it; it does not
# fold merging into the assert.
#
# Worktree is resolved by matching the PR's head branch against `git worktree list`
# (robust to directory naming: pr-<N>-*, issue-<N>-*, etc.). A worktree with
# uncommitted TRACKED changes is PRESERVED, never force-discarded; untracked build
# artifacts (node_modules/bin/obj) are cleared.
#
# Usage:  pwsh scripts/Merge-Pr.ps1 -Pr 1234 [-Repo owner/name] [-Worktree <path>]
# Exit:   0 = merged (worktree removed, or preserved because dirty)
#         1 = NOT merged (gate denied, or merge failed) — nothing destroyed

[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$Pr,
    [string]$Repo,
    [string]$Worktree
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WorktreeCleanup.ps1')
$repoArgs = @(); if ($Repo) { $repoArgs = @('--repo', $Repo) }

function Fail([string]$msg) { [Console]::Error.WriteLine("MERGE ABORTED #${Pr} -- $msg"); exit 1 }

# --- 1. Gate: the pure predicate decides. No merge unless it exits 0. -----------
& pwsh (Join-Path $PSScriptRoot 'Assert-PrGreen.ps1') -Pr $Pr @repoArgs
if ($LASTEXITCODE -ne 0) { Fail "Assert-PrGreen denied (exit $LASTEXITCODE). Not merging." }

# Resolve the head branch BEFORE merging (--delete-branch removes it server-side).
$headRef = gh pr view $Pr @repoArgs --json headRefName --jq '.headRefName' 2>$null
if ($LASTEXITCODE -ne 0 -or -not $headRef) { Fail "could not resolve head branch (exit $LASTEXITCODE)" }
$headRef = $headRef.Trim()

# Main worktree path — git removals/prunes must run from a checkout that is NOT the
# one being removed; the first `worktree list` entry is always the main checkout.
$mainRepo = ((git worktree list --porcelain) | Where-Object { $_ -like 'worktree *' } |
    Select-Object -First 1) -replace '^worktree ', ''

# --- 2. Merge. -----------------------------------------------------------------
gh pr merge $Pr @repoArgs --squash --delete-branch
if ($LASTEXITCODE -ne 0) { Fail "gh pr merge failed (exit $LASTEXITCODE). Worktree untouched." }
Write-Host "Merged #${Pr} ($headRef)."

# --- 3. Remove the PR's worktree. ----------------------------------------------
if (-not $Worktree) {
    # Find the worktree whose checked-out branch matches the PR head branch.
    $wt = $null; $cur = $null
    foreach ($line in (git -C $mainRepo worktree list --porcelain)) {
        if ($line -like 'worktree *') { $cur = $line.Substring(9) }
        elseif ($line -eq "branch refs/heads/$headRef") { $wt = $cur; break }
    }
    $Worktree = $wt
}
if (-not $Worktree) {
    Write-Host "No isolated worktree found for branch '$headRef' (nothing to remove)."
    git -C $mainRepo worktree prune
    exit 0
}

Remove-MergedWorktree -Repo $mainRepo -Worktree $Worktree -Label "#${Pr}"
exit 0
