"""Turn raw stress-profile artifacts into a decision-ready markdown summary.

Inputs (produced by tools/profile-stress-test.sh in --profile-dir):
  profile-metadata.txt     key=value run metadata, including trace_windows and
                           the measured_start/counters_start epochs.
  runtime-counters.csv     1 Hz dotnet-counters output (System.Runtime + Dekaf).
  <label>.topN-inclusive.txt  dotnet-trace topN report per trace window.

Output: one markdown file with
  1. per-window counter aggregates (mean/max per counter per window), and
  2. window-to-window movement of topN inclusive CPU percentages,
so bottleneck decisions come from tables instead of eyeballing raw artifacts.
"""

from __future__ import annotations

import argparse
import csv
import re
import sys
from pathlib import Path

# Matches dotnet-trace PrintReportHelper rows: "12. Name(args)   45.67%   12.34%"
# The name field is fixed-width (70) so 2+ spaces always precede the metrics.
TOPN_ROW = re.compile(r"^\s*(\d+)\.\s+(?P<name>.*?)\s{2,}(?P<inclusive>\d+(?:\.\d+)?)%\s+(?P<exclusive>\d+(?:\.\d+)?)%")


def parse_metadata(path):
    metadata = {}
    if not path.exists():
        return metadata
    for line in path.read_text(encoding="utf-8").splitlines():
        key, sep, value = line.partition("=")
        if sep:
            metadata[key.strip()] = value.strip()
    return metadata


def parse_windows(spec):
    windows = []
    for part in spec.split(","):
        pieces = part.split(":")
        if len(pieces) != 3:
            continue
        offset, duration, label = pieces
        windows.append((int(offset), int(duration), label))
    return windows


def load_counter_rows(path):
    """Return (offset_seconds, provider, counter, type, value) per CSV row.

    dotnet-counters timestamps are local-time and culture-formatted, so rows
    are anchored by tick index instead: each distinct timestamp string is one
    1 Hz refresh interval from the counters start.
    """
    rows = []
    if not path.exists():
        return rows
    tick_by_stamp = {}
    with path.open(encoding="utf-8", newline="") as handle:
        reader = csv.reader(handle)
        header = next(reader, None)
        if header is None:
            return rows
        for record in reader:
            if len(record) < 5:
                continue
            stamp, provider, counter, counter_type, value = record[:5]
            try:
                numeric = float(value)
            except ValueError:
                continue
            tick = tick_by_stamp.setdefault(stamp, len(tick_by_stamp))
            rows.append((tick, provider, counter, counter_type, numeric))
    return rows


def summarize_counters(rows, windows, counter_skew_seconds):
    """Aggregate counter values into mean/max per (counter, window)."""
    aggregates = {}
    for tick, provider, counter, counter_type, value in rows:
        offset = tick + counter_skew_seconds
        for start, duration, label in windows:
            if start <= offset < start + duration:
                key = (provider, counter, counter_type)
                stats = aggregates.setdefault(key, {})
                window_stats = stats.setdefault(label, [0, 0.0, float("-inf")])
                window_stats[0] += 1
                window_stats[1] += value
                window_stats[2] = max(window_stats[2], value)
                break
    return aggregates


def format_value(value):
    if value != value:  # NaN
        return "-"
    magnitude = abs(value)
    if magnitude >= 1_000_000_000:
        return f"{value / 1_000_000_000:.2f}G"
    if magnitude >= 1_000_000:
        return f"{value / 1_000_000:.2f}M"
    if magnitude >= 1_000:
        return f"{value / 1_000:.2f}k"
    if magnitude >= 1:
        return f"{value:.2f}"
    return f"{value:.4g}"


