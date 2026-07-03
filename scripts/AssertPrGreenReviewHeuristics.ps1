function Get-ActionableReviewBodyReason {
    [CmdletBinding()]
    param(
        [AllowNull()][string]$Body
    )

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $null
    }

    $patterns = @(
        @{
            Reason = 'actionable heading'
            Pattern = '(?im)^\s*#{2,4}\s+(?!(?:minor|optional|nit|non[- ]blocking)\b).*\b(Correctness|Bug|Bugs|Concern|Concerns|Issue|Issues|Regression|Risk|Risks|Design|Leak|Leaks|Gap|Gaps|Silently|Blocking|Blocker|Test coverage gap|Required|Must fix)\b'
        },
        @{
            Reason = 'numbered finding heading'
            Pattern = '(?im)^\s*#{2,4}\s+\d+\.\s+(?!(?:minor|optional|nit|non[- ]blocking)\b).+'
        },
        @{
            Reason = 'before-merge action'
            Pattern = '(?im)\b(?:worth|needs?|should|must|required|please|recommend(?:ed)?)\s+(?:be\s+)?address(?:ed|ing)?\b[^\r\n.]{0,120}\bbefore\s+merge\b|\bbefore\s+merge\b[^\r\n.]{0,120}\b(?:worth|needs?|should|must|required|please|recommend(?:ed)?)\s+(?:be\s+)?address(?:ed|ing)?\b'
        }
    )

    foreach ($entry in $patterns) {
        if ($Body -match $entry.Pattern) {
            return "$($entry.Reason): $($Matches[0].Trim())"
        }
    }

    return $null
}
