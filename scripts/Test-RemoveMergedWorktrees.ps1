$ErrorActionPreference = 'Stop'
$sweep = Join-Path $PSScriptRoot 'Remove-MergedWorktrees.ps1'
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = [IO.Path]::GetFullPath((Join-Path $tempBase "dekaf-sweep-$([Guid]::NewGuid().ToString('N'))"))
if (-not $testRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Test directory is outside the temporary directory: $testRoot"
}
$repo = Join-Path $testRoot 'repo'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

# Exercise the real sweep and Git filesystem operations without GitHub or network.
function gh {
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'pr' -and $args[1] -eq 'list') {
        if ($args -contains 'merged') { return $mergedJson }
        if ($failOpenQuery) { $global:LASTEXITCODE = 1; return }
        return $openJson
    }
    if ($args[0] -eq 'api') {
        if ($args[1] -match '/issues/(\d+)$') { return $(if ($Matches[1] -in $closedItems) { 'closed' } else { 'open' }) }
        return '[]'
    }
    throw "Unexpected gh call: $args"
}

try {
    New-Item -ItemType Directory -Path $repo | Out-Null
    git -C $repo init -b main --quiet
    git -C $repo config user.name 'Sweep Test'
    git -C $repo config user.email 'sweep@example.invalid'
    Set-Content -LiteralPath (Join-Path $repo 'README.md') -Value 'base'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../.gitignore') -Destination (Join-Path $repo '.gitignore')
    git -C $repo add README.md .gitignore
    git -C $repo commit --quiet -m 'base'
    $baseHead = git -C $repo rev-parse HEAD
    git -C $repo update-ref refs/remotes/origin/main $baseHead

    $mergedWorktree = Join-Path $testRoot 'merged'
    $dirtyWorktree = Join-Path $testRoot 'dirty'
    $divergentWorktree = Join-Path $testRoot 'divergent'
    $openWorktree = Join-Path $testRoot 'open'
    foreach ($name in @('merged', 'dirty', 'divergent', 'open')) {
        git -C $repo worktree add --quiet -b $name (Join-Path $testRoot $name)
    }
    Set-Content -LiteralPath (Join-Path $mergedWorktree 'build.log') -Value 'evidence'
    Set-Content -LiteralPath (Join-Path $dirtyWorktree 'NewFeature.cs') -Value 'untracked source'
    Set-Content -LiteralPath (Join-Path $divergentWorktree 'README.md') -Value 'unpublished'
    git -C $divergentWorktree commit --quiet -am 'unpublished'
    Set-Content -LiteralPath (Join-Path $openWorktree 'README.md') -Value 'open PR'
    git -C $openWorktree commit --quiet -am 'open PR'
    $openHead = git -C $openWorktree rev-parse HEAD
    $orphan = Join-Path $testRoot 'pr-123-orphan'
    New-Item -ItemType Directory -Path $orphan | Out-Null
    Set-Content -LiteralPath (Join-Path $orphan '.git') -Value "gitdir: $repo/.git/worktrees/missing-registration"
    Set-Content -LiteralPath (Join-Path $orphan 'NewFeature.cs') -Value 'untracked source'
    Set-Content -LiteralPath (Join-Path $repo 'README.md') -Value 'new main commit outside merged PR records'
    git -C $repo commit --quiet -am 'advance main'
    $mainHead = git -C $repo rev-parse HEAD
    git -C $repo update-ref refs/remotes/origin/main $mainHead
    $owned = Join-Path $testRoot 'owned-detached'
    git -C $repo worktree add --quiet --detach $owned $mainHead
    git -C $repo config extensions.worktreeConfig true
    git -C $owned config --worktree agent.lockName fixture-owned
    git -C $owned config --worktree agent.lockWorktree $owned
    # `git worktree add` copies config.worktree; the copied marker names $owned.
    $inherited = Join-Path $testRoot 'inherited-detached'
    git -C $owned worktree add --quiet --detach $inherited $mainHead
    # Closed work items whose checkouts no merge tier recognizes.
    $closedItems = @('500', '501', '503')
    $review = Join-Path $testRoot 'pr-500-review'
    git -C $repo worktree add --quiet -b pr-500-review $review $mainHead
    Set-Content -LiteralPath (Join-Path $review 'README.md') -Value 'local review commit'
    git -C $review commit --quiet -am 'local review commit'
    $reviewHead = git -C $review rev-parse HEAD
    Set-Content -LiteralPath (Join-Path $review 'README.md') -Value 'uncommitted edit'
    Set-Content -LiteralPath (Join-Path $review 'Draft.cs') -Value 'untracked draft'
    $closedDetached = Join-Path $testRoot 'issue-501-detached'
    git -C $repo worktree add --quiet --detach $closedDetached $mainHead
    Set-Content -LiteralPath (Join-Path $closedDetached 'README.md') -Value 'detached commit'
    git -C $closedDetached commit --quiet -am 'detached commit'
    $detachedHead = git -C $closedDetached rev-parse HEAD
    $openItem = Join-Path $testRoot 'pr-502-open'
    git -C $repo worktree add --quiet -b pr-502-open $openItem $mainHead
    $ignoredItem = Join-Path $testRoot 'pr-503-ignored'
    git -C $repo worktree add --quiet -b pr-503-ignored $ignoredItem $mainHead
    # Ignored by .gitignore and not generated output: local settings.
    Set-Content -LiteralPath (Join-Path $ignoredItem '.claude.local.md') -Value 'local notes'
    $evidenceRoot = "$repo-evidence"
    foreach ($entry in @('issue-1/build.log', 'issue-2/build.log')) {
        $file = Join-Path $evidenceRoot $entry
        New-Item -ItemType Directory -Path (Split-Path $file -Parent) -Force | Out-Null
        Set-Content -LiteralPath $file -Value 'evidence'
    }
    (Get-Item -LiteralPath (Join-Path $evidenceRoot 'issue-1/build.log')).LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-10)
    (Get-Item -LiteralPath (Join-Path $evidenceRoot 'issue-1')).LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-10)
    New-Item -ItemType Directory -Path (Join-Path $repo 'scripts') | Out-Null
    # Live Redis semantics are tested separately; this verifies sweep routing.
    Set-Content -LiteralPath (Join-Path $repo 'scripts/AgentLocks.ps1') -Value @'
