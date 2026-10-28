# Check evaluation for Assert-PrGreen.ps1, kept free of `gh` calls so it can be unit tested
# (scripts/Test-AssertPrGreenChecks.ps1).

# Workflows whose every job feeds a required check (`CI Passed` aggregates all of ci.yml;
# `Dependency Review` is its own workflow). GitHub's auto-merge waits for required checks, so
# only these may still be pending when auto-merge is armed. Test-AssertPrGreenChecks.ps1 pins
# the aggregator's coverage of ci.yml.
$AutoMergeAwaitedWorkflows = @('CI', 'Dependency Review')

# Path-filtered pull_request workflows that skip drafts and are not required checks. When a PR
# changes their paths, their verdict on the current head must be in before merging or arming:
# auto-merge would not wait for them.
$DeferredVerdictWorkflows = @('performance-gate.yml', 'performance-harness-tests.yml', 'review-helper-tests.yml')

$PassingConclusions = @('SUCCESS', 'SKIPPED', 'NEUTRAL')
$PendingStates = @('EXPECTED', 'REQUESTED', 'WAITING', 'QUEUED', 'PENDING', 'IN_PROGRESS')

function Test-IsCheckRun($Check) {
    ([string]$Check.__typename -eq 'CheckRun') -or ($null -ne $Check.status)
}

function Get-CheckLabel($Check) {
    if ($Check.PSObject.Properties['name'] -and $Check.name) { return [string]$Check.name }
    return [string]$Check.context
}

# The rollup keeps every execution of a check on the head: a draft run's skipped jobs, a run
# cancelled by `converted_to_draft` or a newer event, and the run that replaced it. Keep the most
# recently started execution per workflow and check name, as GitHub does for required checks.
# A check that has not started yet is the newest.
function Select-CurrentCheck {
    [CmdletBinding()]
    param([AllowNull()][object[]]$Checks)

    $latest = [ordered]@{}
    $statuses = [Collections.Generic.List[object]]::new()
    foreach ($check in @($Checks | Where-Object { $null -ne $_ })) {
        if (-not (Test-IsCheckRun $check)) { $statuses.Add($check); continue }
        $key = '{0}|{1}' -f $check.workflowName, $check.name
        # ConvertFrom-Json already turns ISO timestamps into DateTime values.
        $value = if ($check.PSObject.Properties['startedAt']) { $check.startedAt } else { $null }
        $started = if ($value -is [DateTime]) { [DateTimeOffset]::new($value.ToUniversalTime()) }
        elseif ($value) { [DateTimeOffset]::Parse([string]$value, [Globalization.CultureInfo]::InvariantCulture).ToUniversalTime() }
        else { [DateTimeOffset]::MaxValue }
        if ($started.Year -le 1) { $started = [DateTimeOffset]::MaxValue }
        if (-not $latest.Contains($key) -or $started -ge $latest[$key].Started) {
            $latest[$key] = @{ Started = $started; Check = $check }
        }
    }
    return @(@($latest.Values | ForEach-Object { $_.Check }) + $statuses)
}

function Get-CheckState($Check) {
    if (Test-IsCheckRun $Check) {
        if ([string]$Check.status -ne 'COMPLETED') { return [string]$Check.status }
        return [string]$Check.conclusion
    }
    return [string]$Check.state
}

# Returns one denial per check that blocks a merge (or, with -ForAutoMerge, arming).
function Get-CheckDenial {
    [CmdletBinding()]
    param(
        [AllowNull()][object[]]$Checks,
        [switch]$ForAutoMerge
    )

    foreach ($check in @($Checks)) {
        $label = Get-CheckLabel $check
        $state = Get-CheckState $check
        if ([string]::IsNullOrWhiteSpace($label) -or [string]::IsNullOrWhiteSpace($state)) {
            "unrecognized check entry: $($check | ConvertTo-Json -Compress)"
            continue
        }
        $passing = if (Test-IsCheckRun $check) { $PassingConclusions -contains $state } else { $state -eq 'SUCCESS' }
        if ($passing) { continue }
        if ($ForAutoMerge -and $PendingStates -contains $state -and
            $AutoMergeAwaitedWorkflows -contains [string]$check.workflowName) {
            continue
        }
        "${label}: state=$state"
    }
}

