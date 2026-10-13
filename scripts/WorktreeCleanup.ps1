# WorktreeCleanup.ps1
# Shared worktree-removal helper, dot-sourced by Merge-Pr.ps1 and
# Remove-MergedWorktrees.ps1. Not meant to be run directly.
#
# Removal policy (one place, both callers):
#   - PRESERVE a worktree with uncommitted tracked changes or untracked files outside
#     known generated directories. Never force-discard possible work.
#   - CLEAR untracked build artifacts (node_modules/bin/obj/etc.) — they are not work.
#   - CLEAR root-level workflow output covered by the repository's .gitignore.
#   - Long-path safe: git's own delete now works because core.longpaths=true is set
#     system-wide; the `\\?\` extended-length Remove-Item is kept as a fallback for
#     environments where that config is missing.

function New-OrdinalStringMap {
    [CmdletBinding()]
    [OutputType([System.Collections.Generic.Dictionary[string, bool]])]
    param()

    return [System.Collections.Generic.Dictionary[string, bool]]::new([System.StringComparer]::Ordinal)
}

$script:DisposableWorktreeGeneratedDirectories = New-OrdinalStringMap
foreach ($directory in @(
    '.artifacts',
    '.vs',
    '__pycache__',
    'ARM',
    'ARM64',
    'artifacts',
    'benchmark-results',
    'BenchmarkDotNet.Artifacts',
    'bin',
    'bld',
    'CodeCoverage',
    'Debug',
    'DebugPublic',
    'log',
    'logs',
    'node_modules',
    'obj',
    'Release',
    'Releases',
    'results',
    'StrykerOutput',
    'temptest',
    'TestResults',
    'Win32',
    'x64',
    'x86'
)) {
    $script:DisposableWorktreeGeneratedDirectories[$directory] = $true
}

$script:DisposableWorktreeScopedDirectories = @(
    'docs/.cache',
    'docs/.docusaurus',
    'docs/build',
    # Root-level SDK installs and agent scratch space.
    '.dotnet',
    '.tmp'
)

function Test-DisposableWorktreePath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    # Quoted porcelain paths require Git's escape decoding. Preserve them rather than
    # risk classifying an unusual source path as generated output.
    if ($Path.StartsWith('"', [System.StringComparison]::Ordinal)) { return $false }

    $normalizedPath = $Path -replace '\\', '/'
    # Keep these root-only patterns aligned with .gitignore. Ignore rules alone
    # cannot authorize deleting arbitrary files: ignored source and secrets survive.
    if (-not $normalizedPath.Contains('/') -and
        ($normalizedPath -cmatch '\.(log|nettrace)$' -or
         $normalizedPath -cmatch '(^|-)(pr-body|review-disposition|review-validation|rebase-validation|comment|issue)\.md$')) {
        return $true
    }
    foreach ($docsGeneratedDirectory in $script:DisposableWorktreeScopedDirectories) {
        if ($normalizedPath -ceq $docsGeneratedDirectory -or
            $normalizedPath.StartsWith("$docsGeneratedDirectory/", [System.StringComparison]::Ordinal)) {
            return $true
        }
    }

    foreach ($segment in ($normalizedPath -split '/')) {
        if ($script:DisposableWorktreeGeneratedDirectories.ContainsKey($segment)) { return $true }
    }

    return $false
}

function Test-WorktreeMatchesMergedPullRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Associations,
        [AllowNull()][string]$Branch,
        [bool]$Detached
    )

    foreach ($association in $Associations) {
        if (-not $association.merged_at) { continue }
        if ($Detached) { return $true }
        if ($Branch -and $association.head -and $association.head.ref -ceq $Branch) { return $true }
    }

    return $false
}

function Test-WorktreeHeadMerged {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repo,
        [Parameter(Mandatory)][string]$Head,
        [Parameter(Mandatory)][string]$MergedHead
    )

    if ($Head -ceq $MergedHead) { return $true }
    git -C $Repo merge-base --is-ancestor $Head $MergedHead 2>$null
    return $LASTEXITCODE -eq 0
}

