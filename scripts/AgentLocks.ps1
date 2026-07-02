# AgentLocks.ps1
# Reusable Redis work-item locks for the issue-pr-loop skill (concurrent agents).
#
# Replaces the ~100 lines of inline PowerShell the skill used to redefine on every
# acquire. Each verb emits ONE line (or nothing) so a loop iteration spends a
# handful of tokens on locking instead of dumping function bodies + heartbeat noise
# into the transcript.
#
# The lock is a machine-level mutex that AUTO-EXPIRES after TTL (see $lockTtlSeconds,
# 2h). Agents do NOT heartbeat it periodically — they just `release` when done, or let
# it expire if they die. The cross-process claim that outlives the lock is GitHub's
# in-progress label / open PR, so a lock expiring under a slow-but-live agent is
# backstopped there; `renew` exists only for the rare unit that runs past the TTL.
#
# The acquiring process's token is cached in a per-machine temp state file keyed by
# lock name, so renew/release do NOT need the token passed back — the agent only ever
# passes -LockName. Redis remains the sole ownership authority (token-checked EVAL on
# release/renew); the state file is just this process's private cache of its own
# token, never an ownership signal. Only the lock holder ever writes it (a losing
# acquire prints HELD and writes nothing), so concurrent agents cannot clobber each
# other's token.
#
# Verbs:
#   acquire  -LockName pr-1234 [-Worktree <path>]
#              stdout = token, exit 0    -> acquired (auto-expires after TTL)
#              stderr = "HELD", exit 3   -> already locked by someone; skip
#   release  [-LockName pr-1234]
#              exit 0 (silent)           -> released
#              stderr = "STALE", exit 5  -> token mismatch/expired; do NOT delete or
#                                           overwrite; just skip
#   renew    -LockName pr-1234 [-Worktree <path>]   (OPTIONAL — NOT periodic)
#              exit 0 (silent)           -> TTL re-armed. Call only for a unit that may
#                                           run past the TTL, or once with -Worktree to
#                                           write the auto-derive marker.
#              stderr = "LOST", exit 4   -> ownership lost; abandon the item
#   status   [-LockName pr-1234]
#              stdout = HELD-BY-ME | HELD | FREE, exit 0
#
# Common failure exit: 1 (docker/redis unavailable, or no cached token for
# renew/release). 2 = bad usage / lock name could not be resolved.
#
# Lock-name resolution (renew/release/status): -LockName is OPTIONAL there. When
# omitted it is resolved from, in order: (1) a marker persisted in the current
# worktree's git config (`agent.lockName`), which `acquire`/`renew -Worktree` writes;
# then (2) an `issue-<N>-*` branch name -> `issue-<N>`. `acquire` still REQUIRES an
# explicit name (or an `issue-<N>-*` branch): it runs before the worktree exists, and
# a PR lock's number (`pr-<N>`) is not derivable from the branch (`issue-<M>-...`).
# For PR locks either pass -LockName to release, or write the marker once via
# `renew -Worktree` (or `acquire -Worktree` if the worktree already exists).
#
# Usage:
#   $token = pwsh scripts/AgentLocks.ps1 acquire -LockName pr-1234
#   if ($LASTEXITCODE -ne 0) { <skip this item> }
#   ... do the unit of work — NO periodic refresh ...
#   pwsh scripts/AgentLocks.ps1 release -LockName pr-1234
#   # optional, only if the unit may exceed the TTL:
#   pwsh scripts/AgentLocks.ps1 renew -LockName pr-1234

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('acquire', 'release', 'renew', 'status')]
    [string]$Verb,

    [string]$LockName = '',
    [string]$Worktree = ''
)

$ErrorActionPreference = 'Stop'

$redisContainer = 'dekaf-agent-locks-redis'
$lockTtlSeconds = 7200 # 2 hours: the dead-man's switch. Longer than nearly every
# unit of work; a dead agent frees the item within this window. No periodic
# heartbeat — use `renew` only for the rare unit that runs past it.
$stateDir = Join-Path ([System.IO.Path]::GetTempPath()) 'dekaf-agent-locks'

function Die([int]$code, [string]$msg) { if ($msg) { [Console]::Error.WriteLine($msg) }; exit $code }

function Resolve-LockName([string]$Explicit) {
    if ($Explicit) { return $Explicit }
    # 1. marker persisted in the current worktree by a prior `acquire`/`renew -Worktree`.
    $cfg = (git config --local --get agent.lockName 2>$null)
    if ($LASTEXITCODE -eq 0 -and $cfg) { return $cfg.Trim() }
    # 2. derive issue-<N> from an issue-<N>-<desc> branch (never yields pr-<N>).
    $branch = (git rev-parse --abbrev-ref HEAD 2>$null)
    if ($LASTEXITCODE -eq 0 -and $branch -match '^issue-(\d+)-') { return "issue-$($Matches[1])" }
    return $null
}

