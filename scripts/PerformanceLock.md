# Shared local performance lock

All agents on this machine use the same Redis lock, `performance`, through the **shared checkout's current** `scripts/AgentLocks.ps1`. Read this guide from that checkout before starting heavy local work, even when working on an older branch. The existing Redis backend, owner identity, token checks, and two-hour lease apply; do not create a per-PR, per-worktree, per-core, or alternative-backend performance lock.

## What must acquire it

Acquire `performance` before starting any local benchmark, stress run, profiling session, build, test run, package restore, docs build, or other sustained CPU-, memory-, or disk-intensive job. All these jobs use the same exclusive lock: merely checking that it is free before launching a build leaves a race with benchmark startup. Stop owned watch builds and other background workloads before releasing it. Lightweight editing, source inspection, and remote CI inspection can continue without it.

Hold the lock across the complete baseline/candidate/control experiment, including preparation, warmup, measurements, and any approved repeat. Do not run other heavy work alongside your own measurements. Acquire once at the outermost operation; nested scripts and child processes use that reservation and must not reacquire or release it. Do not leave heavy child processes running after the reservation ends.

A local reservation does not isolate a remote runner. Remote acceptance runs need their own otherwise idle runner; retain the existing stress-dispatch and paid-run rules.

## Ownership and cleanup

Keep the PR/issue lock for item ownership. Acquire that lock first, then `performance`; release `performance` before releasing the item lock. **Never pass `-Worktree` for `performance`**: worktree registration belongs only to the item lock, whose release may remove the checkout. Always pass the explicit `-LockName performance` on every verb.

Use the same stable owner identity on every call. Codex supplies `CODEX_THREAD_ID`; other automation supplies `-OwnerId` or `DEKAF_AGENT_LOCK_OWNER_ID`. Capture acquisition output without printing the cached token.

```powershell
# Absolute path captured from the shared checkout, not a branch copy.
$agentLocks = 'C:/git/Dekaf/scripts/AgentLocks.ps1'
$lockOutput = & pwsh -NoProfile -File $agentLocks acquire -LockName performance
$lockExit = $LASTEXITCODE
if ($lockExit -eq 3) {
    # Another operation owns the reservation. Defer heavy work; inspect/edit instead.
    return
}
if ($lockExit -ne 0) { throw 'Performance lock unavailable; do not start heavy work.' }

try {
    # Run from the intended isolated worktree. This example is one bounded build.
    & dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    $lockStatus = & pwsh -NoProfile -File $agentLocks status -LockName performance
    if ($LASTEXITCODE -ne 0 -or $lockStatus -ne 'HELD-BY-ME') {
        throw 'Performance ownership lost; any measurements are inconclusive.'
    }
}
finally {
    # First stop/observe all heavy processes and services started for this reservation.
    & pwsh -NoProfile -File $agentLocks release -LockName performance
    if ($LASTEXITCODE -ne 0) {
        throw 'Performance lock release failed; inspect ownership without deleting the Redis key.'
    }
}
```

Exit 3 on acquisition means defer, not bypass. Redis errors also prevent starting heavy work. Do not busy-poll or hold the reservation while waiting for CI, reviews, or user input. Never steal the key or stop another agent's processes.

The lease lasts two hours. Bound each uninterrupted command to finish within the remaining lease, leaving time for cleanup. Renew before a phase when needed with `pwsh $agentLocks renew -LockName performance`; keep the reservation between experiment phases. Do not start a command that can outlive the lease. No periodic heartbeat is required. Check ownership before each measurement phase and after completion. On expiry, failed ownership verification, or renew exit 4, stop your owned heavy work and mark any overlapping measurements inconclusive. A later reacquisition cannot validate the earlier interval.

## Measurement acceptance

The lock coordinates cooperating agents; it cannot stop already-running jobs or unrelated applications. Before timing, check for competing builds, tests, benchmarks, active Kafka containers, and memory pressure. Defer if another workload is active; stop only services you own. Agents already running when this policy changes must read it and finish or stop existing heavy work before the first isolated measurement.

Record the reserved interval, machine/runtime configuration, exact SHAs, and observed competing activity with the evidence. CPU affinity alone is not isolation. Measurements with overlapping heavy work or uncertain lease coverage are diagnostic and **INCONCLUSIVE**, not proof of regression or acceptance. Preserve previous results rather than relabeling them as passes. This reservation changes measurement conditions, not performance thresholds, zero-allocation requirements, or paid-run limits.