function Get-WorktreeCleanupLockName {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Worktree)

    if (Test-Path -LiteralPath $Worktree) {
        $enabled = git -C $Worktree config --local --bool --get extensions.worktreeConfig 2>$null
        if ($LASTEXITCODE -notin @(0, 1)) { throw 'Cannot inspect worktree lock configuration.' }
        # Without this extension --worktree aliases shared config, which can contain
        # a legacy marker belonging to an entirely different checkout.
        if ($enabled -eq 'true') {
            $marker = git -C $Worktree config --worktree --get agent.lockName 2>$null
            if ($LASTEXITCODE -notin @(0, 1)) { throw 'Cannot inspect worktree lock marker.' }
            if ($marker) { return $marker.Trim() }
        }
    }

    # Ownership is acquired before checkout. Protect the creation/registration gap
    # using the work-item identity required by the issue/PR worktree convention.
    $name = [IO.Path]::GetFileName($Worktree.TrimEnd('\', '/'))
    if ($name -match '^(pr|issue)-(\d+)(?:-|$)') { return "$($Matches[1])-$($Matches[2])" }
    return $null
}

function Invoke-WorktreeOwnershipCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Script,
        [Parameter(Mandatory)][string]$Verb,
        [Parameter(Mandatory)][string]$LockName,
        [string]$OwnerId
    )

    $state = $null
    $ownerArguments = if ($OwnerId) { @('-OwnerId', $OwnerId) } else { @() }
    if ($Verb -eq 'status') {
        $state = (& pwsh -NoProfile -File $Script $Verb -LockName $LockName @ownerArguments 2>$null | Out-String).Trim()
    } else {
        # Acquire prints an opaque token. Only the canonical script may store it;
        # never include it (or arbitrary endpoint output) in cleanup diagnostics.
        & pwsh -NoProfile -File $Script $Verb -LockName $LockName @ownerArguments *> $null
    }
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; State = $state }
}