def counter_table(aggregates, windows):
    lines = []
    labels = [label for _, _, label in windows]
    if not aggregates:
        lines.append("_No counter data captured._")
        return lines
    header = "| Counter | Type | " + " | ".join(f"{label} mean (max)" for label in labels) + " |"
    lines.append(header)
    lines.append("|" + "---|" * (2 + len(labels)))
    for (provider, counter, counter_type) in sorted(aggregates):
        stats = aggregates[(provider, counter, counter_type)]
        cells = []
        for label in labels:
            if label in stats:
                count, total, peak = stats[label]
                cells.append(f"{format_value(total / count)} ({format_value(peak)})")
            else:
                cells.append("-")
        lines.append(f"| {provider} / {counter} | {counter_type} | " + " | ".join(cells) + " |")
    return lines


def parse_topn(path):
    """Return {function name: inclusive percent} for one topN report file."""
    percents = {}
    if not path.exists():
        return percents
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        match = TOPN_ROW.match(line)
        if not match:
            continue
        name = match.group("name").strip()
        value = float(match.group("inclusive"))
        # Fixed-width truncation can collide distinct methods; keep the larger.
        percents[name] = max(value, percents.get(name, 0.0))
    return percents


def topn_movement_table(per_window, windows, limit=25):
    lines = []
    labels = [label for _, _, label in windows]
    present = [label for label in labels if per_window.get(label)]
    if len(present) < 2:
        lines.append("_Fewer than two parseable topN reports; no movement to compare._")
        return lines

    methods = set()
    for label in present:
        methods.update(per_window[label])

    def delta(method):
        first = per_window[present[0]].get(method, 0.0)
        last = per_window[present[-1]].get(method, 0.0)
        return last - first

    ranked = sorted(methods, key=lambda m: abs(delta(m)), reverse=True)[:limit]
    header = "| Function (inclusive %) | " + " | ".join(present) + " | Δ last−first |"
    lines.append(header)
    lines.append("|" + "---|" * (2 + len(present)))
    for method in ranked:
        cells = []
        for label in present:
            value = per_window[label].get(method)
            cells.append(f"{value:.2f}" if value is not None else "-")
        lines.append(f"| `{method}` | " + " | ".join(cells) + f" | {delta(method):+.2f} |")
    return lines


def build_summary(profile_dir):
    profile_dir = Path(profile_dir)
    metadata = parse_metadata(profile_dir / "profile-metadata.txt")
    windows = parse_windows(metadata.get("trace_windows", ""))

    lines = ["# Stress Profile Summary", ""]
    if metadata:
        lines.append(f"- Profile: `{metadata.get('trace_profile', 'unknown')}`")
        lines.append(f"- Windows: `{metadata.get('trace_windows', 'unknown')}`")
        lines.append(f"- Stress args: `{metadata.get('stress_args', 'unknown')}`")
        lines.append("")
    if not windows:
        lines.append("_No trace windows found in profile-metadata.txt; nothing to summarize._")
        return "\n".join(lines) + "\n"

    try:
        counter_skew = int(metadata.get("counters_start_epoch", "")) - int(
            metadata.get("measured_start_epoch", "")
        )
    except ValueError:
        counter_skew = 0

    lines.append("## Counter aggregates by window")
    lines.append("")
    rows = load_counter_rows(profile_dir / "runtime-counters.csv")
    lines.extend(counter_table(summarize_counters(rows, windows, counter_skew), windows))
    lines.append("")

    lines.append("## Top-method movement across windows")
    lines.append("")
    per_window = {
        label: parse_topn(profile_dir / f"{label}.topN-inclusive.txt")
        for _, _, label in windows
    }
    lines.extend(topn_movement_table(per_window, windows))
    lines.append("")
    return "\n".join(lines) + "\n"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile-dir", required=True, help="Directory of profile artifacts")
    parser.add_argument("--output", required=True, help="Markdown output path")
    args = parser.parse_args(argv)

    summary = build_summary(args.profile_dir)
    output = Path(args.output)
    output.write_text(summary, encoding="utf-8")
    print(f"Wrote {output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
