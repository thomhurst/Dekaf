"""Generate benchmark documentation with noise-aware rolling comparisons."""

import argparse
import glob
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from statistics import median


DEFAULT_WINDOW = 5
DEFAULT_VARIANCE_THRESHOLD = 0.30
_DATA_PREFIX = "window.BENCHMARK_DATA = "
_BENCHMARK_NAME = re.compile(
    r"^(?P<class>.+\.Client\.[^.]+)\."
    r"(?P<client>Dekaf|Confluent)_(?P<operation>[^()]+)"
    r"(?P<parameters>\(.*\))?$"
)
_DEKAF_METHOD = re.compile(r"Dekaf_(?P<operation>\w+)")

# Ratios inside this band are indistinguishable from CI noise.
_PARITY_LOW = 0.83
_PARITY_HIGH = 1.20

_SCENARIO_LABELS = {
    ("ProducerBenchmarks", "ProduceSingle"): ("Produce — one message at a time (awaited)", 0),
    ("ProducerBenchmarks", "ProduceBatch"): ("Produce — batches", 1),
    ("ProducerBenchmarks", "FireAndForget"): ("Produce — fire-and-forget", 2),
    ("ConsumerBenchmarks", "ConsumeAll"): ("Consume — drain a topic", 3),
    ("ConsumerPollBenchmarks", "PollSingle"): ("Consume — poll a single message", 4),
}


def load_history(path):
    text = Path(path).read_text(encoding="utf-8").strip()
    if not text.startswith(_DATA_PREFIX):
        raise ValueError("Benchmark history does not start with window.BENCHMARK_DATA")

    payload = text[len(_DATA_PREFIX) :].rstrip(";").strip()
    data = json.loads(payload)
    return data.get("entries", {}).get("Dekaf Benchmarks", [])


def _comparison_pairs(entry):
    pairs = {}
    for benchmark in entry.get("benches", []):
        match = _BENCHMARK_NAME.match(benchmark.get("name", ""))
        value = benchmark.get("value")
        if match is None or not isinstance(value, (int, float)):
            continue

        key = (
            match.group("class"),
            match.group("operation"),
            match.group("parameters") or "",
        )
        pairs.setdefault(key, {})[match.group("client")] = value
    return pairs


def rolling_comparisons(entries, window=DEFAULT_WINDOW):
    if not entries:
        return []

    observations = {}
    current_keys = {
        key
        for key, clients in _comparison_pairs(entries[-1]).items()
        if clients.get("Dekaf") is not None and clients.get("Confluent") not in (None, 0)
    }

    for entry in entries:
        for key, clients in _comparison_pairs(entry).items():
            if key not in current_keys:
                continue
            dekaf = clients.get("Dekaf")
            confluent = clients.get("Confluent")
            if dekaf is None or confluent in (None, 0):
                continue
            observations.setdefault(key, []).append(dekaf / confluent)

    comparisons = []
    for (class_name, operation, parameters), ratios in observations.items():
        recent = ratios[-window:]
        ratio_median = median(recent)
        ratio_min = min(recent)
        ratio_max = max(recent)
        relative_spread = (
            (ratio_max - ratio_min) / ratio_median if ratio_median else float("inf")
        )
        comparisons.append(
            {
                "group": class_name.rsplit(".", 1)[-1],
                "operation": operation,
                "parameters": parameters[1:-1] if parameters else "—",
                "runs": len(recent),
                "median": ratio_median,
                "minimum": ratio_min,
                "maximum": ratio_max,
                "relative_spread": relative_spread,
            }
        )

    return sorted(
        comparisons,
        key=lambda item: (item["group"], item["operation"], item["parameters"]),
    )


def _scenario_label(group, operation):
    return _SCENARIO_LABELS.get((group, operation), (f"{group}.{operation}", 99))


def _times(value):
    return f"{value:.0f}×" if value >= 9.95 else f"{value:.1f}×"


def describe_speed(ratio):
    if ratio < _PARITY_LOW:
        return f"{_times(1 / ratio)} faster"
    if ratio <= _PARITY_HIGH:
        return "on par"
    return f"{_times(ratio)} slower"


def describe_speed_range(best, worst):
    best_text = describe_speed(best)
    worst_text = describe_speed(worst)
    if best_text == worst_text:
        return best_text
    if best_text.endswith("faster") and worst_text.endswith("faster"):
        return f"{_times(1 / worst)}–{_times(1 / best)} faster"
    if best_text.endswith("slower") and worst_text.endswith("slower"):
        return f"{_times(best)}–{_times(worst)} slower"
    return f"{worst_text} to {best_text}"


def describe_memory(alloc_ratio):
    if alloc_ratio is None:
        return "—"
    if alloc_ratio <= 0:
        return "zero allocations"
    if alloc_ratio < _PARITY_LOW:
        return f"{_times(1 / alloc_ratio)} less"
    if alloc_ratio <= _PARITY_HIGH:
        return "on par"
    return f"{_times(alloc_ratio)} more"


