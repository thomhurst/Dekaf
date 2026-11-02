#!/usr/bin/env pwsh
# Exercises the loop driver's PR survey and deferral fingerprints without GitHub, Redis or an agent.
$ErrorActionPreference = 'Stop'

$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'Start-IssuePrLoop.ps1'), [ref]$null, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors -join "`n") }
foreach ($name in 'Get-Hash', 'Get-CheckState', 'Get-LatestOutsideComment', 'Get-PrFingerprint',
    'Reset-SurveyStats', 'New-Candidate', 'Get-Candidates', 'Get-ReasonReferences',
    'ConvertTo-Instant', 'Test-HasNewFeedback', 'Get-IssueFingerprint', 'Test-HasLabel', 'Test-HasOpenPr') {
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

# The survey's issue sources are stubbed; the open-PR limit test supplies issues.
function Get-MainCandidate { }
function Get-OpenIssues { }
function Get-StrandedBranches { @{} }
function Get-IssuePriority { 0 }
$script:activity = @{}
function Get-PrActivity { $script:activity }
function gh { $global:LASTEXITCODE = 0; ConvertTo-Json -InputObject $script:prs -Depth 20 }

$repoSlug = 'test/repo'
$prFields = ''
$excludedPrLabels = @('needs-human', 'on-hold')
$loopMarker = '<!-- issue-pr-loop -->'
$MaxOpenPrs = 8
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

# Review feedback without a disposition is actionable while checks still run.
$draft.statusCheckRollup = @(@{ name = 'claude-review'; status = 'IN_PROGRESS' })
$script:activity = @{ 1 = [pscustomobject]@{ HeadCommittedAt = '2026-10-28T10:00:00Z'; UnresolvedThreads = 1 } }
Reset-SurveyStats
$feedbackCandidate = @(Get-Candidates) | Where-Object Number -eq 1
Assert-Equal $feedbackCandidate.Rank 2 'An unresolved review thread must make a pending PR actionable'
Assert-Equal ($feedbackCandidate.Hint -like '*review feedback*') $true 'A pending PR with feedback must say why it was chosen'
$script:activity[1].UnresolvedThreads = 0
$draft.reviews = @(@{ author = @{ login = 'reviewer' }; state = 'CHANGES_REQUESTED'; submittedAt = '2026-10-28T11:00:00Z' })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number | Sort-Object) @(1, 2) 'A changes-requested review after the last push is feedback'
$draft.reviews += @{ author = @{ login = 'author' }; state = 'COMMENTED'; submittedAt = '2026-10-28T11:30:00Z' }
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2) 'Feedback the loop already answered must not wake a pending PR'
$draft.reviews = @(@{ author = @{ login = 'reviewer' }; state = 'CHANGES_REQUESTED'; submittedAt = '2026-10-28T11:00:00Z' })
$draft.comments = @([pscustomobject]@{ id = 'answer'; body = '<!-- issue-pr-loop --> Disposition'; createdAt = '2026-10-28T11:30:00Z'; reactionGroups = @() })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2) 'A loop comment after the review must count as its disposition'
$draft.comments = @()
$draft.reviews = @(@{ author = @{ login = 'reviewer' }; state = 'CHANGES_REQUESTED'; submittedAt = '2026-10-28T09:00:00Z' })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2) 'Feedback older than the last push must not wake a pending PR'
$draft.reviews = @(@{ author = @{ login = 'reviewer' }; state = 'COMMENTED'; submittedAt = '2026-10-28T11:00:00Z' })
$draft.comments = @([pscustomobject]@{ id = 'review'; body = 'Review: no blocking issues found.'; createdAt = '2026-10-28T11:00:00Z'; reactionGroups = @() })
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2) 'Comments and comment-only reviews without open threads must not wake a pending PR'
$script:activity[2] = [pscustomobject]@{ HeadCommittedAt = '2026-10-28T10:00:00Z'; UnresolvedThreads = 2 }
Reset-SurveyStats
Assert-Equal (@(Get-Candidates) | Where-Object Number -eq 2).Light $false 'A green PR with open threads needs full effort, not the merge gate'
$script:activity = @{}
$draft.comments = @()
$draft.reviews = @()
$draft.statusCheckRollup = @()

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

Assert-Equal @(Get-ReasonReferences 'Blocked on #12 and #4019; CI run #6494214847 failed, see #12' 4019) @(12) 'Deferral references must skip the item itself and ids too long for issue numbers'

# At the open-PR limit, existing PRs and claims still run but no new issue starts.
function New-TestIssue([int]$Number, [string[]]$Labels) {
    [pscustomobject]@{
        number = $Number; title = "Issue $Number"; lastEditedAt = $null; author = @{ login = 'author'; __typename = 'User' }
        labels = @{ nodes = @($Labels | ForEach-Object { @{ name = $_ } }) }; assignees = @{ nodes = @() }
        comments = @{ totalCount = 0; nodes = @() }; subIssuesSummary = @{ total = 0; completed = 0 }; subIssues = @{ nodes = @() }
        issueDependenciesSummary = @{ blockedBy = 0; blocking = 0 }; closedByPullRequestsReferences = @{ nodes = @() }
    }
}
$parkedLabels = @('needs-human', 'on-hold')
$excludedIssueLabels = @('wontfix', 'duplicate', 'question', 'needs-human', 'on-hold')
$script:issues = @((New-TestIssue 10 @()), (New-TestIssue 11 @()), (New-TestIssue 12 @('in-progress')))
function Get-OpenIssues { $script:issues }
$MaxOpenPrs = 2
$parked = $draft.PSObject.Copy(); $parked.number = 3; $parked.labels = @(@{ name = 'on-hold' })
$script:prs = @($draft, $parked)
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(1, 10, 11, 12) 'Parked PRs must not count toward the open-PR limit'
$script:prs = @($draft, $ready)
Reset-SurveyStats
Assert-Equal @((Get-Candidates).Number) @(2, 1, 12) 'At the open-PR limit only existing work may be queued'
Assert-Equal $surveyStats['issue-wip'] 2 'Issues held by the open-PR limit must be counted'
$MaxOpenPrs = 0
Reset-SurveyStats
Assert-Equal @(Get-Candidates).Count 5 'A zero open-PR limit must not hold issues'

Write-Host 'Issue/PR loop survey and fingerprint tests passed.'
