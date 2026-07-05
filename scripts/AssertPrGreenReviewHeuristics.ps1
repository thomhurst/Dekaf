function Get-ActionableReviewBodyReason {
    [CmdletBinding()]
    param(
        [AllowNull()][string]$Body
    )

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $null
    }

    $nonActionableHeading =
        '(?:\d+\.\s+)?(?:minor|optional|nit|non[- ]blocking)\b' +
        '|previously[- ]flagged\b.*\b(?:fixed|resolved|addressed|verified)\b'
    $actionableHeadingWord = 'Correctness|Bug|Bugs|Concern|Concerns|Issue|Issues|Regression|Risk|Risks|Design|Leak|Leaks|Gap|Gaps|Silently|(?<!not )(?<!non-)Blocking|(?<!not )(?<!non-)Blocker|Test coverage gap|Required|Must fix'

    $patterns = @(
        @{
            Reason = 'actionable heading'
            Pattern = "(?im)^\s*#{2,4}\s+(?!$nonActionableHeading).*\b($actionableHeadingWord)\b"
        },
        @{
            Reason = 'numbered finding heading'
            Pattern = '(?im)^\s*#{2,4}\s+\d+\.\s+(?!(?:minor|optional|nit|non[- ]blocking)\b).+'
        },
        @{
            Reason = 'before-merge action'
            Pattern = '(?im)\b(?:worth|needs?|should|must|required|please|recommend(?:ed)?)\s+(?:be\s+)?(?:address(?:ed|ing)?|fix(?:ed|ing)?)\b[^\r\n.]{0,120}\b(?:before\s+merg(?:e|ing)|prior\s+to\s+merg(?:e|ing))\b|\b(?:before\s+merg(?:e|ing)|prior\s+to\s+merg(?:e|ing))\b[^\r\n.]{0,120}\b(?:worth|needs?|should|must|required|please|recommend(?:ed)?)\s+(?:be\s+)?(?:address(?:ed|ing)?|fix(?:ed|ing)?)\b'
        }
    )

    foreach ($entry in $patterns) {
        if ($Body -match $entry.Pattern) {
            return "$($entry.Reason): $($Matches[0].Trim())"
        }
    }

    return $null
}