function Remove-MergedWorktree {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repo,
        [Parameter(Mandatory)][string]$Worktree,
        [string]$Label = '',
        [string]$ExpectedHead,
        [switch]$AllowCurrentOwner,
        [switch]$WhatIf
    )

    $coreParameters = @{} + $PSBoundParameters
    $coreParameters.Remove('AllowCurrentOwner')
    try { $lockName = Get-WorktreeCleanupLockName -Worktree $Worktree }
    catch {
        Write-Host "Preserving worktree $Label : $Worktree (ownership identity is unreadable)"
        return
    }
    if (-not $lockName) {
        Remove-MergedWorktreeCore @coreParameters
        return
    }

    # Repo is the shared checkout, not a branch copy whose locking protocol may be old.
    $agentLocks = Join-Path $Repo 'scripts/AgentLocks.ps1'
    if (-not (Test-Path -LiteralPath $agentLocks)) {
        Write-Host "Preserving worktree $Label : $Worktree (canonical ownership script is unavailable)"
        return
    }
    $hasCurrentOwner = -not [string]::IsNullOrWhiteSpace($env:DEKAF_AGENT_LOCK_OWNER_ID) -or
        -not [string]::IsNullOrWhiteSpace($env:CODEX_THREAD_ID)
    if ($AllowCurrentOwner -and $hasCurrentOwner) {
        try {
            # Only the post-merge caller opts into using its own canonical identity.
            # Sweeps always use a separate temporary owner and preserve live leases.
            $current = Invoke-WorktreeOwnershipCommand -Script $agentLocks -Verb status -LockName $lockName
            if ($current.ExitCode -eq 0 -and $current.State -eq 'HELD-BY-ME') {
                if (-not $WhatIf) {
                    # Validate the cached token and renew before removal; a stale or
                    # nearly expired lease cannot authorize the cleanup operation.
                    $renewed = Invoke-WorktreeOwnershipCommand -Script $agentLocks -Verb renew -LockName $lockName
                    if ($renewed.ExitCode -ne 0) { throw 'Current cleanup ownership could not be renewed.' }
                }
                Remove-MergedWorktreeCore @coreParameters -ExpectedLockName $lockName
                # The caller retains its lease through subsequent branch cleanup
                # and releases it in its normal finally path.
                return
            }
            if ($current.ExitCode -ne 0 -or $current.State -ne 'FREE') {
                Write-Host "Preserving worktree $Label : $Worktree (current caller does not own $lockName or ownership is unavailable)"
                return
            }
        }
        catch {
            Write-Host "Preserving worktree $Label : $Worktree (current ownership cleanup failed)"
            return
        }
    }
    $owner = "worktree-cleanup-$([Guid]::NewGuid().ToString('N'))"
    $verb = if ($WhatIf) { 'status' } else { 'acquire' }
    try { $result = Invoke-WorktreeOwnershipCommand -Script $agentLocks -Verb $verb -LockName $lockName -OwnerId $owner }
    catch {
        Write-Host "Preserving worktree $Label : $Worktree (ownership check failed)"
        return
    }
    if ($result.ExitCode -ne 0 -or ($WhatIf -and $result.State -ne 'FREE')) {
        Write-Host "Preserving worktree $Label : $Worktree (Redis ownership held or unavailable for $lockName)"
        return
    }

    try {
        Remove-MergedWorktreeCore @coreParameters -ExpectedLockName $lockName
    }
    catch {
        Write-Host "WARNING: worktree cleanup failed for $Label : $Worktree; any remaining checkout is preserved."
    }
    finally {
        if (-not $WhatIf) {
            # No -Worktree was supplied on acquire: release only this temporary lease.
            # A competing owner cannot acquire the item between our check and removal.
            $released = Invoke-WorktreeOwnershipCommand -Script $agentLocks -Verb release -LockName $lockName -OwnerId $owner
            if ($released.ExitCode -ne 0) {
                Write-Host "WARNING: could not release cleanup ownership for $lockName (exit $($released.ExitCode))"
            }
        }
    }
}