# Reads `name:` and the `on.pull_request.paths` list from a workflow file. Only the flat
# block-sequence form used in this repository is supported; anything else fails closed.
function Get-WorkflowPathFilter {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $lines = Get-Content -LiteralPath $Path
    $name = ($lines | Where-Object { $_ -match '^name:\s*(.+?)\s*$' } | Select-Object -First 1) -replace '^name:\s*', ''
    $name = $name.Trim().Trim("'", '"')
    $patterns = [Collections.Generic.List[string]]::new()
    $section = $null
    foreach ($line in $lines) {
        if ($line -match '^\S') { $section = if ($line -match '^on:\s*$') { 'on' } else { $null }; continue }
        if ($section -eq 'on' -and $line -match '^  \S') { $section = if ($line -match '^  pull_request:\s*$') { 'pr' } else { 'on' }; continue }
        if ($section -eq 'pr' -and $line -match '^    \S') { $section = if ($line -match '^    paths:\s*$') { 'paths' } else { 'pr' }; continue }
        if ($section -eq 'paths') {
            if ($line -match '^\s+-\s+[''"]?([^''"#]+?)[''"]?\s*(#.*)?$') { $patterns.Add($Matches[1]) }
            elseif ($line.Trim() -and -not $line.Trim().StartsWith('#')) { $section = 'pr' }
        }
    }
    if (-not $name -or $patterns.Count -eq 0) { throw "could not read the name and pull_request paths of $Path" }
    [pscustomobject]@{ Name = $name; Patterns = $patterns.ToArray() }
}

# GitHub paths-filter glob subset: `**` crosses directories (`**/` also matches none), `*` does not.
function Test-PathMatchesPattern {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string[]]$Patterns)

    foreach ($pattern in $Patterns) {
        $regex = '^' + ([regex]::Escape($pattern) -replace '\\\*\\\*/', '(?:.*/)?' -replace '\\\*\\\*', '.*' -replace '\\\*', '[^/]*') + '$'
        if ($Path -match $regex) { return $true }
    }
    return $false
}

# A deferred-verdict workflow whose paths the PR changes must have run on the current head
# (at least one successful job) and must be finished: neither merging nor auto-merge waits for it.
function Get-DeferredVerdictDenial {
    [CmdletBinding()]
    param(
        [AllowNull()][object[]]$Checks,
        [AllowNull()][string[]]$ChangedFiles,
        [Parameter(Mandatory)][object[]]$Workflows
    )

    foreach ($workflow in $Workflows) {
        if (-not @($ChangedFiles | Where-Object { Test-PathMatchesPattern -Path $_ -Patterns $workflow.Patterns }).Count) { continue }
        $own = @($Checks | Where-Object { [string]$_.workflowName -eq $workflow.Name })
        if (@($own | Where-Object { (Get-CheckState $_) -eq 'SUCCESS' }).Count -eq 0 -or
            @($own | Where-Object { $PendingStates -contains (Get-CheckState $_) }).Count -gt 0) {
            "'$($workflow.Name)' has no finished run on this head; it is not a required check, so its verdict must be in first" +
                $(if ($workflow.Name -eq 'Performance gate') { ' (a head pushed after the PR left draft is measured when it leaves draft again or carries the performance label)' } else { '' })
        }
    }
}

# The Claude review workflow posts one issue comment per run that starts with
# `<!-- REVIEW_RUN: <run>/<attempt>; HEAD: <sha> -->`.
function Test-ClaudeReviewForHead {
    [CmdletBinding()]
    param([AllowNull()][object[]]$Comments, [Parameter(Mandatory)][string]$Head)

    $marker = "; HEAD: $Head -->"
    foreach ($comment in @($Comments | Where-Object { $null -ne $_ })) {
        $first = (([string]$comment.body) -split "`n", 2)[0].Trim()
        if ([string]$comment.user.login -eq 'github-actions[bot]' -and
            $first.StartsWith('<!-- REVIEW_RUN: ') -and $first.EndsWith($marker)) {
            return $true
        }
    }
    return $false
}
