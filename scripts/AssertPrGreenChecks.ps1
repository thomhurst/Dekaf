# Check evaluation for Assert-PrGreen.ps1, kept free of `gh` calls so it can be unit tested
# (scripts/Test-AssertPrGreenChecks.ps1).

# Workflows whose every job feeds a required check (`CI Passed` aggregates all of ci.yml;
# `Micro A1/B/A2 screen` aggregates the performance gate; `Dependency Review` is its own
# workflow). GitHub's auto-merge waits for required checks, so only these may still be pending
# when auto-merge is armed. Test-AssertPrGreenChecks.ps1 pins both aggregators' coverage.
$AutoMergeAwaitedWorkflows = @('CI', 'Performance gate', 'Dependency Review')

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

# The performance gate's required check also passes on a head pushed after the PR left draft,
# which it does not measure. Only a run that selected benchmark classes on this head measured it;
# arming may proceed while that selection is still queued, because auto-merge waits for the verdict.
function Get-PerformanceGateDenial {
    [CmdletBinding()]
    param([AllowNull()][object[]]$Checks, [switch]$ForAutoMerge)

    $plan = @($Checks | Where-Object { [string]$_.workflowName -eq 'Performance gate' -and (Get-CheckLabel $_) -eq 'Select benchmark classes' })
    $state = if ($plan.Count) { Get-CheckState $plan[-1] } else { '' }
    if ($state -eq 'SUCCESS' -or ($ForAutoMerge -and $PendingStates -contains $state)) { return }
    "'Performance gate' has not measured this head (a head pushed after the PR left draft is measured when it leaves draft again or carries the performance label)"
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
