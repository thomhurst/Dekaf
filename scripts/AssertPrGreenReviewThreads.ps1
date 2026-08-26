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

        $nodesProperty = $page.PSObject.Properties['nodes']
        if ($null -eq $nodesProperty -or $null -eq $nodesProperty.Value) {
            throw 'Review-thread response did not contain nodes.'
        }

        foreach ($thread in @($nodesProperty.Value)) {
            if ($null -eq $thread) {
                throw 'Review-thread response contained a null thread.'
            }

            $isResolvedProperty = $thread.PSObject.Properties['isResolved']
            if ($null -eq $isResolvedProperty -or $isResolvedProperty.Value -isnot [bool]) {
                throw 'Review-thread node did not contain a Boolean isResolved.'
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
