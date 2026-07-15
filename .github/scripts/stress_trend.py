"""Noise-aware regression detection for weekly stress-test results."""

import argparse
import json
from math import isfinite
from pathlib import Path
from statistics import median

from stress_report import (
    cpu_micros_per_message,
    effective_rate,
    intra_run_throughput,
    paired_order_identity,
    paired_latency_thresholds,
)


HISTORY_VERSION = 1
HISTORY_LIMIT = 10
MIN_BASELINE_RUNS = 3
MAD_MULTIPLIER = 2.0
RELATIVE_NOISE_FLOOR = 0.01

_IDENTITY_FIELDS = (
    "scenario",
    "client",
    "brokerCount",
    "durationMinutes",
    "messageSizeBytes",
    "consumerSeedBatchSizeBytes",
    "consumerConnectionsPerBroker",
    "roundTripMessages",
    "roundTripSteadySeconds",
    "pairedClientOrder",
    "pairedSampleCount",
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


def _roundtrip_messages(result):
    if result.get("roundTripSteadySeconds") is not None:
        return None
    validation = result.get("roundTripValidation")
    if isinstance(validation, dict):
        return validation.get("expectedMessages", result.get("roundTripMessages"))
    return result.get("roundTripMessages")


def _consumer_seed_batch_size(result):
    value = result.get("consumerSeedBatchSizeBytes")
    if value is not None:
        return value
    scenario = str(result.get("scenario", "")).casefold()
    return 16_384 if scenario.startswith("consumer") else None


def _consumer_connections_per_broker(result):
    value = result.get("consumerConnectionsPerBroker")
    if value is not None:
        return value

    scenario = str(result.get("scenario", "")).casefold()
    client = str(result.get("client", "")).casefold()
    if not scenario.startswith("consumer"):
        return None

    # Results written before the connection count became part of the performance
    # identity used the old two-connection Dekaf preset or Confluent's one connection.
    return 1 if client.startswith("confluent") else 2


def _identity(result):
    return (
        str(result.get("scenario", "unknown")).casefold(),
        str(result.get("client", "unknown")).casefold(),
        result.get("brokerCount", 1),
        result.get("durationMinutes"),
        result.get("messageSizeBytes"),
        _consumer_seed_batch_size(result),
        _consumer_connections_per_broker(result),
        _roundtrip_messages(result),
        result.get("roundTripSteadySeconds"),
        paired_order_identity(result),
    )


def _is_confluent(result):
    return str(result.get("client", "")).casefold().startswith("confluent")


def _pair_identity(result):
    """Match a Dekaf scenario to its same-run Confluent control.

    Client and consumer connection count are intentionally excluded: each client uses its
    own connection preset, while broker count and workload shape define the paired run.
    """
    return (
        str(result.get("scenario", "unknown")).casefold(),
        result.get("brokerCount", 1),
        result.get("durationMinutes"),
        result.get("messageSizeBytes"),
        _consumer_seed_batch_size(result),
        _roundtrip_messages(result),
        result.get("roundTripSteadySeconds"),
        paired_order_identity(result),
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


def _matching_control_ratios(runs, result, metric):
    pair_key = _pair_identity(result)
    candidate_key = _identity(result)
    trend_field = f"{metric}ControlRatioTrend"
    ratios = []

    for run in runs:
        if run.get("environmentShiftSuspected", False):
            continue
        matching_results = [
            item
            for item in run.get("results", [])
            if _pair_identity(item) == pair_key
            and not item.get("environmentShiftSuspected", False)
        ]
        candidate = next(
            (item for item in matching_results if _identity(item) == candidate_key),
            None,
        )
        control = next((item for item in matching_results if _is_confluent(item)), None)
        if candidate is None or control is None:
            continue

        candidate_value = candidate.get(metric)
        control_value = control.get(metric)
        if (
            not _finite_number(candidate_value)
            or not _finite_number(control_value)
            or control_value == 0
            or candidate.get(trend_field) == "regression"
        ):
            continue

        ratios.append(candidate_value / control_value)

    return ratios[-HISTORY_LIMIT:]


def _limit_observations_per_identity(runs):
    """Keep recent observations and clean metric baselines per configuration."""
    seen_counts = {}
    retained_baseline_counts = {}
    retained_runs = []

    for run in reversed(runs):
        retained_results = []
        for observation in reversed(run.get("results", [])):
            key = _identity(observation)
            seen_count = seen_counts.get(key, 0)
            seen_counts[key] = seen_count + 1

            baseline_keys = []
            for metric in _METRICS:
                baseline_key = (key, metric)
                if (
                    _finite_number(observation.get(metric))
                    and observation.get(f"{metric}Trend") != "regression"
                    and not observation.get("environmentShiftSuspected", False)
                    and retained_baseline_counts.get(baseline_key, 0) < HISTORY_LIMIT
                ):
                    baseline_keys.append(baseline_key)

            if seen_count >= HISTORY_LIMIT and not baseline_keys:
                continue

            for baseline_key in baseline_keys:
                retained_baseline_counts[baseline_key] = (
                    retained_baseline_counts.get(baseline_key, 0) + 1
                )
            retained_results.append(observation)

        if retained_results:
            retained_runs.append({
                **run,
                "results": list(reversed(retained_results)),
            })

    return list(reversed(retained_runs))


def _scenario_label(result):
    brokers = result.get("brokerCount", 1)
    label = (
        f"{result.get('scenario', 'unknown')} / {result.get('client', 'unknown')} / "
        f"{brokers} broker{'s' if brokers != 1 else ''} / "
        f"{result.get('messageSizeBytes', '?')}B / {result.get('durationMinutes', '?')}m"
    )
    roundtrip_messages = _roundtrip_messages(result)
    if roundtrip_messages is not None:
        label += f" / {roundtrip_messages} messages"
    return label


def _metric_status(value, baseline, lower_is_regression):
    baseline_median = median(baseline)
    mad = median(abs(item - baseline_median) for item in baseline)
    half_width = max(
        MAD_MULTIPLIER * mad,
        abs(baseline_median) * RELATIVE_NOISE_FLOOR,
    )
    lower = baseline_median - half_width
    upper = baseline_median + half_width

    if lower <= value <= upper:
        status = "stable"
    elif value < lower:
        status = "regression" if lower_is_regression else "improvement"
    else:
        status = "improvement" if lower_is_regression else "regression"

    return status, baseline_median, mad, lower, upper


def _control_ratio_evaluation(baseline_runs, result, metric, definition, value, control_value):
    ratio = value / control_value
    ratio_history = _matching_control_ratios(baseline_runs, result, metric)
    evaluation = {
        "scenario": _scenario_label(result),
        "metric": f"{metric}ControlRatio",
        "metricLabel": f"{definition['label']} / Confluent",
        "current": ratio,
        "baselineCount": len(ratio_history),
        "median": None,
        "mad": None,
        "lower": None,
        "upper": None,
        "status": "insufficient-history",
        "repeatedRegression": False,
        "failureEligible": False,
        "corroboratesBaselineRegression": False,
    }
    if len(ratio_history) < MIN_BASELINE_RUNS:
        return evaluation

    status, center, mad, lower, upper = _metric_status(
        ratio,
        ratio_history,
        definition["lower_is_regression"],
    )
    evaluation.update({
        "median": center,
        "mad": mad,
        "lower": lower,
        "upper": upper,
        "status": status,
    })
    return evaluation


def evaluate_and_update(history, current_results, run_started_at):
    """Evaluate current results, append one compact run, and return failure state."""
    if history is not None and history.get("version") != HISTORY_VERSION:
        raise ValueError(f"Unsupported stress history version: {history.get('version')}")

    runs = history.get("runs", []) if history is not None else []
    if not isinstance(runs, list):
        raise ValueError("Stress history 'runs' must be a list")

    # A re-run of the same result set replaces its prior entry and never uses itself
    # as baseline data.
    baseline_runs = [run for run in runs if run.get("runStartedAtUtc") != run_started_at]
    evaluations = []
    observations = []
    should_fail = False
    environment_shift_suspected = False
    controls = {
        _pair_identity(result): result
        for result in current_results
        if _is_confluent(result)
    }

    for result in current_results:
        is_confluent = _is_confluent(result)
        prior = _matching_observations(baseline_runs, result)
        observation = {field: result.get(field) for field in _IDENTITY_FIELDS}
        observation["consumerSeedBatchSizeBytes"] = _consumer_seed_batch_size(result)
        observation["consumerConnectionsPerBroker"] = _consumer_connections_per_broker(result)
        observation["brokerCount"] = result.get("brokerCount", 1)
        observation["roundTripMessages"] = _roundtrip_messages(result)
        throughput_regression = False

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
                and not item.get("environmentShiftSuspected", False)
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
                "failureEligible": False,
                "corroborated": None,
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

            observation[metric] = value
            observation[f"{metric}Trend"] = evaluation["status"]

            ratio_evaluation = None
            control = controls.get(_pair_identity(result)) if not is_confluent else None
            control_value = definition["extract"](control) if control is not None else None
            if _finite_number(control_value) and control_value != 0:
                ratio_evaluation = _control_ratio_evaluation(
                    baseline_runs,
                    result,
                    metric,
                    definition,
                    value,
                    control_value,
                )
                ratio_metric = ratio_evaluation["metric"]
                observation[ratio_metric] = ratio_evaluation["current"]
                observation[f"{ratio_metric}Trend"] = ratio_evaluation["status"]
                evaluation["corroborated"] = ratio_evaluation["status"] == "regression"
                evaluation["failureEligible"] = (
                    evaluation["repeatedRegression"] and evaluation["corroborated"]
                )
                ratio_evaluation["corroboratesBaselineRegression"] = evaluation[
                    "failureEligible"
                ]
            elif not is_confluent:
                evaluation["failureEligible"] = evaluation["repeatedRegression"]

            if is_confluent and evaluation["status"] == "regression":
                environment_shift_suspected = True
                evaluation["environmentShiftSuspected"] = True

            evaluations.append(evaluation)
            if ratio_evaluation is not None:
                evaluations.append(ratio_evaluation)
            should_fail = should_fail or evaluation["failureEligible"]
            if metric == "messagesPerSecond":
                throughput_regression = evaluation["status"] == "regression"

        intra_run = intra_run_throughput(result)
        if intra_run is not None:
            slope_breached = intra_run["slopeThresholdBreached"]
            threshold_metrics = (
                (
                    "steadyStatePeakRatio",
                    "Steady-state / peak",
                    intra_run["steadyStatePeakRatio"],
                    intra_run["steadyStatePeakRatioThreshold"],
                    intra_run["steadyStatePeakThresholdBreached"],
                ),
                (
                    "slopePercentPerMinute",
                    "Slope %/min",
                    intra_run["slopePercentPerMinute"],
                    intra_run["slopePercentPerMinuteThreshold"],
                    intra_run["slopeThresholdBreached"],
                ),
            )
            for metric, label, value, threshold, breached in threshold_metrics:
                trend_field = f"{metric}Trend"
                previous = prior[-1] if prior else {}
                previous_trend = previous.get(trend_field)
                previous_value = previous.get(metric)
                if (
                    previous_trend is None
                    and isinstance(previous_value, (int, float))
                    and isfinite(previous_value)
                ):
                    previous_trend = (
                        "regression" if previous_value < threshold else "stable"
                    )
                status = "regression" if breached else "stable"
                repeated = status == "regression" and previous_trend == "regression"
                corroborated = (
                    breached
                    if metric == "slopePercentPerMinute"
                    else slope_breached or throughput_regression
                )
                failure_eligible = (
                    breached and repeated and corroborated and not is_confluent
                )
                evaluations.append({
                    "scenario": _scenario_label(result),
                    "metric": metric,
                    "metricLabel": label,
                    "current": value,
                    "baselineCount": 0,
                    "median": None,
                    "mad": None,
                    "lower": threshold,
                    "upper": None,
                    "status": status,
                    "repeatedRegression": repeated,
                    "thresholdBreach": breached,
                    "corroborated": corroborated,
                    "failureEligible": failure_eligible,
                })
                observation[metric] = value
                observation[trend_field] = status
                should_fail = should_fail or failure_eligible

        observations.append(observation)

    # Environment shifts model runner-wide noise: one regressed Confluent control makes
    # every observation from the same run unsafe for future absolute baselines.
    if environment_shift_suspected:
        for observation in observations:
            observation["environmentShiftSuspected"] = True

    latency_evaluations = paired_latency_thresholds(current_results)
    evaluations.extend(latency_evaluations)
    should_fail = should_fail or any(
        item['thresholdBreach'] for item in latency_evaluations
    )

    current_run = {
        "runStartedAtUtc": run_started_at,
        "results": observations,
    }
    if environment_shift_suspected:
        current_run["environmentShiftSuspected"] = True
    updated_runs = baseline_runs + [current_run]
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
            f"Current metrics and paired Dekaf/Confluent ratios are compared with up to "
            f"{HISTORY_LIMIT} matching runs. "
            f"The noise band is trailing median ± max({MAD_MULTIPLIER:g}×MAD, "
            f"{RELATIVE_NOISE_FLOOR:.0%} of median); "
            "a second consecutive adverse excursion becomes a failure candidate."
        ),
        (
            "Paired Dekaf baseline regressions fail only when the same-run ratio also "
            "regresses; unpaired scenarios retain the consecutive-regression gate. "
            "Confluent regressions remain environment warnings."
        ),
    ]

    if any(item.get("environmentShiftSuspected") for item in evaluations):
        lines.extend([
            "",
            "> Environment shift suspected: Confluent control regressed beyond its trailing band; "
            "this run is excluded from absolute baselines.",
        ])

    lines.extend([
        "",
        "| Scenario | Metric | Current | Baseline median | Band | Status |",
        "|----------|--------|--------:|----------------:|------|--------|",
    ])

    labels = {
        "insufficient-history": "Collecting baseline",
        "stable": "Within band",
        "improvement": "Improvement",
        "regression": "Regression",
    }

    for item in evaluations:
        if "thresholdBreach" in item:
            center = "-"
            if item.get("thresholdDirection") == "maximum":
                band = f"<= {item['upper']:,.2f}"
            else:
                band = f">= {item['lower']:,.2f}"
        elif item["median"] is None:
            center = "-"
            band = f"{item['baselineCount']}/{MIN_BASELINE_RUNS} runs"
        else:
            center = f"{item['median']:,.2f}"
            band = f"{item['lower']:,.2f} – {item['upper']:,.2f}"

        status = labels[item["status"]]
        if item.get("thresholdBreach"):
            if item.get("latencyThreshold"):
                status = "Threshold breach (fail)"
            elif item.get("failureEligible"):
                status = "Repeated threshold breach (fail)"
            elif item.get("corroborated") is False:
                status = "Threshold breach (uncorroborated warning)"
            else:
                status = "Threshold breach (warning)"
        elif item.get("environmentShiftSuspected"):
            status = "Environment shift suspected (warning)"
        elif item["repeatedRegression"]:
            if item.get("failureEligible"):
                status = "Repeated regression (fail)"
            elif item.get("corroborated") is False:
                status = "Repeated regression (control-normalized warning)"
            else:
                status = "Repeated regression (warning)"
        elif item.get("corroboratesBaselineRegression"):
            status = "Regression (corroborates fail)"
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

        if item.get("thresholdBreach"):
            level = "error" if item.get("failureEligible", True) else "warning"
            prefix = (
                "Latency threshold breach"
                if item.get("latencyThreshold")
                else "Intra-run threshold breach"
            )
        elif item.get("environmentShiftSuspected"):
            level = "warning"
            prefix = "Environment shift suspected"
        elif item["repeatedRegression"]:
            level = "error" if item.get("failureEligible") else "warning"
            prefix = (
                "Repeated regression"
                if item.get("failureEligible")
                else "Control-normalized regression warning"
            )
        elif item.get("corroboratesBaselineRegression"):
            level = "error"
            prefix = "Control ratio regression"
        elif item["status"] == "regression":
            level = "warning"
            prefix = "Regression"
        else:
            level = "notice"
            prefix = "Improvement"

        if "thresholdBreach" in item:
            if item.get("thresholdDirection") == "maximum":
                requirement = f"required <= {item['upper']:.2f}"
            else:
                requirement = f"required >= {item['lower']:.2f}"
            message = (
                f"{prefix}: {item['scenario']} {item['metricLabel']}={item['current']:.2f}; "
                f"{requirement}"
            )
        else:
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
    parser.add_argument(
        "--github-output",
        help="GitHub Actions output file; reports repeated regressions via should_fail",
    )
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

    if args.github_output:
        with Path(args.github_output).open("a", encoding="utf-8", newline="\n") as file:
            file.write(f"should_fail={str(should_fail).lower()}\n")
        return 0

    return 1 if should_fail else 0


if __name__ == "__main__":
    raise SystemExit(main())