# Called only after Remove-MergedWorktree has established cleanup ownership, or
# determined this checkout has no registered/standard work-item identity.
function Remove-MergedWorktreeCore {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repo,       # a checkout that is NOT the one being removed (main)
        [Parameter(Mandatory)][string]$Worktree,   # path to remove
        [string]$Label = '',                       # e.g. "#1234" for log lines
        [string]$ExpectedHead,
        [string]$ExpectedLockName,
        [switch]$WhatIf
    )

    if (-not (Test-Path -LiteralPath $Worktree)) {
        if (-not $WhatIf) { git -C $Repo worktree prune }
        return
    }

    $repoPath = (Resolve-Path -LiteralPath $Repo).Path.TrimEnd('\', '/')
    $worktreePath = (Resolve-Path -LiteralPath $Worktree).Path.TrimEnd('\', '/')
    $relativePath = [IO.Path]::GetRelativePath($repoPath, $worktreePath)
    if ($relativePath -eq '.' -or (-not [IO.Path]::IsPathRooted($relativePath) -and
        $relativePath -ne '..' -and -not $relativePath.StartsWith("..$([IO.Path]::DirectorySeparatorChar)"))) {
        Write-Host "Preserving main or harness-managed worktree: $Worktree"
        return
    }
    $topLevel = git -C $Worktree rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($topLevel) -ne $worktreePath) {
        Write-Host "Preserving worktree $Label : $Worktree (could not verify repository path)"
        return
    }
    $gitDirectory = git -C $Worktree rev-parse --absolute-git-dir 2>$null
    if ($LASTEXITCODE -ne 0 -or (Test-Path -LiteralPath (Join-Path $gitDirectory 'locked'))) {
        Write-Host "Preserving locked or unreadable worktree: $Worktree"
        return
    }
    $commonDirectory = git -C $Worktree rev-parse --path-format=absolute --git-common-dir 2>$null
    if ($LASTEXITCODE -ne 0) { return }
    $repoCommonDirectory = git -C $Repo rev-parse --path-format=absolute --git-common-dir 2>$null
    if ($LASTEXITCODE -ne 0 -or $commonDirectory -ne $repoCommonDirectory -or $gitDirectory -eq $commonDirectory) {
        Write-Host "Preserving worktree from another repository or main checkout: $Worktree"
        return
    }
    $head = git -C $Worktree rev-parse HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or ($ExpectedHead -and $head -cne $ExpectedHead)) {
        Write-Host "Preserving worktree $Label : $Worktree (HEAD changed during sweep)"
        return
    }

    # Preserve tracked work and every untracked/ignored path that is not under a known
    # generated directory. --force below is safe only after this fail-closed check.
    $status = @(git -C $Worktree status --porcelain=v1 --untracked-files=all --ignored=matching 2>$null)
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Preserving worktree $Label : $Worktree (could not inspect worktree status)"
        return
    }

    $work = @($status | Where-Object {
        if ($_ -notmatch '^(\?\?|!!) ') { return $true }
        return -not (Test-DisposableWorktreePath -Path $_.Substring(3))
    })
    if ($work.Count -gt 0) {
        Write-Host "Preserving dirty worktree $Label : $Worktree (uncommitted work)"
        foreach ($entry in $work) { Write-Host "  $entry" }
        return
    }

    if ($WhatIf) {
        Write-Host "sweep: WOULD remove $Worktree -- $Label"
        return
    }

    if ($ExpectedLockName -and (Get-WorktreeCleanupLockName -Worktree $Worktree) -cne $ExpectedLockName) {
        Write-Host "Preserving worktree $Label : $Worktree (ownership identity changed during cleanup)"
        return
    }

    $pending = Register-PendingWorktreeRemoval -Repo $Repo -Worktree $Worktree -LockNames @($ExpectedLockName)
    if (Remove-WorktreeDirectory -Repo $Repo -Worktree $Worktree -Label $Label) {
        if ($pending) { Remove-Item -LiteralPath $pending -Force -ErrorAction SilentlyContinue }
        Write-Host "Removed worktree $Label : $Worktree"
    } elseif ($pending) {
        Write-Host "WARNING: could not fully remove $Worktree; Remove-MergedWorktrees.ps1 retries the pending removal"
    } else {
        Write-Host "WARNING: could not fully remove $Worktree"
    }
}

# Deletes an already-vetted worktree directory and prunes its registration. Returns
# whether the directory is gone. Callers must have checked that its content is disposable.
function Remove-WorktreeDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repo,
        [Parameter(Mandatory)][string]$Worktree,
        [string]$Label = ''
    )

    # Primary path: let git remove it (force clears untracked artifacts; tracked is clean).
    git -C $Repo -c core.longpaths=true worktree remove --force $Worktree 2>$null | Out-Null

    # Fallback for long-path failures and remnants that Git no longer recognizes.
    if (Test-Path -LiteralPath $Worktree) {
        # Avoid recursing through a package-manager junction if one exists in a docs
        # worktree. Leave it for manual cleanup instead of risking deletion outside
        # the worktree.
        $junction = Get-ChildItem -LiteralPath $Worktree -Directory -Recurse -Force -Filter node_modules -ErrorAction SilentlyContinue |
            Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 } |
            Select-Object -First 1
        if ($junction) {
            Write-Host "WARNING: worktree $Label requires manual removal -- detach the node_modules junction at $($junction.FullName) first, then re-run cleanup: $Worktree"
            return $false
        }
        # \\?\ disables Win32 path normalization, so forward slashes (git's output
        # format) are NOT translated — convert to backslashes or the delete no-ops.
        $removalPath = if ($IsWindows) { '\\?\' + ($Worktree -replace '/', '\') } else { $Worktree }
        Remove-Item -LiteralPath $removalPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    git -C $Repo worktree prune 2>$null | Out-Null
    return -not (Test-Path -LiteralPath $Worktree)
}

