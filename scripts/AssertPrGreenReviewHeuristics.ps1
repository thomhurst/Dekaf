function Get-ActionableReviewBodyReason {
    [CmdletBinding()]
    param(
        [AllowNull()][string]$Body
    )

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $null
    }

    $previouslyResolvedHeading =
        'previously[- ]flagged\b(?:(?!\bnot\b|\bnever\b|\bstill\b|\bun\w+\b).)*\b(?:fixed|resolved|addressed|verified)\b\s*$'
    $nonActionableHeading =
        '(?:\d+\.\s+)?(?:minor|optional|nit|non[- ]blocking)\b' +
        "|$previouslyResolvedHeading"
    $actionableHeadingWord = 'Correctness|Bug|Bugs|Concern|Concerns|Issue|Issues|Regression|Risk|Risks|Design|Leak|Leaks|Gap|Gaps|Silently|(?<!not )(?<!non-)Blocking|(?<!not )(?<!non-)Blocker|Test coverage gap|Required|Must fix'
    $categoryOnlyHeading = '(?:correctness|design|architecture)(?:\s*/\s*(?:correctness|design|architecture))*'
    $positiveCategorySection =
        '\bno\s+(?:\w+\s+){0,5}(?:bugs?|issues?|concerns?|blockers?|findings?|problems?)\b(?:\s+(?:found|detected|identified|seen|remain|remaining))?'

    $categoryHeadingPattern = "(?im)^\s*#{2,4}\s+($categoryOnlyHeading)\s*:?\s*$"
    foreach ($heading in [regex]::Matches($Body, $categoryHeadingPattern)) {
        $sectionStart = $heading.Index + $heading.Length
        $sectionRemainder = $Body.Substring($sectionStart)
        $nextHeading = [regex]::Match($sectionRemainder, '(?m)^\s*#{2,4}\s+')
        $sectionBody = if ($nextHeading.Success) {
            $sectionRemainder.Substring(0, $nextHeading.Index)
        } else {
            $sectionRemainder
        }
        $firstParagraph = [regex]::Match($sectionBody, '(?ms)\S.*?(?:\r?\n\s*\r?\n|$)').Value

        if ($firstParagraph -notmatch $positiveCategorySection) {
            return "actionable category heading: $($heading.Value.Trim())"
        }
    }

    $patterns = @(
        @{
            Reason = 'actionable heading'
            Pattern = "(?im)^\s*#{2,4}\s+(?!$nonActionableHeading)(?!$categoryOnlyHeading\s*:?\s*$).*\b($actionableHeadingWord)\b"
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