def summarize_scenarios(comparisons, variance_threshold=DEFAULT_VARIANCE_THRESHOLD):
    scenarios = {}
    for comparison in comparisons:
        key = (comparison["group"], comparison["operation"])
        scenarios.setdefault(key, []).append(comparison)

    summaries = []
    for (group, operation), rows in scenarios.items():
        medians = [row["median"] for row in rows]
        stable = sum(
            1
            for row in rows
            if row["runs"] >= 2 and row["relative_spread"] <= variance_threshold
        )
        summaries.append(
            {
                "group": group,
                "operation": operation,
                "best": min(medians),
                "worst": max(medians),
                "median": median(medians),
                "stable_rows": stable,
                "total_rows": len(rows),
            }
        )

    def sort_key(item):
        label, order = _scenario_label(item["group"], item["operation"])
        return (order, label)

    return sorted(summaries, key=sort_key)


def latest_alloc_ratios(paths):
    """Median Dekaf-vs-Confluent allocation ratio per operation from latest-run tables."""
    ratios = {}
    for markdown_path in paths:
        method_index = None
        alloc_index = None
        for line in Path(markdown_path).read_text(encoding="utf-8").splitlines():
            if not line.startswith("|"):
                continue
            cells = [_cell_text(cell) for cell in line.strip().strip("|").split("|")]
            if "Method" in cells:
                method_index = cells.index("Method")
                alloc_index = (
                    cells.index("Alloc Ratio") if "Alloc Ratio" in cells else None
                )
                continue
            if method_index is None or alloc_index is None or len(cells) <= alloc_index:
                continue
            match = _DEKAF_METHOD.search(cells[method_index])
            if match is None:
                continue
            try:
                value = float(cells[alloc_index])
            except ValueError:
                continue
            ratios.setdefault(match.group("operation"), []).append(value)

    return {operation: median(values) for operation, values in ratios.items()}


def _confidence_label(stable_rows, total_rows):
    if total_rows == 0:
        return "⚠ Insufficient history"
    if stable_rows == total_rows:
        return "Stable"
    if stable_rows == 0:
        return "⚠ Noisy"
    return "Mixed"


def format_summary_table(summaries, alloc_ratios):
    lines = [
        "| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |",
        "|---|---|---|---|",
    ]

    for summary in summaries:
        label, _ = _scenario_label(summary["group"], summary["operation"])
        lines.append(
            "| {label} | {speed} | {memory} | {confidence} |".format(
                label=label,
                speed=describe_speed_range(summary["best"], summary["worst"]),
                memory=describe_memory(alloc_ratios.get(summary["operation"])),
                confidence=_confidence_label(
                    summary["stable_rows"], summary["total_rows"]
                ),
            )
        )

    if not summaries:
        lines.append("| No comparable history yet | — | — | ⚠ Insufficient history |")

    return lines


def format_rolling_table(comparisons, variance_threshold):
    lines = [
        "| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |",
        "|---|---|---:|---:|---:|---:|---|",
    ]

    for comparison in comparisons:
        if comparison["runs"] < 2:
            confidence = "⚠ Insufficient history"
        elif comparison["relative_spread"] > variance_threshold:
            confidence = "⚠ Low"
        else:
            confidence = "Stable"

        lines.append(
            "| {group}.{operation} | {parameters} | {runs} | {median:.2f} | "
            "{minimum:.2f}–{maximum:.2f} | {spread:.0%} | {confidence} |".format(
                group=comparison["group"],
                operation=comparison["operation"],
                parameters=comparison["parameters"].replace("|", r"\|"),
                runs=comparison["runs"],
                median=comparison["median"],
                minimum=comparison["minimum"],
                maximum=comparison["maximum"],
                spread=comparison["relative_spread"],
                confidence=confidence,
            )
        )

    if not comparisons:
        lines.append(
            "| No comparable history available | — | 0 | — | — | — | "
            "⚠ Insufficient history |"
        )

    return lines


def _cell_text(cell):
    return cell.replace("*", "").replace("`", "").strip()


def annotate_ratio_confidence(lines, variance_threshold):
    output = []
    ratio_sd_index = None

    for line in lines:
        if not line.startswith("|"):
            continue

        cells = line.strip().strip("|").split("|")
        normalized = [_cell_text(cell) for cell in cells]

        if "Method" in normalized:
            ratio_sd_index = (
                normalized.index("RatioSD") if "RatioSD" in normalized else None
            )
            if ratio_sd_index is not None:
                cells.append(" Confidence ")
        elif ratio_sd_index is not None and all(
            re.fullmatch(r"\s*:?-+:?\s*", cell) for cell in cells
        ):
            cells.append("---")
        elif ratio_sd_index is not None:
            value = _cell_text(cells[ratio_sd_index])
            try:
                confidence = "⚠ Low" if float(value) > variance_threshold else "Stable"
            except ValueError:
                confidence = "—"
            cells.append(f" {confidence} ")

        output.append("|" + "|".join(cells) + "|")

    return output


