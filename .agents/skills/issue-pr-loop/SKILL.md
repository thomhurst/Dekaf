---
name: issue-pr-loop
description: "Run the GitHub issue/PR queue autonomously when the user invokes /issue-pr-loop or requests continuous queue work: maintain PRs, claim issues, implement in isolated worktrees, and ship until stopped."
---

# Issue/PR Loop

Maintain open PRs before starting issues. Complete one unit per iteration: merge a ready PR, push fixes to an actionable PR, or claim an issue and open its PR. Then survey again. Pending CI/review means work another item; it is not completion.

This workflow runs unattended. Defer unsafe or externally blocked items with a GitHub comment, release their locks, and continue without blocking questions. Stop only when the user stops/pauses the loop or no queueable issue or actionable PR remains and remaining work requires an external decision/dependency. If only CI/review is pending, re-survey; monitoring is a last resort when no other work is available. Do not watch, sleep, poll a single item, or schedule wake-ups while other work is queueable. Use commentary for progress; do not send a final wrap-up while the loop remains active.

If the user authorizes subagents, delegate one iteration per subagent with this skill, the repo path, and the completion contract below. Otherwise run locally.

## Isolation and ownership

At each survey, run `pwsh scripts/Remove-MergedWorktrees.ps1` from the shared checkout. It safely reclaims merged-PR worktrees, including squash merges. Do not infer merge status from ancestry or a `[gone]` branch.

Use the shared checkout only for read-only surveys and worktree setup. Make edits, builds, tests, rebases, commits, and pushes in an isolated worktree under sibling `Dekaf-worktrees` (fallback: `C:\tmp\Dekaf-worktrees`). Set every mutating tool call's `workdir` explicitly; shell directory changes do not persist across calls. If a path exists, choose a unique one.

Capture the canonical lock script before entering a worktree; branch copies may use incompatible token-cache formats:

```powershell
$repo = git rev-parse --show-toplevel
$agentLocks = Join-Path $repo 'scripts/AgentLocks.ps1'
$worktreeRoot = Join-Path (Split-Path $repo -Parent) 'Dekaf-worktrees'
New-Item -ItemType Directory -Force $worktreeRoot | Out-Null
```

Acquire a Redis lock before touching an item: `pr-<N>` for any PR work, `issue-<N>` for new implementation. Use the absolute `$agentLocks` path for every verb.

| Command | Result |
| --- | --- |
| `pwsh $agentLocks acquire -LockName $lockName` | Exit 0: acquired. Exit 3: held, skip item. Other failure: skip; do not bypass Redis. |
| `pwsh $agentLocks release -LockName $lockName` | Run in cleanup/finally. Exit 0 confirms release; exit 5 means stale token, leave the key alone. |
| `pwsh $agentLocks renew -LockName $lockName` | Optional for work exceeding the 2-hour TTL, not a periodic heartbeat. Exit 4 means lost ownership: stop this item and do not push. |
| `pwsh $agentLocks status -LockName $lockName` | Read-only `FREE` / `HELD` / `HELD-BY-ME`. |

Redis is the ownership authority. Do not remove another agent's lock, use PID liveness to infer abandonment, or implement a second lock backend. The script privately caches tokens; do not echo or manage them. Codex supplies `CODEX_THREAD_ID`; other automation needs a stable unique `DEKAF_AGENT_LOCK_OWNER_ID` or consistent `-OwnerId` across commands. Locks expire after 2 hours. The issue's `in-progress` label persists independently.

After claiming an issue, create branch `issue-<N>-<short-desc>` from freshly fetched `origin/main` with `git worktree add`. For PR fixes, create a detached worktree from `origin/main`, then run `gh pr checkout <N>` with that worktree as `workdir`.

Before the first file edit and after changing checkout, verify from the intended worktree:

```powershell
$actualRoot = [System.IO.Path]::GetFullPath((git rev-parse --show-toplevel))
$expectedRoot = [System.IO.Path]::GetFullPath($worktree)
if (-not [string]::Equals($actualRoot, $expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Wrong checkout: $actualRoot; expected $expectedRoot"
}
git status --short --branch
```

Only remove an abandoned worktree manually when it has no uncommitted work. Merge cleanup belongs to the repo scripts. Dekaf has no Aspire AppHost; tests use Docker/Testcontainers. Stop only processes and containers started for this work; never blanket-delete containers or volumes.

## Maintain PRs

Survey with `gh pr list --author @me --state open --json number,title,headRefName,mergeable,mergeStateStatus,reviewDecision,statusCheckRollup,isDraft`.

For each PR inspect all review surfaces, paginating as needed:

- `gh api repos/{owner}/{repo}/pulls/{N}/reviews`
- `gh api repos/{owner}/{repo}/pulls/{N}/comments`
- `gh api repos/{owner}/{repo}/issues/{N}/comments`
- GraphQL `reviewThreads`: IDs, resolution state, and comment bodies/authors/timestamps.