# PENDING REMOVALS
# On Windows a directory cannot be deleted while any process has its current directory
# inside it, and build or test processes can hold files open briefly. Git then deletes
# part of the tree, including the .git file, and fails. The remnant no longer looks like
# a worktree, so neither release nor the merged-worktree sweep would recognize it again.
# Before deleting a vetted checkout, cleanup records it under the common Git directory.
# The record is removed after success, and Complete-PendingWorktreeRemovals retries the
# rest once the blocking process has exited.

function Get-PendingWorktreeRemovalDirectory {
    param([Parameter(Mandatory)][string]$Repo)

    $common = git -C $Repo rev-parse --path-format=absolute --git-common-dir 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $common) { return $null }
    return Join-Path $common.Trim() 'agent-pending-removals'
}

function Register-PendingWorktreeRemoval {
    param(
        [Parameter(Mandatory)][string]$Repo,
        [Parameter(Mandatory)][string]$Worktree,
        [string[]]$LockNames = @()
    )

    $directory = Get-PendingWorktreeRemovalDirectory -Repo $Repo
    if (-not $directory) { return $null }
    $path = [IO.Path]::GetFullPath($Worktree).TrimEnd([char[]]@('/', '\'))
    $gitDirectory = git -C $path rev-parse --absolute-git-dir 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $gitDirectory) { return $null }
    try { $names = @(Get-WorktreeCleanupLockName -Worktree $path) + $LockNames }
    catch { $names = $LockNames }

    $key = if ($IsWindows) { $path.ToLowerInvariant() } else { $path }
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($key)))
    $record = Join-Path $directory ($hash.Substring(0, 16).ToLowerInvariant() + '.json')
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    [ordered]@{
        path         = $path
        gitDirectory = [IO.Path]::GetFullPath($gitDirectory.Trim())
        lockNames    = @($names | Where-Object { $_ } | Sort-Object -CaseSensitive -Unique)
        recordedAt   = [DateTimeOffset]::UtcNow.ToString('o')
    } | ConvertTo-Json -Compress | Set-Content -LiteralPath $record -NoNewline
    return $record
}

function Test-HasWorkNewerThan {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][DateTimeOffset]$Cutoff
    )

    foreach ($file in (Get-ChildItem -LiteralPath $Path -File -Recurse -Force -ErrorAction SilentlyContinue)) {
        $relative = [IO.Path]::GetRelativePath($Path, $file.FullName)
        if ($relative -eq '.git' -or (Test-DisposableWorktreePath -Path $relative)) { continue }
        if ($file.LastWriteTimeUtc -gt $Cutoff.UtcDateTime) { return $true }
    }
    return $false
}

