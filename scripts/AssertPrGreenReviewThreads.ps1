function Get-AllReviewThreads {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][scriptblock]$FetchPage
    )

    $threads = [System.Collections.Generic.List[object]]::new()
    $seenCursors = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $cursor = $null

    while ($true) {
        $page = & $FetchPage $cursor
        if ($null -eq $page -or $null -eq $page.pageInfo) {
            throw 'Review-thread response did not contain pageInfo.'
        }

        $hasNextPageProperty = $page.pageInfo.PSObject.Properties['hasNextPage']
        if ($null -eq $hasNextPageProperty -or $hasNextPageProperty.Value -isnot [bool]) {
            throw 'Review-thread pageInfo did not contain a Boolean hasNextPage.'
        }

        foreach ($thread in @($page.nodes)) {
            if ($null -eq $thread) {
                throw 'Review-thread response contained a null thread.'
            }

            $threads.Add($thread)
        }

        if (-not $hasNextPageProperty.Value) {
            return $threads.ToArray()
        }

        $nextCursor = [string]$page.pageInfo.endCursor
        if ([string]::IsNullOrWhiteSpace($nextCursor)) {
            throw 'Review-thread response has another page but no end cursor.'
        }

        if (-not $seenCursors.Add($nextCursor)) {
            throw "Review-thread pagination repeated cursor '$nextCursor'."
        }

        $cursor = $nextCursor
    }
}
