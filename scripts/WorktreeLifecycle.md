# Local worktree lifecycle

`AgentLocks.ps1 release` removes its recorded checkout before releasing ownership. Register the path once after checkout with `renew -LockName <name> -Worktree <absolute-path>`. Use the shared checkout's current script, and release in `finally` after stopping owned processes and saving evidence outside the worktree.

Local branches remain available for `git worktree add <path> <branch>`. Detached commits are retained as `retained-worktrees/<SHA>`. Uncommitted source, unknown ignored files, Git-locked worktrees, the main checkout and foreign repositories are preserved. Known generated output is disposable under `WorktreeCleanup.ps1`. Release prints why a checkout was retained.

A crashed process or an expired Redis TTL cannot run release. `Remove-MergedWorktrees.ps1` remains the conservative fallback for merged work. No GitHub Actions job manages local disk.

The sweep preserves live Redis ownership, including detached checkouts of main. It resolves the work-item identity from the per-worktree `agent.lockName` marker, or from a standard `pr-<N>-*` / `issue-<N>-*` directory before marker registration. For eligible worktrees, it acquires a temporary lease through the shared checkout's `AgentLocks.ps1`, keeps that lease through removal, then releases it. Ownership errors preserve the checkout. `-WhatIf` only reads ownership. Normal owner release still removes its own eligible checkout before releasing its lease.

Directories with dangling Git registration are reported and preserved: the sweep cannot verify their ownership marker or uncommitted source. Inspect these directories and retain valuable evidence before manual cleanup.

Run regression tests locally with PowerShell 7, Git and Docker:

```powershell
pwsh scripts/Test-ReleasedWorktreeCleanup.ps1
pwsh scripts/Test-WorktreeOwnershipCleanup.ps1
pwsh scripts/Test-WorktreeCleanup.ps1
pwsh scripts/Test-RemoveMergedWorktrees.ps1
```
