"""Summarize Stryker.NET JSON and gate only material score regressions."""

from __future__ import annotations

import argparse
import json
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Sequence


DETECTED_STATUSES = frozenset({"Killed", "Timeout"})
UNDETECTED_STATUSES = frozenset({"Survived", "NoCoverage"})
BACKLOG_PRIORITY = {"Survived": 0, "NoCoverage": 1}
OUTCOME_ORDER = ("Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "RuntimeError", "Ignored")


@dataclass(frozen=True)
class BacklogEntry:
    status: str
    path: str
    line: int
    mutator: str
    replacement: str


@dataclass(frozen=True)
class MutationSummary:
    score: float
    detected: int
    undetected: int
    statuses: Counter[str]
    backlog: tuple[BacklogEntry, ...]


def _line(mutant: dict[str, Any]) -> int:
    return int(mutant.get("location", {}).get("start", {}).get("line", 0))


def summarize(report: dict[str, Any]) -> MutationSummary:
    statuses: Counter[str] = Counter()
    backlog: list[BacklogEntry] = []

    files = report.get("files")
    if not isinstance(files, dict):
        raise ValueError("Stryker report has no 'files' object")

    for path, source_file in files.items():
        for mutant in source_file.get("mutants", []):
            status = mutant.get("status", "Unknown")
            statuses[status] += 1
            if status in BACKLOG_PRIORITY:
                backlog.append(
                    BacklogEntry(
                        status=status,
                        path=path,
                        line=_line(mutant),
                        mutator=mutant.get("mutatorName", "unknown"),
                        replacement=mutant.get("replacement", ""),
                    )
                )

    detected = sum(statuses[status] for status in DETECTED_STATUSES)
    undetected = sum(statuses[status] for status in UNDETECTED_STATUSES)
    scored = detected + undetected
    score = 100.0 * detected / scored if scored else 100.0
    backlog.sort(
        key=lambda entry: (
            BACKLOG_PRIORITY[entry.status],
            entry.path,
            entry.line,
            entry.mutator,
        )
    )

    return MutationSummary(
        score=score,
        detected=detected,
        undetected=undetected,
        statuses=statuses,
        backlog=tuple(backlog),
    )


def _markdown_cell(value: object) -> str:
    return str(value).replace("|", "\\|").replace("\r", " ").replace("\n", " ")


def render_markdown(
    current: MutationSummary,
    previous: MutationSummary | None,
    max_drop: float,
    backlog_limit: int = 50,
) -> tuple[str, bool]:
    if max_drop <= 0:
        raise ValueError("max_drop must be greater than zero")

    lines = ["# Mutation testing", "", f"- Current score: **{current.score:.2f}%**"]
    failed = False

    if previous is None:
        lines.append("- Previous score: unavailable (first retained run)")
        lines.append("- Large-drop gate: passed (no baseline)")
    else:
        delta = current.score - previous.score
        failed = delta <= -max_drop
        lines.append(f"- Previous score: **{previous.score:.2f}%**")
        lines.append(f"- Trend: **{delta:+.2f} percentage points**")
        lines.append(
            f"- Large-drop gate: {'failed' if failed else 'passed'} "
            f"(fails at a drop of {max_drop:.2f} points or more)"
        )

    lines.extend(
        [
            "",
            "## Mutant outcomes",
            "",
            "| Outcome | Count |",
            "|---|---:|",
        ]
    )
    for status in OUTCOME_ORDER:
        lines.append(f"| {status} | {current.statuses[status]} |")

    lines.extend(["", "## Prioritized test backlog", ""])
    if not current.backlog:
        lines.append("No surviving or uncovered mutants.")
    else:
        lines.extend(
            [
                "Survived mutants lead uncovered mutants because existing tests execute them but fail to detect the behavior change.",
                "",
                "| Priority | Status | Location | Mutator | Replacement |",
                "|---:|---|---|---|---|",
            ]
        )
        for index, entry in enumerate(current.backlog[:backlog_limit], start=1):
            location = f"{entry.path}:{entry.line}" if entry.line else entry.path
            lines.append(
                f"| {index} | {entry.status} | {_markdown_cell(location)} | "
                f"{_markdown_cell(entry.mutator)} | `{_markdown_cell(entry.replacement)}` |"
            )
        omitted = len(current.backlog) - backlog_limit
        if omitted > 0:
            lines.extend(["", f"{omitted} more entries remain in uploaded HTML/JSON reports."])

    return "\n".join(lines) + "\n", failed


def _load(path: Path) -> MutationSummary:
    with path.open(encoding="utf-8") as stream:
        return summarize(json.load(stream))


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("current", type=Path, help="Current Stryker JSON report")
    parser.add_argument("--previous", type=Path, help="Previous successful main-branch JSON report")
    parser.add_argument("--max-drop", type=float, default=5.0, help="Maximum allowed score drop in points")
    parser.add_argument("--summary", type=Path, help="Write GitHub-flavored Markdown summary here")
    parser.add_argument("--backlog-limit", type=int, default=50)
    args = parser.parse_args(argv)

    current = _load(args.current)
    previous = _load(args.previous) if args.previous else None
    markdown, failed = render_markdown(current, previous, args.max_drop, args.backlog_limit)

    if args.summary:
        args.summary.parent.mkdir(parents=True, exist_ok=True)
        args.summary.write_text(markdown, encoding="utf-8")
    else:
        print(markdown, end="")

    if failed:
        print(
            f"::error::Mutation score dropped from {previous.score:.2f}% to {current.score:.2f}% "
            f"(limit: {args.max_drop:.2f} percentage points)"
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
