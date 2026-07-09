"""Noise-aware regression detection for weekly stress-test results."""

import argparse
import json
from math import isfinite
from pathlib import Path
from statistics import median

from stress_report import cpu_micros_per_message, effective_rate


HISTORY_VERSION = 1
HISTORY_LIMIT = 10
MIN_BASELINE_RUNS = 3
MAD_MULTIPLIER = 2.0

_IDENTITY_FIELDS = (
    "scenario",
    "client",
    "brokerCount",
    "durationMinutes",
    "messageSizeBytes",
)

_METRICS = {
    "messagesPerSecond": {
        "label": "Messages/sec",
        "extract": effective_rate,
        "lower_is_regression": True,
    },
    "cpuMicrosPerMessage": {
        "label": "CPU μs/msg",
        "extract": cpu_micros_per_message,
        "lower_is_regression": False,
    },
}


def _finite_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and isfinite(value)


def _identity(result):
    return (
        str(result.get("scenario", "unknown")).casefold(),
        str(result.get("client", "unknown")).casefold(),
        result.get("brokerCount", 1),
        result.get("durationMinutes"),
        result.get("messageSizeBytes"),
    )


def _matching_observations(runs, result):
    key = _identity(result)
    matches = []
    for run in runs:
        observation = next(
            (item for item in run.get("results", []) if _identity(item) == key),
            None,
        )
        if observation is not None:
            matches.append(observation)
    return matches


def _limit_observations_per_identity(runs):
    """Keep the newest observations for every stress configuration."""
    retained_counts = {}
    retained_runs = []

    for run in reversed(runs):
        retained_results = []
        for observation in reversed(run.get("results", [])):
            key = _identity(observation)
            retained_count = retained_counts.get(key, 0)
            if retained_count >= HISTORY_LIMIT:
                continue

            retained_counts[key] = retained_count + 1
            retained_results.append(observation)

        if retained_results:
            retained_runs.append({
                **run,
                "results": list(reversed(retained_results)),
            })

    return list(reversed(retained_runs))


def _scenario_label(result):
    brokers = result.get("brokerCount", 1)
    return (
        f"{result.get('scenario', 'unknown')} / {result.get('client', 'unknown')} / "
        f"{brokers} broker{'s' if brokers != 1 else ''} / "
        f"{result.get('messageSizeBytes', '?')}B / {result.get('durationMinutes', '?')}m"
    )


def _metric_status(value, baseline, lower_is_regression):
    baseline_median = median(baseline)
    mad = median(abs(item - baseline_median) for item in baseline)
    half_width = MAD_MULTIPLIER * mad
    lower = baseline_median - half_width
    upper = baseline_median + half_width

    if lower <= value <= upper:
        status = "stable"
    elif value < lower:
        status = "regression" if lower_is_regression else "improvement"
    else:
        status = "improvement" if lower_is_regression else "regression"

    return status, baseline_median, mad, lower, upper


def evaluate_and_update(history, current_results, run_started_at):
    """Evaluate current results, append one compact run, and return failure state."""
    if history and history.get("version") != HISTORY_VERSION:
        raise ValueError(f"Unsupported stress history version: {history.get('version')}")

    runs = history.get("runs", []) if history else []
    if not isinstance(runs, list):
        raise ValueError("Stress history 'runs' must be a list")

    # A re-run of the same result set replaces its prior entry and never uses itself
    # as baseline data.
    baseline_runs = [run for run in runs if run.get("runStartedAtUtc") != run_started_at]
    evaluations = []
    observations = []
    should_fail = False

    for result in current_results:
        prior = _matching_observations(baseline_runs, result)
        observation = {field: result.get(field) for field in _IDENTITY_FIELDS}
        observation["brokerCount"] = result.get("brokerCount", 1)

        for metric, definition in _METRICS.items():
            value = definition["extract"](result)
            if not _finite_number(value):
                continue

            trend_field = f"{metric}Trend"
            previous_trend = prior[-1].get(trend_field) if prior else None
            # Keep warned observations for consecutive-trend detection, but do not let
            # them widen the clean baseline used to judge the next run.
            history_values = [
                item.get(metric)
                for item in prior
                if item.get(trend_field) != "regression"
            ]
            history_values = [
                item for item in history_values if _finite_number(item)
            ][-HISTORY_LIMIT:]

            evaluation = {
                "scenario": _scenario_label(result),
                "metric": metric,
                "metricLabel": definition["label"],
                "current": value,
                "baselineCount": len(history_values),
                "median": None,
                "mad": None,
                "lower": None,
                "upper": None,
                "status": "insufficient-history",
                "repeatedRegression": False,
            }

            if len(history_values) >= MIN_BASELINE_RUNS:
                status, center, mad, lower, upper = _metric_status(
                    value,
                    history_values,
                    definition["lower_is_regression"],
                )
                repeated = status == "regression" and previous_trend == "regression"
                evaluation.update({
                    "median": center,
                    "mad": mad,
                    "lower": lower,
                    "upper": upper,
                    "status": status,
                    "repeatedRegression": repeated,
                })
                should_fail = should_fail or repeated

            observation[metric] = value
            observation[f"{metric}Trend"] = evaluation["status"]
            evaluations.append(evaluation)

        observations.append(observation)

    updated_runs = baseline_runs + [{
        "runStartedAtUtc": run_started_at,
        "results": observations,
    }]
    updated = {
        "version": HISTORY_VERSION,
        "runs": _limit_observations_per_identity(updated_runs),
    }
    return evaluations, updated, should_fail