A `COMMENTED` review body can contain blocking findings even with zero unresolved threads. Treat concerns, suggestions, `CHANGES_REQUESTED`, unreplied current inline comments (`position != null`), and unresolved threads as actionable. Empty/boilerplate review bodies are non-blocking; uncertain findings block merging.

Prioritize:

1. **Merge:** Open, `MERGEABLE`, `CLEAN`; every check completed with `SUCCESS`, `SKIPPED`, or `NEUTRAL`; no unresolved threads, unaddressed inline comments, or outstanding review-body concerns; approved if required; and a bot CI/review cycle after the last fix push. Recent merged PRs without approval can indicate that human approval is not required. Merge only through the gate below.
2. **Fix:** Rebase conflicts onto fresh `origin/main`, investigate failed CI logs, or address review findings. Reply to each finding with its fix or technical disposition and push. Let another bot CI/review cycle complete before merging in a later iteration. Investigate flakes under `CLAUDE.md`; do not rerun for green.
3. **Pending:** Move to another PR or issue.

Resolve a bot thread autonomously only when you replied with a fix commit or concrete disposition, the current head contains the fix, and a subsequent bot review/CI cycle did not rebut it. A current-head `REVIEW_VERDICT: CLEAR` is strongest evidence. Do not resolve newer rebuttals, unaddressed findings, or human threads awaiting response. After resolving eligible threads via GraphQL `resolveReviewThread`, re-fetch review/check state.

Discuss technical disagreements once; implement reaffirmed feedback subject to `CLAUDE.md` performance requirements. If conflicts or failures cannot be resolved safely, record the blocker, abort any unfinished rebase, release the lock, and take another item.

### Merge gate

```powershell
pwsh scripts/Merge-Pr.ps1 -Pr <N>
```

This wrapper re-fetches state through `Assert-PrGreen.ps1`, squash-merges only on a passing gate, then cleans up the worktree and head branches. Confirm judgment-based review signals yourself; a passing mechanical gate is necessary but insufficient.

Never call `gh pr merge` directly or enable `--auto`. Nonterminal checks block merging. A nonzero wrapper exit means skip the item; do not bypass the gate or retry blindly. Cleanup warnings after a successful merge do not justify retrying the merge. Dirty worktrees and their branches are preserved by the script.

## Pick an issue

List open, unassigned issues without `in-progress`; create that label if absent. Skip `wontfix`, `duplicate`, `question`, claimed items, and external blockers. Prefer clear, smaller tasks with fewer discussion comments; size alone is not a reason to stop when larger work remains.

After acquiring the issue lock, re-fetch state, assignees, and labels. If still open, unassigned, and unclaimed, add `in-progress` and confirm it before branching. Keep the label while its PR is open; remove it if abandoning the issue before opening a PR.

Inspect source, tests, docs, and tools to establish full scope. A missing API may be the requested implementation. Complete coherent issues in one PR; `Closes #<N>` must cover all acceptance criteria.

For multiple independent deliverables, split into focused native GitHub sub-issues and implement the first unblocked child in the same iteration. Inspect existing children (including closed ones) and dependencies first to avoid duplication:

```powershell
gh api repos/thomhurst/Dekaf/issues/<N>/sub_issues --paginate
gh api repos/thomhurst/Dekaf/issues/<N>/dependencies/blocked_by
```

Use native parent/child and `blocked_by` relationships rather than checklist-only tracking. Linking endpoints are `POST issues/<parent>/sub_issues` with `-F sub_issue_id=<id>` and `POST issues/<blocked>/dependencies/blocked_by` with `-F issue_id=<prerequisite-id>` under `repos/thomhurst/Dekaf/`. They require numeric REST issue `.id`, not issue number or GraphQL node ID. Leave the parent open, remove its claim if splitting, and claim a child only when all its blockers are closed.

## Completion contract

Follow `CLAUDE.md` for testing, performance evidence, and final code review. Use TDD for implementation where feasible; explain exceptions. Run focused and relevant broader tests, plus `npm run build --prefix docs` for docs-site changes. State unavailable validation in the PR.

A code unit is complete only after commit, successful push, and PR creation/update. Create normal ready-for-review PRs unless the user requests a draft. Reference the issue in commits and include `Closes #<N>` on its own line in the PR body.

- Use non-interactive commit messages (`-m`, `--file`, or `--amend --no-edit`). Disable editors for rebase and continuation: `git -c core.editor=true -c sequence.editor=true rebase ...`.
- After rebasing, inspect the diff against `origin/main` for lost changes at shared insertion points.
- Use `git push --force-with-lease`, never bare `--force`.
- Confirm `gh pr view <N> --json headRefOid` matches local `git rev-parse HEAD` before reporting a pushed result.
- If pushing fails or ownership is lost, report the unit as failed, document the blocker, release the lock, and continue. Local edits or a green build alone are not completion.

Return delegated results only after confirmation: `merged #N`, `pushed fixes to #N`, `opened #N closing #M`, or `failed #N: reason`. The coordinating loop immediately begins its next survey.