param($Verb, $LockName, $OwnerId)
if ($LockName -ne 'fixture-owned') { if ($Verb -eq 'status') { 'FREE' }; exit 0 }
if ($Verb -eq 'status') { 'HELD'; exit 0 }
exit 3
'@
    $mergedJson = @(
        foreach ($name in @('merged', 'dirty', 'divergent', 'open')) {
            @{ headRefName = $name; headRefOid = $baseHead }
        }
    ) | ConvertTo-Json
    $openJson = @(@{ headRefName = 'open'; headRefOid = $openHead }) | ConvertTo-Json -AsArray

    Push-Location $repo
    try {
        $failOpenQuery = $true
        & $sweep -Repo fixture/repo
        Assert-True (Test-Path -LiteralPath $mergedWorktree) 'Failed open-PR query allowed removal.'
        $failOpenQuery = $false

        $preview = (& $sweep -Repo fixture/repo -WhatIf 6>&1) -join "`n"
        Assert-True ($preview -match 'WOULD remove .*[\\/]merged') 'Preview did not identify merged worktree.'
        Assert-True ($preview -match 'WOULD remove .*[\/]dirty -- .*\(saving 1') 'Preview did not report saving merged leftover work.'
        Assert-True (Test-Path -LiteralPath $mergedWorktree) 'Preview removed worktree.'
        Assert-True ($preview -match 'WOULD remove stale evidence .*issue-1') 'Preview did not identify stale evidence.'
        Assert-True (Test-Path -LiteralPath (Join-Path $evidenceRoot 'issue-1')) 'Preview removed evidence.'
        Assert-True ($preview -match 'WOULD remove .*pr-500-review -- \(closed work item pr-500\) \(saving 2') 'Preview did not report retained work.'
        Assert-True (Test-Path -LiteralPath (Join-Path $review 'Draft.cs')) 'Preview saved or removed closed-item work.'

        & $sweep -Repo fixture/repo
        Assert-True (-not (Test-Path -LiteralPath $mergedWorktree)) 'Merged worktree was not removed autonomously.'
        Assert-True (-not (Test-Path -LiteralPath $dirtyWorktree)) 'Merged worktree with leftover changes was not removed.'
        $dirtyWip = @(git -C $repo for-each-ref --format='%(refname:short)' 'refs/heads/retained-worktrees/dirty-wip-*')
        Assert-True ($dirtyWip.Count -eq 1) 'Merged leftover changes were not saved to a retained branch.'
        Assert-True ((git -C $repo show "$($dirtyWip[0]):NewFeature.cs") -eq 'untracked source') 'Saved merged leftover file is wrong.'
        Assert-True (Test-Path -LiteralPath $divergentWorktree) 'Divergent local commit was removed.'
        Assert-True (Test-Path -LiteralPath $openWorktree) 'Open PR worktree was removed.'
        Assert-True (Test-Path -LiteralPath $owned) 'Redis-owned detached main checkout was removed.'
        Assert-True (-not (Test-Path -LiteralPath $inherited)) 'Checkout with an inherited lock marker was preserved.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $evidenceRoot 'issue-1'))) 'Stale evidence was not removed.'
        Assert-True (Test-Path -LiteralPath (Join-Path $evidenceRoot 'issue-2')) 'Recent evidence was removed.'
        Assert-True (Test-Path -LiteralPath (Join-Path $orphan 'NewFeature.cs')) 'Orphan with unverifiable ownership/source was removed.'
        git -C $repo show-ref --verify --quiet refs/heads/merged
        Assert-True ($LASTEXITCODE -ne 0) 'Merged branch was not cleaned up.'
        git -C $repo show-ref --verify --quiet refs/heads/divergent
        Assert-True ($LASTEXITCODE -eq 0) 'Divergent branch was deleted.'

        Assert-True (-not (Test-Path -LiteralPath $review)) 'Closed PR review checkout was not removed.'
        Assert-True ((git -C $repo rev-parse refs/heads/pr-500-review) -eq $reviewHead) 'Closed item branch was not kept.'
        $wip = @(git -C $repo for-each-ref --format='%(refname:short)' 'refs/heads/retained-worktrees/pr-500-wip-*')
        Assert-True ($wip.Count -eq 1) 'Uncommitted work was not saved to a retained branch.'
        Assert-True ((git -C $repo show "$($wip[0]):README.md") -eq 'uncommitted edit') 'Saved tracked change is wrong.'
        Assert-True ((git -C $repo show "$($wip[0]):Draft.cs") -eq 'untracked draft') 'Saved untracked file is wrong.'
        Assert-True ((git -C $repo rev-parse "$($wip[0])^") -eq $reviewHead) 'Saved work is not based on the checkout HEAD.'
        Assert-True (@(git -C $repo stash list).Count -eq 0) 'Saving work touched the shared stash list.'
        Assert-True (-not (Test-Path -LiteralPath $closedDetached)) 'Closed detached checkout was not removed.'
        Assert-True ((git -C $repo rev-parse "refs/heads/retained-worktrees/$detachedHead") -eq $detachedHead) 'Detached commit was not retained.'
        Assert-True (Test-Path -LiteralPath $openItem) 'Open item checkout was removed.'
        Assert-True (Test-Path -LiteralPath (Join-Path $ignoredItem '.claude.local.md')) 'Unknown ignored file of a closed item was removed.'
    }
    finally { Pop-Location }
    Write-Host 'OK merged-worktree sweep tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
