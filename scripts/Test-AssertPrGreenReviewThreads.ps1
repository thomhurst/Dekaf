$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AssertPrGreenReviewThreads.ps1')

function New-ReviewThreadPage {
    param(
        [Parameter(Mandatory)][object[]]$Threads,
        [Parameter(Mandatory)][bool]$HasNextPage,
        [AllowNull()][string]$EndCursor
    )

    return [pscustomobject]@{
        nodes = $Threads
        pageInfo = [pscustomobject]@{
            hasNextPage = $HasNextPage
            endCursor = $EndCursor
        }
    }
}

$requestedCursors = [System.Collections.Generic.List[object]]::new()
$resolvedFirstPage = 1..100 | ForEach-Object { [pscustomobject]@{ isResolved = $true } }
$allResolved = @(Get-AllReviewThreads -FetchPage {
    param($cursor)
    $requestedCursors.Add($cursor)
    if ($null -eq $cursor) {
        return New-ReviewThreadPage -Threads $resolvedFirstPage -HasNextPage $true -EndCursor 'page-2'
    }

    return New-ReviewThreadPage -Threads @([pscustomobject]@{ isResolved = $true }) -HasNextPage $false
})

if ($allResolved.Count -ne 101) {
    throw "Expected 101 resolved threads, got $($allResolved.Count)."
}
if ($requestedCursors.Count -ne 2 -or $null -ne $requestedCursors[0] -or $requestedCursors[1] -ne 'page-2') {
    throw "Unexpected cursor sequence: $($requestedCursors -join ', ')."
}

$laterUnresolved = @(Get-AllReviewThreads -FetchPage {
    param($cursor)
    if ($null -eq $cursor) {
        return New-ReviewThreadPage -Threads $resolvedFirstPage -HasNextPage $true -EndCursor 'page-2'
    }

    return New-ReviewThreadPage -Threads @([pscustomobject]@{ isResolved = $false }) -HasNextPage $false
})
if (@($laterUnresolved | Where-Object { -not $_.isResolved }).Count -ne 1) {
    throw 'Expected an unresolved thread on the second page.'
}

$missingCursorThrew = $false
try {
    Get-AllReviewThreads -FetchPage {
        New-ReviewThreadPage -Threads @() -HasNextPage $true
    }
}
catch {
    $missingCursorThrew = $true
}
if (-not $missingCursorThrew) {
    throw 'Expected pagination without an end cursor to fail closed.'
}

foreach ($invalidPageInfo in @(
    [pscustomobject]@{},
    [pscustomobject]@{ hasNextPage = $null },
    [pscustomobject]@{ hasNextPage = 'false' }
)) {
    $invalidHasNextPageThrew = $false
    try {
        Get-AllReviewThreads -FetchPage {
            [pscustomobject]@{ nodes = @(); pageInfo = $invalidPageInfo }
        }
    }
    catch {
        $invalidHasNextPageThrew = $true
    }
    if (-not $invalidHasNextPageThrew) {
        throw 'Expected a missing, null, or non-Boolean hasNextPage to fail closed.'
    }
}

$fetchFailureThrew = $false
try {
    Get-AllReviewThreads -FetchPage { throw 'simulated API failure' }
}
catch {
    $fetchFailureThrew = $true
}
if (-not $fetchFailureThrew) {
    throw 'Expected a page-fetch failure to propagate.'
}

Write-Host 'OK review-thread pagination tests passed.'