def format_markdown(evaluations):
    lines = [
        "## Stress Trend Analysis",
        "",
        (
            f"Current metrics are compared with up to {HISTORY_LIMIT} matching runs. "
            f"The noise band is trailing median ± {MAD_MULTIPLIER:g}×MAD; "
            "a second consecutive adverse excursion fails the job."
        ),
        "",
        "| Scenario | Metric | Current | Baseline median | Band | Status |",
        "|----------|--------|--------:|----------------:|------|--------|",
    ]

    labels = {
        "insufficient-history": "Collecting baseline",
        "stable": "Within band",
        "improvement": "Improvement",
        "regression": "Regression",
    }

    for item in evaluations:
        if item["median"] is None:
            center = "-"
            band = f"{item['baselineCount']}/{MIN_BASELINE_RUNS} runs"
        else:
            center = f"{item['median']:,.2f}"
            band = f"{item['lower']:,.2f} – {item['upper']:,.2f}"

        status = labels[item["status"]]
        if item["repeatedRegression"]:
            status = "Repeated regression (fail)"
        elif item["status"] == "regression":
            status = "Regression (warning)"

        lines.append(
            f"| {item['scenario']} | {item['metricLabel']} | {item['current']:,.2f} | "
            f"{center} | {band} | {status} |"
        )

    lines.append("")
    return "\n".join(lines)


def _annotation_escape(value):
    return str(value).replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def emit_annotations(evaluations):
    for item in evaluations:
        if item["status"] not in {"regression", "improvement"}:
            continue

        if item["repeatedRegression"]:
            level = "error"
            prefix = "Repeated regression"
        elif item["status"] == "regression":
            level = "warning"
            prefix = "Regression"
        else:
            level = "notice"
            prefix = "Improvement"

        message = (
            f"{prefix}: {item['scenario']} {item['metricLabel']}={item['current']:.2f}; "
            f"baseline {item['median']:.2f}, band {item['lower']:.2f}-{item['upper']:.2f}"
        )
        print(f"::{level} title=Stress performance trend::{_annotation_escape(message)}")


def _load_json(path):
    with Path(path).open(encoding="utf-8") as file:
        return json.load(file)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", required=True, help="Merged stress-test result JSON")
    parser.add_argument("--history", required=True, help="Committed compact history JSON")
    parser.add_argument("--output", required=True, help="Updated history JSON")
    parser.add_argument("--summary", help="GitHub step-summary file to append")
    args = parser.parse_args(argv)

    result_document = _load_json(args.results)
    current_results = result_document.get("results")
    run_started_at = result_document.get("runStartedAtUtc")
    if not isinstance(current_results, list) or not run_started_at:
        raise ValueError("Merged results require runStartedAtUtc and a results list")

    history_path = Path(args.history)
    history = _load_json(history_path) if history_path.exists() else {"version": 1, "runs": []}
    evaluations, updated, should_fail = evaluate_and_update(history, current_results, run_started_at)

    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8", newline="\n") as file:
        json.dump(updated, file, indent=2)
        file.write("\n")

    report = format_markdown(evaluations)
    print(report)
    emit_annotations(evaluations)

    if args.summary:
        with Path(args.summary).open("a", encoding="utf-8", newline="\n") as file:
            file.write("\n" + report + "\n")

    return 1 if should_fail else 0


if __name__ == "__main__":
    raise SystemExit(main())
