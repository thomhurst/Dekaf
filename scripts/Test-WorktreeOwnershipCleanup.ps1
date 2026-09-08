# Uses isolated Git repositories and unique Redis keys; requires PowerShell 7 and Docker.
param([string]$AgentLocksScript)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WorktreeCleanup.ps1')
if (-not $AgentLocksScript) {
    $common = & git rev-parse --path-format=absolute --git-common-dir
    $AgentLocksScript = Join-Path (Split-Path $common -Parent) 'scripts/AgentLocks.ps1'
}
$AgentLocksScript = (Resolve-Path -LiteralPath $AgentLocksScript).Path
$suffix = [Guid]::NewGuid().ToString('N')
$owner = "ownership-tests-$suffix"
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "worktree-ownership-$suffix"))
$temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (-not $testRoot.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture root' }
$repo = Join-Path $testRoot 'repo'
$keys = [Collections.Generic.List[string]]::new()

function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Invoke-TestGit {
    & git.exe @args 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Git fixture command failed: $args" }
}
function Claim([string]$Key, [string]$Path) {
    $keys.Add($Key)
    $arguments = @('acquire', '-LockName', $Key, '-OwnerId', $owner)
    if ($Path) { $arguments += @('-Worktree', $Path) }
    & pwsh -NoProfile -File $AgentLocksScript @arguments *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot acquire isolated test lease' }
}
function Release([string]$Key) {
    & pwsh -NoProfile -File $AgentLocksScript release -LockName $Key -OwnerId $owner *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot release isolated test lease' }
}

try {
    New-Item -ItemType Directory -Path (Join-Path $repo 'scripts') -Force | Out-Null
    Invoke-TestGit init --initial-branch=main $repo
    Invoke-TestGit -C $repo config user.name 'Ownership Tests'
    Invoke-TestGit -C $repo config user.email 'ownership-tests@example.invalid'
    Set-Content -LiteralPath (Join-Path $repo '.gitignore') -Value "bin/`n.artifacts/"
    Set-Content -LiteralPath (Join-Path $repo 'README.md') -Value 'fixture'
    # The fixture's canonical endpoint delegates to this repository's canonical script.
    $quotedScript = $AgentLocksScript.Replace("'", "''")
    $proxy = "& '$quotedScript' @args`nexit `$LASTEXITCODE"
    Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value $proxy
    Invoke-TestGit -C $repo add .
    Invoke-TestGit -C $repo commit -m fixture
    Push-Location $repo
    try {
        $held = Join-Path $testRoot 'held-detached'
        Invoke-TestGit -C $repo worktree add --detach $held HEAD
        $key = "cleanup-test-$suffix-held"
        Claim $key $held
        Remove-MergedWorktree -Repo $repo -Worktree $held -Label 'owned detached fixture'
        Assert (Test-Path -LiteralPath (Join-Path $held '.git')) 'Cleanup removed a detached checkout with a live Redis lease.'
        $state = & pwsh -NoProfile -File $AgentLocksScript status -LockName $key -OwnerId $owner
        Assert ($state -eq 'HELD-BY-ME') 'Cleanup changed the active owner lease.'
        Remove-MergedWorktree -Repo $repo -Worktree $held -WhatIf
        Assert (Test-Path -LiteralPath $held) 'Preview removed an owned checkout.'
        Release $key
        Assert (-not (Test-Path -LiteralPath $held)) 'Canonical owner release did not remove its clean checkout.'

        # The standard directory identity protects creation before marker registration.
        $number = Get-Random -Minimum 1000000000 -Maximum 2000000000
        $pending = Join-Path $testRoot "pr-$number-before-marker"
        Invoke-TestGit -C $repo worktree add --detach $pending HEAD
        $key = "pr-$number"
        Claim $key ''
        Remove-MergedWorktree -Repo $repo -Worktree $pending -Label 'before marker fixture'
        Assert (Test-Path -LiteralPath (Join-Path $pending '.git')) 'Cleanup removed a checkout before its marker was registered.'
        Release $key

        # Keep a released checkout with a Git lock, then make it eligible for cleanup.
        $released = Join-Path $testRoot 'released-detached'
        Invoke-TestGit -C $repo worktree add --detach $released HEAD
        $key = "cleanup-test-$suffix-released"
        Claim $key $released
        Invoke-TestGit -C $repo worktree lock $released
        Release $key
        Invoke-TestGit -C $repo worktree unlock $released
        Remove-MergedWorktree -Repo $repo -Worktree $released -WhatIf
        Assert (Test-Path -LiteralPath $released) 'WhatIf removed a checkout.'
        # Attempt a competing acquisition after cleanup acquires and immediately
        # before it releases. Both must fail, with removal between those boundaries.
        $raceLog = Join-Path $testRoot 'race.log'
        $raceProxy = @'
param($Verb, $LockName, $OwnerId)
$canonical = '__CANONICAL__'
$competitor = '__COMPETITOR__'
function Try-CompetingClaim {
    & pwsh -NoProfile -File $canonical acquire -LockName $LockName -OwnerId $competitor *> $null
    $code = $LASTEXITCODE
    if ($code -eq 0) {
        & pwsh -NoProfile -File $canonical release -LockName $LockName -OwnerId $competitor *> $null
    }
    Add-Content -LiteralPath '__LOG__' -Value "$Verb competing=$code exists=$(Test-Path -LiteralPath '__WORKTREE__')"
}
if ($Verb -eq 'release') { Try-CompetingClaim }
& pwsh -NoProfile -File $canonical $Verb -LockName $LockName -OwnerId $OwnerId *> $null
$code = $LASTEXITCODE
if ($Verb -eq 'acquire' -and $code -eq 0) { Try-CompetingClaim }
exit $code
'@
        $raceProxy = $raceProxy.Replace('__CANONICAL__', $quotedScript).
            Replace('__COMPETITOR__', "competitor-$suffix").
            Replace('__LOG__', $raceLog.Replace("'", "''")).
            Replace('__WORKTREE__', $released.Replace("'", "''"))
        Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value $raceProxy
        Remove-MergedWorktree -Repo $repo -Worktree $released
        Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value $proxy
        Assert (-not (Test-Path -LiteralPath $released)) 'Released checkout was not removed.'
        $races = @(Get-Content -LiteralPath $raceLog)
        Assert ($races.Count -eq 2 -and $races[0] -ceq 'acquire competing=3 exists=True' -and
            $races[1] -ceq 'release competing=3 exists=False') 'Cleanup did not exclude competing ownership through removal.'
        $state = & pwsh -NoProfile -File $AgentLocksScript status -LockName $key -OwnerId $owner
        Assert ($state -eq 'FREE') 'Cleanup left its temporary lease behind.'

        $unavailable = Join-Path $testRoot 'unavailable'
        Invoke-TestGit -C $repo worktree add --detach $unavailable HEAD
        Invoke-TestGit -C $unavailable config --worktree agent.lockName "cleanup-test-$suffix-unavailable"
        Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value 'exit 2'
        Remove-MergedWorktree -Repo $repo -Worktree $unavailable
        Assert (Test-Path -LiteralPath $unavailable) 'Unavailable ownership check allowed removal.'
        Remove-MergedWorktree -Repo $repo -Worktree $unavailable -WhatIf
        Assert (Test-Path -LiteralPath $unavailable) 'Unavailable preview allowed removal.'
        Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value $proxy

        # Simulate a marker changing after acquisition; cleanup must release its
        # original lease without removing the checkout under a different identity.
        $changed = Join-Path $testRoot 'changed-marker'
        Invoke-TestGit -C $repo worktree add --detach $changed HEAD
        $key = "cleanup-test-$suffix-original"
        Invoke-TestGit -C $changed config --worktree agent.lockName $key
        $changedProxy = @'
param($Verb, $LockName, $OwnerId)
& pwsh -NoProfile -File '__CANONICAL__' $Verb -LockName $LockName -OwnerId $OwnerId *> $null
$code = $LASTEXITCODE
if ($Verb -eq 'acquire' -and $code -eq 0) {
    git -C '__WORKTREE__' config --worktree agent.lockName changed-fixture-identity
    if ($LASTEXITCODE -ne 0) { throw 'Cannot change fixture marker' }
}
exit $code
'@
        $changedProxy = $changedProxy.Replace('__CANONICAL__', $quotedScript).
            Replace('__WORKTREE__', $changed.Replace("'", "''"))
        Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value $changedProxy
        Remove-MergedWorktree -Repo $repo -Worktree $changed
        Assert (Test-Path -LiteralPath $changed) 'Changed ownership identity allowed removal.'
        $state = & pwsh -NoProfile -File $AgentLocksScript status -LockName $key -OwnerId $owner
        Assert ($state -eq 'FREE') 'Preserved checkout left cleanup ownership behind.'
        Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value $proxy
        Write-Host 'OK worktree ownership: live owner, creation window, canonical release, preview, unavailable endpoint, changed identity and competing acquisition throughout removal.'
    }
    finally {
        foreach ($key in $keys) {
            $state = & pwsh -NoProfile -File $AgentLocksScript status -LockName $key -OwnerId $owner
            if ($state -eq 'HELD-BY-ME') { Release $key }
        }
        Pop-Location
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
