$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'WorktreeCleanup.ps1')

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = [IO.Path]::GetFullPath((Join-Path $tempBase "dekaf-worktree-cleanup-$([Guid]::NewGuid().ToString('N'))"))
if (-not $testRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use test directory outside the system temporary directory: $testRoot"
}

$repo = Join-Path $testRoot 'repo'
$sourceWorktree = Join-Path $testRoot 'issue-123-fresh-source'
$artifactWorktree = Join-Path $testRoot 'issue-124-artifacts-only'

try {
    New-Item -ItemType Directory -Path $repo | Out-Null
    git -C $repo init -b main --quiet
    git -C $repo config user.name 'Worktree Cleanup Test'
    git -C $repo config user.email 'worktree-cleanup@example.invalid'
    Set-Content -LiteralPath (Join-Path $repo 'README.md') -Value '# fixture'
    git -C $repo add README.md
    git -C $repo commit --quiet -m 'fixture'

    git -C $repo worktree add --quiet -b issue-123-fresh-source $sourceWorktree
    $newSource = Join-Path $sourceWorktree 'src/NewFeature.cs'
    New-Item -ItemType Directory -Path (Split-Path $newSource -Parent) | Out-Null
    Set-Content -LiteralPath $newSource -Value 'internal sealed class NewFeature;'

    Remove-MergedWorktree -Repo $repo -Worktree $sourceWorktree -Label 'source fixture'
    Assert-True (Test-Path -LiteralPath $sourceWorktree) 'Fresh issue worktree with untracked source was removed.'
    Assert-True (Test-Path -LiteralPath $newSource) 'Untracked source file was removed.'

    git -C $repo worktree remove --force $sourceWorktree
    git -C $repo branch -D issue-123-fresh-source | Out-Null

    git -C $repo worktree add --quiet -b issue-124-artifacts-only $artifactWorktree
    $generatedFile = Join-Path $artifactWorktree 'src/Fixture/bin/generated.dll'
    New-Item -ItemType Directory -Path (Split-Path $generatedFile -Parent) | Out-Null
    Set-Content -LiteralPath $generatedFile -Value 'generated'

    Remove-MergedWorktree -Repo $repo -Worktree $artifactWorktree -Label 'artifact fixture'
    Assert-True (-not (Test-Path -LiteralPath $artifactWorktree)) 'Artifact-only worktree was not removed.'

    $mainAssociation = [pscustomobject]@{
        state = 'closed'
        merged_at = '2026-08-19T00:00:00Z'
        head = [pscustomobject]@{ ref = 'main' }
    }
    $issueAssociation = [pscustomobject]@{
        state = 'closed'
        merged_at = '2026-08-19T00:00:00Z'
        head = [pscustomobject]@{ ref = 'issue-123-fresh-source' }
    }

    Assert-True (-not (Test-WorktreeMatchesMergedPullRequest -Associations @($mainAssociation) -Branch 'issue-123-fresh-source' -Detached $false)) `
        'Named issue branch matched an unrelated merged PR through a shared commit.'
    Assert-True (Test-WorktreeMatchesMergedPullRequest -Associations @($issueAssociation) -Branch 'issue-123-fresh-source' -Detached $false) `
        'Named issue branch did not match its own merged PR.'
    Assert-True (Test-WorktreeMatchesMergedPullRequest -Associations @($mainAssociation) -Branch $null -Detached $true) `
        'Detached worktree did not match its merged commit association.'

    Write-Host 'OK worktree cleanup safety tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
