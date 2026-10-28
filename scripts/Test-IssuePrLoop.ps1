# Exercises the loop driver's PR survey and deferral fingerprints without GitHub, Redis or an agent.
$ErrorActionPreference = 'Stop'

$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'Start-IssuePrLoop.ps1'), [ref]$null, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors -join "`n") }
foreach ($name in 'Get-Hash', 'Get-CheckState', 'Get-LatestOutsideComment', 'Get-PrFingerprint',
    'Reset-SurveyStats', 'New-Candidate', 'Get-Candidates') {
    $definition = $ast.Find({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
        }, $false)
    if (-not $definition) { throw "Missing function $name" }
    . ([scriptblock]::Create($definition.Extent.Text))
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    $actualJson = ConvertTo-Json -InputObject $Actual -Compress -Depth 10
    $expectedJson = ConvertTo-Json -InputObject $Expected -Compress -Depth 10
    if ($actualJson -ne $expectedJson) { throw "$Message. Expected $expectedJson, got $actualJson." }
}

# The survey's issue half has its own sources; this test covers the PR half only.
function Get-MainCandidate { }
function Get-OpenIssues { }
function Get-StrandedBranches { @{} }
function Get-IssuePriority { 0 }
function gh { $global:LASTEXITCODE = 0; ConvertTo-Json -InputObject $script:prs -Depth 20 }

$repoSlug = 'test/repo'
$prFields = ''
$excludedPrLabels = @('needs-human', 'on-hold')
$loopMarker = '<!-- issue-pr-loop -->'
$surveyStats = [ordered]@{}
$script:promoted = [Collections.Generic.HashSet[int]]::new()

$draft = [pscustomobject]@{
    number = 1; title = 'Draft awaiting review'; isDraft = $true
    headRefOid = 'head-sha'; mergeable = 'MERGEABLE'; mergeStateStatus = 'DRAFT'
    author = @{ login = 'author' }; labels = @(); reviews = @(); comments = @()
    statusCheckRollup = @(@{ name = 'claude-review'; status = 'COMPLETED'; conclusion = 'SUCCESS' })
}
$ready = $draft.PSObject.Copy()
$ready.number = 2
$ready.isDraft = $false
$ready.mergeStateStatus = 'CLEAN'
$script:prs = @($draft, $ready)

Reset-SurveyStats
$candidates = @(Get-Candidates)
Assert-Equal @($candidates.Number | Sort-Object) @(1, 2) 'Draft and ready PRs must both enter the queue'
Assert-Equal $surveyStats['pr-draft'] 1 'The draft count must describe open drafts'
Assert-Equal ($candidates | Where-Object Number -eq 1).Light $false 'A draft needs review processing, not a light merge unit'
Assert-Equal ($candidates | Where-Object Number -eq 2).Light $true 'A clean ready PR stays a light merge candidate'

$draft.statusCheckRollup = @(@{ name = 'claude-review'; status = 'IN_PROGRESS' })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2) 'A draft waiting only for checks must yield to other work'
$draft.mergeable = 'CONFLICTING'
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number | Sort-Object) @(1, 2) 'A conflicting draft stays actionable while checks run'
$draft.mergeable = 'MERGEABLE'
foreach ($label in $excludedPrLabels) {
    $draft.labels = @(@{ name = $label })
    $draft.statusCheckRollup = @()
    Reset-SurveyStats
    Assert-Equal @((Get-Candidates).Number) @(2) "A draft labelled $label must stay parked"
}
$draft.labels = @()

$before = Get-PrFingerprint $draft
$draft.isDraft = $false
Assert-Equal ((Get-PrFingerprint $draft) -ne $before) $true 'Marking a PR ready must invalidate a deferral'
$draft.isDraft = $true

$request = [pscustomobject]@{
    id = 'request'; createdAt = '2026-10-28T12:00:00Z'
    body = "<!-- issue-pr-loop -->`n<!-- codex-review:head-sha -->`n@codex review"
    reactionGroups = @()
}
$draft.comments = @($request)
$before = Get-PrFingerprint $draft
$request.reactionGroups = @(@{ content = 'THUMBS_UP'; users = @{ totalCount = 1 } })
Assert-Equal ((Get-PrFingerprint $draft) -ne $before) $true 'A Codex reaction on the current-head request must wake a waiting draft'

$before = Get-PrFingerprint $draft
$draft.comments += [pscustomobject]@{ id = 'reply'; body = '<!-- issue-pr-loop --> Disposition'; createdAt = '2026-10-28T13:00:00Z'; reactionGroups = @() }
Assert-Equal (Get-PrFingerprint $draft) $before 'An ordinary loop comment must not invalidate a deferral'

$draft.headRefOid = 'new-head'
$before = Get-PrFingerprint $draft
$request.reactionGroups = @(@{ content = 'EYES'; users = @{ totalCount = 1 } })
Assert-Equal (Get-PrFingerprint $draft) $before 'A reaction on an older head''s request must not wake the new head'

Write-Host 'Issue/PR loop survey and fingerprint tests passed.'