$LockName = Resolve-LockName $LockName
if (-not $LockName) {
    Die 2 "cannot resolve lock name: pass -LockName (e.g. pr-1234 / issue-1234), or run inside an issue-<N>-* worktree"
}

$lockKey = "dekaf:agent-lock:$LockName"
$metaKey = "dekaf:agent-lock-meta:$LockName"
$tokenFile = Join-Path $stateDir ("{0}.token" -f ($LockName -replace '[\\/:*?"<>| ]', '_'))

function Ensure-AgentRedis {
    # One inspect tells missing vs stopped vs running: error exit = missing,
    # 'false' = stopped, 'true' = running. Warm path (the common loop case) costs
    # this single call — no separate `docker ps` or a redundant PING; the verb's
    # own Redis command is the connectivity check.
    $running = docker inspect -f '{{.State.Running}}' $redisContainer 2>$null
    if ($LASTEXITCODE -eq 0 -and $running -eq 'true') { return }

    if ($LASTEXITCODE -ne 0) {
        docker run -d --name $redisContainer redis:7-alpine redis-server --save '' --appendonly no | Out-Null
    }
    else {
        docker start $redisContainer | Out-Null
    }
    # Cold start only: the container may not be answering yet, so confirm.
    if ((docker exec $redisContainer redis-cli PING) -ne 'PONG') {
        Die 1 "redis PING failed on $redisContainer"
    }
}

function Redis { (& docker exec $redisContainer redis-cli @args | Out-String).Trim() }

function New-Meta([string]$Token) {
    @{
        lockVersion     = 3
        lockBackend     = 'redis'
        lockName        = $LockName
        token           = $Token
        pid             = $PID
        startedAt       = (Get-Date).ToString('o')
        lastRenewedAt   = (Get-Date).ToString('o')
        worktree        = $Worktree
    } | ConvertTo-Json -Compress
}

function Read-Token {
    $t = (Get-Content -LiteralPath $tokenFile -Raw -ErrorAction SilentlyContinue)
    if ($t) { $t.Trim() } else { $null }
}

Ensure-AgentRedis

switch ($Verb) {

    'acquire' {
        $token = [Guid]::NewGuid().ToString('N')
        # Lock + meta set atomically in one round-trip: SET NX, then write meta only
        # if we won the lock. Returns 1 on acquire, 0 if already held.
        $script = "if redis.call('SET', KEYS[1], ARGV[1], 'NX', 'EX', ARGV[3]) then redis.call('SET', KEYS[2], ARGV[2], 'EX', ARGV[3]); return 1; else return 0; end"
        if ((Redis EVAL $script 2 $lockKey $metaKey $token (New-Meta $token) $lockTtlSeconds) -ne '1') { Die 3 'HELD' }
        New-Item -ItemType Directory -Force $stateDir | Out-Null
        Set-Content -LiteralPath $tokenFile -Value $token -NoNewline
        # If the worktree already exists at acquire time, write the auto-derive marker.
        if ($Worktree) { git -C $Worktree config --local agent.lockName $LockName 2>$null | Out-Null }
        Write-Output $token
        exit 0
    }

    'renew' {
        # OPTIONAL — not periodic. Re-arm the TTL for a unit that may outlive it, and/or
        # persist the auto-derive marker via -Worktree. Skip entirely for normal units.
        $token = Read-Token
        if (-not $token) { Die 1 "no cached token for $LockName" }
        # Re-arm TTL on lock + meta only if we still own the token.
        $script = "if redis.call('GET', KEYS[1]) == ARGV[1] then redis.call('EXPIRE', KEYS[1], ARGV[2]); redis.call('SET', KEYS[2], ARGV[3], 'EX', ARGV[2]); return 1; else return 0; end"
        if ((Redis EVAL $script 2 $lockKey $metaKey $token $lockTtlSeconds (New-Meta $token)) -ne '1') {
            Die 4 'LOST'
        }
        if ($Worktree) { git -C $Worktree config --local agent.lockName $LockName 2>$null | Out-Null }
        exit 0
    }

    'release' {
        $token = Read-Token
        if (-not $token) { Die 1 "no cached token for $LockName" }
        # Delete lock + meta only if we still own the token; never clobber otherwise.
        $script = "if redis.call('GET', KEYS[1]) == ARGV[1] then redis.call('DEL', KEYS[2]); return redis.call('DEL', KEYS[1]); else return 0; end"
        $released = Redis EVAL $script 2 $lockKey $metaKey $token
        Remove-Item -LiteralPath $tokenFile -ErrorAction SilentlyContinue
        if ($released -ne '1') { Die 5 'STALE' }
        exit 0
    }

    'status' {
        $held = Redis GET $lockKey
        if (-not $held) { Write-Output 'FREE'; exit 0 }
        if ($held -eq (Read-Token)) { Write-Output 'HELD-BY-ME' } else { Write-Output 'HELD' }
        exit 0
    }
}
