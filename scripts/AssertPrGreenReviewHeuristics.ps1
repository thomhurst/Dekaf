function Get-ActionableReviewBodyReason {
    [CmdletBinding()]
    param(
        [AllowNull()][string]$Body
    )

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $null
    }

    $previouslyResolvedHeading =
        '(?=.{1,300}\s*$)previously[- ]flagged\b(?![^\r\n]*\b(?:not|never|still|un(?:fixed|resolved|addressed|verified|handled))\b)(?=[^\r\n]*\b(?:fixed|resolved|addressed|verified)\b)[^\r\n]*\s*$'
    $resolvedFindingHeading =
        '(?=.{1,300}\s*$)(?![^\r\n]*\b(?:not|never|still|un(?:fixed|resolved|addressed|verified|handled))\b)(?=[^\r\n]*\b(?:fix|fix(?:es|ed)?|change|commit|follow[- ]up|resolves?|resolved|addresses?|addressed|verified)\b)(?=[^\r\n]*\b(?:issues?|bugs?|findings?|concerns?|regressions?)\b)[^\r\n]*\s*$'
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
    $positiveVerdictDefect =
        '(?<!not\s)incorrect(?:ly)?|(?<!not\s)(?<!nothing\s)wrong|miss(?:es|ing)?|deadlocks?|will\s+throw|crash(?:es|ing|ed)?|fixme|nullreferenceexception|box(?:es|ing|ed)?|allocat(?:es|ing|ed)|will\s+allocate|allocate\s+per|(?:per[- ]message|array)\s+(?:\w+\s+){0,3}allocations?|off[- ]by[- ]one|still\s+broken|remains?\s+broken|is\s+broken|leak(?:s|ing|ed)?|race|corrupt(?:s|ion|ed|ing)?|vulnerabilit(?:y|ies)|vulnerable|insecure|injection|hardcoded|guessable|session\s+token|stack\s+overflow|hang(?:s|ing)?|forever|infinite\s+loop|use[- ]after[- ]free|double[- ]free|(?<!no\s)data\s+loss|silent(?:ly)?\s+drops?(?:\s+\w+){0,2}\s+messages?|skip(?:s|ped|ping)?\s+validation|design\s+risk|risk\s+(?:for|of)|real\s+concerns?|concerns?\s+about|(?:test\s+)?coverage\s+gap|(?<!no\s)(?<!non[- ])block(?:er|ing)|fix\s+is\s+required|required\s+fix|required\s+before|real\s+(?:bug|issue)|edge\s+case|not\s+(?:thread[- ]safe|safe|correct|fixed|resolved|addressed|scoped)'
    $positiveVerdictBlocker = '\b(?:' + $positiveVerdictDefect + ')\b'
    $positiveVerdictContinuationDefect =
        "$positiveVerdictDefect|(?<!not\s+a\s)(?<!no\s)regressions?(?!\s+(?:coverage|tests?|risk))|now\s+duplicated|(?<!used\s+to\s+be\s)(?<!previously\s)duplicated\s+across|duplicates?\s+logic|duplication\s+of|swallow(?:s|ed|ing)?"
    $positiveVerdictContinuationBlocker =
        '\b(?:' + $positiveVerdictContinuationDefect + ')\b'
    $positiveVerdictAlternatives = @(
        "$noCategoryFindings(?:[\s\S]*)?"
        'looks?\s+(?:right|good)(?:[\s\S]*)?'
        'verified(?:\s+against\b[\s\S]*)?'
        'confirmed\b(?:[\s\S]*)?'
        'no\s+concerns\b(?:[\s\S]*)?'
        '`?configureawait\(false\)`?(?:[\s\S]*\b)?used\s+consistently(?:[\s\S]*)?'
        '(?:the\s+)?core\s+fix\s+is\s+(?:sound|correct)(?:[\s\S]*)?'
        'genuine\s+improvement,\s+not\s+just\s+churn(?:[\s\S]*)?'
        'fix\s+is\s+scoped(?:\s+to\b[\s\S]*)?'
        '(?:[\s\S]*\b)?allocation[- ]free(?:[\s\S]*)?'
    ) -join '|'
    $positiveCategoryVerdict =
        "(?is)^(?!.*$positiveVerdictBlocker)(?!.*$positiveVerdictContinuationBlocker)\s*(?:[-*]\s*)?(?:$positiveVerdictAlternatives)\.?\s*$"
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
        $firstVerdictMatch = [regex]::Match($sectionBody, '(?m)\S[^\r\n]*')
        $firstVerdict = $firstVerdictMatch.Value.Trim()

        if ([string]::IsNullOrWhiteSpace($firstVerdict)) {
            if ($headingVerdict) {
                continue
            }

            return "actionable category heading: $($heading.Value.Trim())"
        }

        if ($firstVerdict -notmatch $positiveCategorySection) {
            return "actionable category heading: $($heading.Value.Trim())"
        }

        $sectionTailStart = $firstVerdictMatch.Index + $firstVerdictMatch.Length
        $sectionTail = $sectionBody.Substring($sectionTailStart)
        if ($sectionTail -match $positiveVerdictContinuationBlocker) {
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
