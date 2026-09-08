# Executes the real merge wrapper against local Git remotes and isolated Redis keys.
# Only GitHub's gate/merge responses are substituted; no real PR is merged.
param([string]$AgentLocksScript)
$ErrorActionPreference = 'Stop'
if (-not $AgentLocksScript) {
    $common = & git rev-parse --path-format=absolute --git-common-dir
    $AgentLocksScript = Join-Path (Split-Path $common -Parent) 'scripts/AgentLocks.ps1'
}
$AgentLocksScript = (Resolve-Path -LiteralPath $AgentLocksScript).Path
$suffix = [Guid]::NewGuid().ToString('N')
$owner = "merge-owner-tests-$suffix"
$previousOwner = $env:DEKAF_AGENT_LOCK_OWNER_ID
$previousThread = $env:CODEX_THREAD_ID
$env:DEKAF_AGENT_LOCK_OWNER_ID = $owner
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "merge-owner-$suffix"))
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (-not $testRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture root' }
$leases = [Collections.Generic.List[object]]::new()

function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Invoke-TestGit {
    & git @args 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Git fixture command failed: $args" }
}

# Merge-Pr.ps1 resolves this function instead of contacting GitHub. Git operations
# still execute against the fixture's real bare remote and isolated worktree.
function gh {
    if ($args[0] -eq 'pr' -and $args[1] -eq 'view') {
        $global:LASTEXITCODE = 0
        return $fixtureBranch
    }
    if ($args[0] -eq 'pr' -and $args[1] -eq 'merge') {
        Invoke-TestGit -C $fixtureRepo merge --squash $fixtureBranch
        Invoke-TestGit -C $fixtureRepo commit -m 'Fixture squash merge'
        $global:LASTEXITCODE = 0
        return
    }
    throw "Unexpected GitHub fixture call: $args"
}

try {
    foreach ($scenario in @('cleanup-failure', 'owner', 'foreign-owner', 'unowned', 'unidentified', 'dirty-owner', 'renew-failure', 'status-failure')) {
        $caseRoot = Join-Path $testRoot $scenario
        $repo = Join-Path $caseRoot 'repo'
        $remote = Join-Path $caseRoot 'remote.git'
        $number = Get-Random -Minimum 1000000000 -Maximum 2000000000
        $branch = "issue-$number-fixture"
        $worktree = Join-Path $caseRoot "pr-$number-merge"
        $lockName = "pr-$number"
        New-Item -ItemType Directory -Path (Join-Path $repo 'scripts') -Force | Out-Null
        Invoke-TestGit init --bare $remote
        Invoke-TestGit init --initial-branch=main $repo
        Invoke-TestGit -C $repo config user.name 'Merge Ownership Tests'
        Invoke-TestGit -C $repo config user.email 'merge-owner@example.invalid'
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Merge-Pr.ps1') -Destination (Join-Path $repo 'scripts/Merge-Pr.ps1')
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'WorktreeCleanup.ps1') -Destination (Join-Path $repo 'scripts/WorktreeCleanup.ps1')
        if ($scenario -eq 'cleanup-failure') {
            # Inject an inspection failure after ownership has been acquired. The
            # real wrapper must report merge success, preserve branches and release
            # its temporary lease even when the guarded filesystem operation throws.
            Add-Content -LiteralPath (Join-Path $repo 'scripts/WorktreeCleanup.ps1') -Value @'
function Remove-MergedWorktreeCore {
    param($Repo, $Worktree, $Label, $ExpectedHead, $ExpectedLockName, [switch]$WhatIf)
    throw 'Injected cleanup inspection failure'
}
'@
        }
        Set-Content -LiteralPath (Join-Path $repo 'scripts/Assert-PrGreen.ps1') -Value 'exit 0'
        $quotedCanonical = $AgentLocksScript.Replace("'", "''")
        $proxy = "& '$quotedCanonical' @args`nexit `$LASTEXITCODE"
        Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value $proxy
        Set-Content -LiteralPath (Join-Path $repo 'README.md') -Value 'fixture'
        Invoke-TestGit -C $repo add .
        Invoke-TestGit -C $repo commit -m fixture
        Invoke-TestGit -C $repo remote add origin $remote
        Invoke-TestGit -C $repo push -u origin main
        Invoke-TestGit -C $repo worktree add -b $branch $worktree HEAD
        Set-Content -LiteralPath (Join-Path $worktree 'change.txt') -Value $scenario
        Invoke-TestGit -C $worktree add change.txt
        Invoke-TestGit -C $worktree commit -m change
        Invoke-TestGit -C $worktree push -u origin $branch
        $leaseOwner = if ($scenario -eq 'foreign-owner') { "foreign-$owner" } else { $owner }
        $claimed = $scenario -notin @('unowned', 'unidentified', 'cleanup-failure')
        if ($claimed) {
            & pwsh -NoProfile -File $AgentLocksScript acquire -LockName $lockName -OwnerId $leaseOwner -Worktree $worktree *> $null
            Assert ($LASTEXITCODE -eq 0) 'Cannot acquire isolated merge lease'
            $leases.Add([pscustomobject]@{ Key = $lockName; Owner = $leaseOwner })
        }
        if ($scenario -eq 'dirty-owner') {
            Set-Content -LiteralPath (Join-Path $worktree 'change.txt') -Value 'uncommitted source'
        }
        if ($scenario -eq 'renew-failure') {
            $proxy = "if (`$args[0] -eq 'renew') { exit 4 }`n$proxy"
            Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value $proxy
        }
        if ($scenario -eq 'status-failure') {
            $proxy = "if (`$args[0] -eq 'status') { exit 1 }`n$proxy"
            Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value $proxy
        }
        $script:fixtureRepo = $repo
        $script:fixtureBranch = $branch
        Push-Location $repo
        try {
            if ($scenario -eq 'unidentified') {
                $env:DEKAF_AGENT_LOCK_OWNER_ID = $null
                $env:CODEX_THREAD_ID = $null
            }
            & (Join-Path $repo 'scripts/Merge-Pr.ps1') -Pr $number -Worktree $worktree
            Assert ($LASTEXITCODE -eq 0) 'Fixture merge wrapper failed'
        }
        finally {
            $env:DEKAF_AGENT_LOCK_OWNER_ID = $owner
            $env:CODEX_THREAD_ID = $previousThread
            Pop-Location
        }
        Assert ((Get-Content -LiteralPath (Join-Path $repo 'change.txt')) -eq $scenario) 'Fixture merge did not complete'
        $shouldRemove = $scenario -in @('owner', 'unowned', 'unidentified')
        Assert ((Test-Path -LiteralPath $worktree) -ne $shouldRemove) "Wrong worktree retention for $scenario"
        & git -C $repo show-ref --verify --quiet "refs/heads/$branch"
        Assert (($LASTEXITCODE -eq 0) -ne $shouldRemove) "Wrong local branch retention for $scenario"
        & git -C $repo ls-remote --exit-code origin "refs/heads/$branch" 2>$null | Out-Null
        Assert (($LASTEXITCODE -eq 0) -ne $shouldRemove) "Wrong remote branch retention for $scenario"
        if ($claimed) {
            $state = & pwsh -NoProfile -File $AgentLocksScript status -LockName $lockName -OwnerId $leaseOwner
            Assert ($state -eq 'HELD-BY-ME') "Merge changed the existing lease for $scenario"
            # A competing claim must still fail after cleanup and branch deletion.
            & pwsh -NoProfile -File $AgentLocksScript acquire -LockName $lockName -OwnerId "competitor-$owner" *> $null
            $competingExit = $LASTEXITCODE
            if ($competingExit -eq 0) {
                & pwsh -NoProfile -File $AgentLocksScript release -LockName $lockName -OwnerId "competitor-$owner" *> $null
            }
            Assert ($competingExit -eq 3) "Merge released ownership too soon for $scenario"
        }
        else {
            $state = & pwsh -NoProfile -File $AgentLocksScript status -LockName $lockName -OwnerId $owner
            Assert ($state -eq 'FREE') "Merge left its temporary lease behind for $scenario"
        }
        Write-Host "OK merge ownership scenario: $scenario"
    }
}
finally {
    foreach ($lease in $leases) {
        & pwsh -NoProfile -File $AgentLocksScript release -LockName $lease.Key -OwnerId $lease.Owner *> $null
    }
    $env:DEKAF_AGENT_LOCK_OWNER_ID = $previousOwner
    $env:CODEX_THREAD_ID = $previousThread
    if (Test-Path -LiteralPath $testRoot) {
        $resolved = (Resolve-Path -LiteralPath $testRoot).Path
        if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup path' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