function Complete-PendingWorktreeRemovals {
    param(
        [Parameter(Mandatory)][string]$Repo,
        [switch]$WhatIf
    )

    $directory = Get-PendingWorktreeRemovalDirectory -Repo $Repo
    if (-not $directory -or -not (Test-Path -LiteralPath $directory)) { return 0 }
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    $completed = 0
    foreach ($recordFile in (Get-ChildItem -LiteralPath $directory -File -Filter '*.json')) {
        try {
            $record = Get-Content -LiteralPath $recordFile.FullName -Raw | ConvertFrom-Json
            $path = [string]$record.path
            $recordedAt = [DateTimeOffset]::Parse([string]$record.recordedAt, [Globalization.CultureInfo]::InvariantCulture)
        }
        catch {
            Write-Host "sweep: WARNING unreadable pending removal record: $($recordFile.FullName)"
            continue
        }
        $drop = { if (-not $WhatIf) { Remove-Item -LiteralPath $recordFile.FullName -Force -ErrorAction SilentlyContinue } }

        if (-not $path -or -not (Test-Path -LiteralPath $path)) { & $drop; continue }

        # A new checkout at the same path has its own Git metadata, created after the record.
        $marker = Join-Path $path '.git'
        if (Test-Path -LiteralPath $marker -PathType Leaf) {
            $gitDirectory = ((Get-Content -LiteralPath $marker -TotalCount 1) -replace '^gitdir:\s*', '').Trim()
            if ($gitDirectory -and -not [IO.Path]::IsPathRooted($gitDirectory)) { $gitDirectory = Join-Path $path $gitDirectory }
            if ($gitDirectory -and (Test-Path -LiteralPath $gitDirectory) -and (
                    -not [string]::Equals([IO.Path]::GetFullPath($gitDirectory).TrimEnd('\', '/'), ([string]$record.gitDirectory).TrimEnd('\', '/'), $comparison) -or
                    (Get-Item -LiteralPath $gitDirectory -Force).CreationTimeUtc -gt $recordedAt.UtcDateTime)) {
                Write-Host "sweep: dropping pending removal superseded by a new checkout: $path"
                & $drop
                continue
            }
        }
        elseif (Test-Path -LiteralPath $marker) {
            Write-Host "sweep: dropping pending removal for a standalone repository: $path"
            & $drop
            continue
        }

        # The checkout was vetted before deletion began. Anything written since is new work.
        if (Test-HasWorkNewerThan -Path $path -Cutoff $recordedAt) {
            Write-Host "sweep: WARNING preserving pending removal with files changed after removal began; inspect manually: $path"
            & $drop
            continue
        }

        $names = @($record.lockNames | Where-Object { $_ })
        $agentLocks = Join-Path $Repo 'scripts/AgentLocks.ps1'
        if ($names.Count -gt 0 -and -not (Test-Path -LiteralPath $agentLocks)) {
            Write-Host "sweep: preserving pending removal (canonical ownership script is unavailable): $path"
            continue
        }

        # Hold temporary leases through deletion so a new owner cannot reuse the item midway.
        $owner = "worktree-cleanup-$([Guid]::NewGuid().ToString('N'))"
        $verb = if ($WhatIf) { 'status' } else { 'acquire' }
        $acquired = [Collections.Generic.List[string]]::new()
        try {
            $owned = $true
            foreach ($name in $names) {
                $result = Invoke-WorktreeOwnershipCommand -Script $agentLocks -Verb $verb -LockName $name -OwnerId $owner
                if ($result.ExitCode -ne 0 -or ($WhatIf -and $result.State -ne 'FREE')) {
                    Write-Host "sweep: preserving pending removal (Redis ownership held or unavailable for $name): $path"
                    $owned = $false
                    break
                }
                if (-not $WhatIf) { $acquired.Add($name) }
            }
            if (-not $owned) { continue }
            if ($WhatIf) {
                Write-Host "sweep: WOULD finish pending removal of $path"
                continue
            }
            if (Remove-WorktreeDirectory -Repo $Repo -Worktree $path -Label '(pending removal)') {
                & $drop
                $completed++
                Write-Host "sweep: finished pending removal of $path"
            } else {
                Write-Host "sweep: WARNING pending removal is still blocked, retrying next sweep: $path"
            }
        }
        catch {
            Write-Host "sweep: WARNING pending removal failed, retrying next sweep: $path"
        }
        finally {
            foreach ($name in $acquired) {
                $released = Invoke-WorktreeOwnershipCommand -Script $agentLocks -Verb release -LockName $name -OwnerId $owner
                if ($released.ExitCode -ne 0) {
                    Write-Host "WARNING: could not release cleanup ownership for $name (exit $($released.ExitCode))"
                }
            }
        }
    }
    return $completed
}