def collect_tables(paths, variance_threshold=None):
    output = []
    for markdown_path in paths:
        lines = Path(markdown_path).read_text(encoding="utf-8").splitlines()
        if variance_threshold is not None:
            output.extend(annotate_ratio_confidence(lines, variance_threshold))
        else:
            output.extend(line for line in lines if line.startswith("|"))
        output.append("")
    return output


def _details_section(summary, body_lines):
    return [
        "<details>",
        f"<summary>{summary}</summary>",
        "",
        *body_lines,
        "</details>",
        "",
    ]


def generate_document(
    results_dir,
    history_path,
    updated_at,
    window=DEFAULT_WINDOW,
    variance_threshold=DEFAULT_VARIANCE_THRESHOLD,
):
    history = load_history(history_path)
    comparisons = rolling_comparisons(history, window)
    result_pattern = str(Path(results_dir))

    producer_md = sorted(
        glob.glob(f"{result_pattern}/Client/results/*ProducerBenchmarks*-github.md")
    )
    consumer_md = sorted(
        glob.glob(f"{result_pattern}/Client/results/*Consumer*Benchmarks*-github.md")
    )
    alloc_ratios = latest_alloc_ratios(producer_md + consumer_md)
    summaries = summarize_scenarios(comparisons, variance_threshold)

    output = [
        "---",
        "sidebar_position: 13",
        "---",
        "",
        "# Benchmark Results",
        "",
        "How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.",
        "",
        f"**Last Updated:** {updated_at}",
        "",
        "## At a glance",
        "",
        f"Each scenario is the median Dekaf-vs-Confluent result over the last {window} CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.",
        "",
        *format_summary_table(summaries, alloc_ratios),
        "",
        '"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.',
        "",
        "## Full results",
        "",
        *_details_section(
            f"Cross-run comparison — last {window} runs, per parameter set",
            [
                "Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.",
                "",
                f"Rows with run spread above {variance_threshold:.0%} are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.",
                "",
                *format_rolling_table(comparisons, variance_threshold),
                "",
            ],
        ),
    ]

    if producer_md:
        output.extend(
            _details_section(
                "Latest run — producer benchmarks",
                collect_tables(producer_md, variance_threshold),
            )
        )

    if consumer_md:
        output.extend(
            _details_section(
                "Latest run — consumer benchmarks",
                collect_tables(consumer_md, variance_threshold),
            )
        )

    protocol_md = sorted(
        glob.glob(f"{result_pattern}/Unit/results/*ProtocolBenchmarks*-github.md")
    )
    if protocol_md:
        output.extend(
            _details_section(
                "Protocol serialization — Dekaf internals",
                [
                    "Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.",
                    "",
                    *collect_tables(protocol_md),
                ],
            )
        )

    serializer_md = sorted(
        glob.glob(f"{result_pattern}/Unit/results/*SerializerBenchmarks*-github.md")
    )
    if serializer_md:
        output.extend(
            _details_section(
                "Serializers — Dekaf internals", collect_tables(serializer_md)
            )
        )

    compression_md = sorted(
        glob.glob(f"{result_pattern}/Unit/results/*CompressionBenchmarks*-github.md")
    )
    if compression_md:
        output.extend(
            _details_section(
                "Compression — Dekaf internals", collect_tables(compression_md)
            )
        )

    output.extend(
        _details_section(
            "How to read these tables",
            [
                "- **Mean**: Average execution time",
                "- **Error**: Half of 99.9% confidence interval",
                "- **StdDev**: Standard deviation of all measurements",
                "- **Ratio**: Performance relative to that table's baseline row",
                "  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster",
                "  - Dekaf-internals tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent",
                "- **RatioSD**: BenchmarkDotNet's uncertainty for the latest run's ratio",
                f"- **Confidence**: `⚠ Low` when latest `RatioSD > {variance_threshold:.2f}` or rolling run spread exceeds {variance_threshold:.0%}",
                "- **Allocated**: Heap memory allocated per operation",
                "  - `-` = Zero allocations (ideal!)",
                "",
            ],
        )
    )

    output.append("*Benchmarks are automatically run on every push to main.*")

    return "\n".join(output)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--results-dir", required=True)
    parser.add_argument("--history", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--window", type=int, default=DEFAULT_WINDOW)
    parser.add_argument(
        "--variance-threshold", type=float, default=DEFAULT_VARIANCE_THRESHOLD
    )
    parser.add_argument(
        "--updated-at",
        default=datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC"),
    )
    args = parser.parse_args()

    document = generate_document(
        args.results_dir,
        args.history,
        args.updated_at,
        args.window,
        args.variance_threshold,
    )
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(document, encoding="utf-8")
    print(f"Generated {output_path} with {len(document.splitlines())} lines")


if __name__ == "__main__":
    main()
