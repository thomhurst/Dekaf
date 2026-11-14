"""Print a Stryker --mutate pattern that covers one shard of a large C# file.

Stryker.NET accepts a character span after the file pattern
(``Producer/RecordAccumulator.cs{start..end}``) and only mutates code that the
span fully contains. Each cut is placed on the blank line after a closing brace
at member indentation, so no method body (and no mutant) straddles two shards.
Cuts are recomputed from the checked-out file, so they follow edits on main.

Usage: mutation_shard.py <project-dir> <relative-file> <shard> <shards>
"""

import sys
from pathlib import Path

MEMBER_END = "    }"


def member_cuts(text):
    """Character offsets of blank lines that follow a member's closing brace."""
    cuts = []
    offset = 0
    previous = ""
    for line in text.splitlines(keepends=True):
        if previous.rstrip("\r\n") == MEMBER_END and line.strip() == "":
            cuts.append(offset)
        offset += len(line)
        previous = line
    return cuts


def shard_span(text, shard, shards):
    if not 1 <= shard <= shards:
        raise ValueError(f"shard {shard} is outside 1..{shards}")
    cuts = member_cuts(text)
    if shards > 1 and not cuts:
        raise ValueError("no member boundary to cut at")
    bounds = [0]
    for index in range(1, shards):
        target = len(text) * index // shards
        bounds.append(min(cuts, key=lambda cut: abs(cut - target)))
    bounds.append(len(text))
    if bounds != sorted(set(bounds)):
        raise ValueError(f"file has too few member boundaries for {shards} shards")
    return bounds[shard - 1], bounds[shard]


def main(argv):
    project_dir, relative_file, shard, shards = argv[1], argv[2], int(argv[3]), int(argv[4])
    # Offsets must match Roslyn's source text: drop the BOM, keep line endings as-is.
    with open(Path(project_dir) / relative_file, encoding="utf-8-sig", newline="") as source:
        text = source.read()
    start, end = shard_span(text, shard, shards)
    print(f"{relative_file}{{{start}..{end}}}")


if __name__ == "__main__":
    main(sys.argv)
