function Get-ActionableReviewBodyReason {
    [CmdletBinding()]
    param(
        [AllowNull()][string]$Body
    )

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $null
    }

    $previouslyResolvedHeading =
        'previously[- ]flagged\b(?:(?!\bnot\b|\bnever\b|\bstill\b|\bun(?:fixed|resolved|addressed|verified|handled)\b).)*\b(?:fixed|resolved|addressed|verified)\b\s*$'
    $resolvedFindingHeading =
        '(?:(?:fix|change|commit|follow[- ]up)\b(?:(?!\bnot\b|\bnever\b|\bstill\b|\bun(?:fixed|resolved|addressed|verified|handled)\b).)*\b(?:fix(?:es|ed)?|resolves?|resolved|addresses?|addressed)\b(?:(?!\bnot\b|\bnever\b|\bstill\b|\bun(?:fixed|resolved|addressed|verified|handled)\b).)*\b(?:issues?|bugs?|findings?|concerns?|regressions?)|(?:(?!\bnot\b|\bnever\b|\bstill\b|\bun(?:fixed|resolved|addressed|verified|handled)\b).)*\b(?:issues?|bugs?|findings?|concerns?|regressions?)\b(?:(?!\bnot\b|\bnever\b|\bstill\b|\bun(?:fixed|resolved|addressed|verified|handled)\b).)*\b(?:fixed|resolved|addressed|verified)\b(?:\s+in\s+`?[\w]+`?)?)\s*$'
    $nonActionableHeading =
        '(?:\d+\.\s+)?(?:minor|optional|nit|non[- ]blocking)\b' +
        '|not\s+a\s+regression\b(?:,\s+just\s+noting\s+scope)?\s*$' +
        "|$previouslyResolvedHeading" +
        "|$resolvedFindingHeading"
    $actionableHeadingWord = 'Correctness|Bug|Bugs|Concern|Concerns|Issue|Issues|Regression|Risk|Risks|Design|Leak|Leaks|Gap|Gaps|Silently|(?<!not )(?<!non-)Blocking|(?<!not )(?<!non-)Blocker|Test coverage gap|Required|Must fix'
    $horizontalWhitespace = '[^\S\r\n]'
    $categoryHeadingTerm = "(?:correctness|security|design|architecture|claude\.md$horizontalWhitespace+(?:compliance|conventions))"
    $categoryOnlyHeading = "$categoryHeadingTerm(?:$horizontalWhitespace*/$horizontalWhitespace*$categoryHeadingTerm)*"
    $noCategoryFindings =
        '\bno\s+(?:\w+\s+){0,5}(?:bugs?|issues?|concerns?|blockers?|findings?|problems?)\b(?:\s+(?:found|detected|identified|seen|remain|remaining))?'
    $positiveVerdictBlocker =
        '\b(?:though|that\s+said|still|incorrectly|miss(?:es|ing)?|deadlocks?|will\s+throw|nullreferenceexception|box(?:es|ing|ed)?|allocat(?:es|ing|ed)|off[- ]by[- ]one|broken|leaks?|race|corrupt(?:s|ion)?|unsafe|real\s+bug|edge\s+case|not\s+(?:thread[- ]safe|safe|correct|fixed|resolved|addressed|scoped))\b'
    $positiveVerdictAlternatives = @(
        "$noCategoryFindings(?:[\s\S]*)?"
        'looks?\s+(?:right|good)'
        'verified(?:\s+against\b[\s\S]*)?'
        'confirmed\b(?:[\s\S]*)?'
        '(?:[\s\S]*\b)?no\s+concerns\b(?:[\s\S]*)?'
        '(?:[\s\S]*\b)?configureawait\(false\)(?:[\s\S]*\b)?used\s+consistently(?:[\s\S]*)?'
        '(?:the\s+)?core\s+fix\s+is\s+(?:sound|correct)(?:[\s\S]*)?'
        'genuine\s+improvement,\s+not\s+just\s+churn(?:[\s\S]*)?'
        'fix\s+is\s+scoped(?:\s+to\b[\s\S]*)?'
        '(?:[\s\S]*\b)?allocation[- ]free\s+helper(?:[\s\S]*)?'
    ) -join '|'
    $positiveCategoryVerdict =
        "(?is)^(?!.*$positiveVerdictBlocker)\s*(?:[-*]\s*)?(?:$positiveVerdictAlternatives)\.?\s*$"
    $positiveCategoryHeadingVerdict = $positiveCategoryVerdict
    $positiveCategorySection = $positiveCategoryVerdict

    $categoryHeadingWithOptionalVerdict = "$categoryOnlyHeading(?:(?:$horizontalWhitespace*(?:[-:]|\p{Pd})$horizontalWhitespace*\S[^\r\n#]*)|(?:$horizontalWhitespace*\([^\r\n#)]*\))|(?:$horizontalWhitespace+\S[^\r\n#]*))?:?"
    $categoryHeadingPattern = "(?im)^$horizontalWhitespace*#{2,4}$horizontalWhitespace+($categoryOnlyHeading)(?:(?:$horizontalWhitespace*(?:[-:]|\p{Pd})$horizontalWhitespace*(?<verdict>\S[^\r\n#]*))|(?:$horizontalWhitespace*\((?<parentheticalVerdict>[^)\r\n#]+)\))|(?:$horizontalWhitespace+(?<bareVerdict>\S[^\r\n#]*)))?:?$horizontalWhitespace*\r?$"
    foreach ($heading in [regex]::Matches($Body, $categoryHeadingPattern)) {
        $headingVerdict = if ($heading.Groups['verdict'].Success) {
            $heading.Groups['verdict'].Value
        } elseif ($heading.Groups['bareVerdict'].Success) {
            $heading.Groups['bareVerdict'].Value
        } else {
            $heading.Groups['parentheticalVerdict'].Value
        }
        if ($headingVerdict -and $headingVerdict -notmatch $positiveCategoryHeadingVerdict) {
            return "actionable category heading: $($heading.Value.Trim())"
        }

        $sectionStart = $heading.Index + $heading.Length
        $sectionRemainder = $Body.Substring($sectionStart)
        $nextHeading = [regex]::Match($sectionRemainder, '(?m)^\s*#{2,4}\s+')
        $sectionBody = if ($nextHeading.Success) {
            $sectionRemainder.Substring(0, $nextHeading.Index)
        } else {
            $sectionRemainder
        }
        $firstParagraph = [regex]::Match($sectionBody, '(?ms)\S.*?(?:\r?\n\s*\r?\n|$)').Value

        if ([string]::IsNullOrWhiteSpace($firstParagraph)) {
            if ($headingVerdict) {
                continue
            }

            return "actionable category heading: $($heading.Value.Trim())"
        }

        if ($firstParagraph -notmatch $positiveCategorySection) {
            return "actionable category heading: $($heading.Value.Trim())"
        }
    }

    $patterns = @(
        @{
            Reason = 'actionable heading'
            Pattern = "(?im)^\s*#{2,4}\s+(?!$nonActionableHeading)(?!$categoryHeadingWithOptionalVerdict\s*$).*\b($actionableHeadingWord)\b"
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
