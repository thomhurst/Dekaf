# Remove-MergedWorktrees.ps1
# Safety-net sweep for the issue-pr-loop skill: removes every worktree whose branch
# has already merged, regardless of who merged it (this loop, another agent, a human).
#
# SQUASH-SAFE DETECTION — this is the whole point:
#   A squash (or rebase) merge rewrites history, so the branch tip is NOT an ancestor
#   of main and `git merge-base --is-ancestor` reports "not merged". The only reliable,
#   ancestor-independent signal is GitHub's own record: a PR with state=MERGED for that
#   head branch. So this sweep keys off `gh pr list --state merged --json headRefName`.
#
#   DELIBERATELY NOT a signal: a [gone] upstream branch. [gone] only means the remote
#   ref was deleted — which also happens when a PR is CLOSED UNMERGED and its branch
#   pruned. Treating [gone] as "merged" wrongly reaps unmerged work (verified: a closed-
#   unmerged PR's worktree got flagged). Merged-PR state is the only trustworthy signal.
#
# Guards (never delete work):
#   - skip the main checkout
#   - skip detached worktrees (no branch to map to a PR) — reported for manual review
#   - skip a branch that still has an OPEN PR (branch reused for active work)
#   - PRESERVE any worktree with uncommitted tracked changes (shared helper)
#
# Run it once per loop iteration (cheap: ~3 gh calls total, not per-worktree).
#
# Usage:  pwsh scripts/Remove-MergedWorktrees.ps1 [-Repo owner/name] [-WhatIf]
# Exit:   0 always (a sweep failure must not break the loop; problems are logged)

[CmdletBinding()]
param(
    [string]$Repo,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WorktreeCleanup.ps1')
$repoArgs = @(); if ($Repo) { $repoArgs = @('--repo', $Repo) }

function Warn([string]$m) { [Console]::Error.WriteLine("sweep: $m") }

# "Exit 0 always" is load-bearing: a sweep failure must never kill an otherwise-healthy
# loop iteration. $ErrorActionPreference is 'Stop', so any unguarded throw (a git call
# failing, an unexpected gh output shape, a null string op) would exit non-zero. Wrap the
# whole body so every such error is logged and swallowed.
try {
    # Authoritative merge signal: merged-PR head branches (squash-safe).
    # --limit 1000 is far more than any realistic leftover window — worktrees linger hours,
    # not months, and Merge-Pr.ps1 already removes most the moment they merge. A worktree
    # whose merge fell outside the last 1000 merged PRs is implausible.
    $merged = @{}
    $rawMerged = gh pr list @repoArgs --state merged --limit 1000 --json headRefName --jq '.[].headRefName' 2>$null
    if ($LASTEXITCODE -ne 0) { Warn "could not list merged PRs (exit $LASTEXITCODE) -- skipping sweep this round"; exit 0 }
    foreach ($b in $rawMerged) { if ($b) { $merged[$b.Trim()] = $true } }

    # Open-PR head branches: never remove a worktree whose branch is actively open.
    $open = @{}
    $rawOpen = gh pr list @repoArgs --state open --limit 1000 --json headRefName --jq '.[].headRefName' 2>$null
    foreach ($b in $rawOpen) { if ($b) { $open[$b.Trim()] = $true } }

    $mainRepo = ((git worktree list --porcelain) | Where-Object { $_ -like 'worktree *' } |
        Select-Object -First 1) -replace '^worktree ', ''

    # Walk worktrees (porcelain: worktree / branch|detached records).
    $wts = @(); $cur = $null; $branch = $null; $detached = $false
    foreach ($line in (git -C $mainRepo worktree list --porcelain)) {
        if ($line -like 'worktree *') { $cur = $line.Substring(9); $branch = $null; $detached = $false }
        elseif ($line -like 'branch *') { $branch = ($line.Substring(7) -replace '^refs/heads/', '') }
        elseif ($line -eq 'detached') { $detached = $true }
        elseif ($line -eq '') { if ($cur) { $wts += [pscustomobject]@{ Path = $cur; Branch = $branch; Detached = $detached } }; $cur = $null }
    }
    if ($cur) { $wts += [pscustomobject]@{ Path = $cur; Branch = $branch; Detached = $detached } }

    $removed = 0
    foreach ($w in $wts) {
        if ($w.Path -eq $mainRepo) { continue }
        if ($w.Detached) { Write-Host "sweep: skipping detached worktree (manual review): $($w.Path)"; continue }
        if ($open.ContainsKey($w.Branch)) { continue }                 # active open PR — keep
        if (-not $merged.ContainsKey($w.Branch)) { continue }          # only remove on a MERGED PR

        if ($WhatIf) { Write-Host "sweep: WOULD remove $($w.Path) (branch $($w.Branch))"; continue }
        Remove-MergedWorktree -Repo $mainRepo -Worktree $w.Path -Label "($($w.Branch))"
        if (-not (Test-Path -LiteralPath $w.Path)) { $removed++ }
    }
    git -C $mainRepo worktree prune
    Write-Host "sweep: removed $removed merged worktree(s)."
}
catch {
    Warn "unexpected sweep error (ignored, loop continues): $_"
}
exit 0
