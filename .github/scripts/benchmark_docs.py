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


def append_tables(output, paths, variance_threshold=None):
    for markdown_path in paths:
        lines = Path(markdown_path).read_text(encoding="utf-8").splitlines()
        if variance_threshold is not None:
            output.extend(annotate_ratio_confidence(lines, variance_threshold))
        else:
            output.extend(line for line in lines if line.startswith("|"))
        output.append("")


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

    output = [
        "---",
        "sidebar_position: 13",
        "---",
        "",
        "# Benchmark Results",
        "",
        "Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.",
        "",
        f"**Last Updated:** {updated_at}",
        "",
        ":::info",
        "These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. ",
        "Ratio semantics differ per table — see 'How to Read These Results' below.",
        ":::",
        "",
        f"## Rolling comparison (last {window} runs)",
        "",
        "Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.",
        "",
        f"Rows with run spread above {variance_threshold:.0%} are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.",
        "",
        *format_rolling_table(comparisons, variance_threshold),
        "",
        "## Latest run",
        "",
        "Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.",
        "",
    ]

    producer_md = sorted(
        glob.glob(f"{result_pattern}/Client/results/*ProducerBenchmarks*-github.md")
    )
    if producer_md:
        output.extend(
            [
                "### Producer Benchmarks",
                "",
                "Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.",
                "",
            ]
        )
        append_tables(output, producer_md, variance_threshold)

    consumer_md = sorted(
        glob.glob(f"{result_pattern}/Client/results/*Consumer*Benchmarks*-github.md")
    )
    if consumer_md:
        output.extend(
            [
                "### Consumer Benchmarks",
                "",
                "Comparing Dekaf vs Confluent.Kafka for message consumption.",
                "",
            ]
        )
        append_tables(output, consumer_md, variance_threshold)

    protocol_md = sorted(
        glob.glob(f"{result_pattern}/Unit/results/*ProtocolBenchmarks*-github.md")
    )
    if protocol_md:
        output.extend(
            [
                "## Protocol Benchmarks",
                "",
                "Zero-allocation wire protocol serialization/deserialization.",
                "",
                ":::tip",
                "**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!",
                ":::",
                "",
            ]
        )
        append_tables(output, protocol_md)

    serializer_md = sorted(
        glob.glob(f"{result_pattern}/Unit/results/*SerializerBenchmarks*-github.md")
    )
    if serializer_md:
        output.extend(["## Serializer Benchmarks", ""])
        append_tables(output, serializer_md)

    compression_md = sorted(
        glob.glob(f"{result_pattern}/Unit/results/*CompressionBenchmarks*-github.md")
    )
    if compression_md:
        output.extend(["## Compression Benchmarks", ""])
        append_tables(output, compression_md)

    output.extend(
        [
            "---",
            "",
            "## How to Read These Results",
            "",
            "- **Mean**: Average execution time",
            "- **Error**: Half of 99.9% confidence interval",
            "- **StdDev**: Standard deviation of all measurements",
            "- **Ratio**: Performance relative to that table's baseline row",
            "  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster",
            "  - Unit tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent",
            "- **RatioSD**: BenchmarkDotNet's uncertainty for the latest run's ratio",
            f"- **Confidence**: `⚠ Low` when latest `RatioSD > {variance_threshold:.2f}` or rolling run spread exceeds {variance_threshold:.0%}",
            "- **Allocated**: Heap memory allocated per operation",
            "  - `-` = Zero allocations (ideal!)",
            "",
            "*Benchmarks are automatically run on every push to main.*",
        ]
    )

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
