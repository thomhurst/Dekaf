"""Screen aggregate stress metrics against bracketing exact-SHA baseline runs."""

import argparse
import json
from dataclasses import dataclass
from math import isfinite
from pathlib import Path

from stress_report import cpu_micros_per_message, effective_rate, median_interval_rate


DEFAULT_TOLERANCE_PERCENT = 3.0
DEFAULT_MAX_CONTROL_DRIFT_PERCENT = 10.0
# Conservative aggregate-screen floor: 100 observations in the upper 1% by rank.
# This does not establish independent samples, p99 precision, or complete coverage.
MIN_LATENCY_SAMPLES = 10_000


@dataclass(frozen=True)
class Metric:
    key: str
    label: str
    unit: str
    higher_is_better: bool
    gated: bool = True
    floor: bool = False
    # Absolute spread below which differences in this metric's unit are treated as noise in
    # both directions: neither control drift nor a candidate delta smaller than this can
    # decide the metric. Percentage gates still apply above it.
    noise_floor: float = 0.0


# Steady/peak is a shape statistic: it normalizes a run's settled throughput by that same
# run's best interval. Comparing it A-vs-B penalizes a candidate whose adaptive window
# intentionally front-loads throughput while it settles — run 29518014693's candidate beat
# both controls on delivered throughput yet "failed" stability solely because its own early
# peak was higher — and its control spread has been observed near 5%, wider than the adverse
# tolerance. It is therefore gated against the harness's own absolute steady-state floor,
# which still rejects genuine intra-run collapse (run 29513570135's 0.745 breaches it) and
# marks the comparison inconclusive when a control itself is unstable.
METRICS = (
    Metric("throughput", "Delivered throughput", "msg/s", True),
    Metric("medianThroughput", "Median interval throughput", "msg/s", True),
    Metric("p50", "Latency p50", "ms", False),
    Metric("p95", "Latency p95", "ms", False),
    Metric("p99", "Latency p99", "ms", False),
    Metric("cpu", "CPU", "us/msg", False),
    # Producer hot paths allocate 0 B/msg by design; the residual 0.5-0.9 B/msg is cold-path
    # activity (metadata refreshes, probes, samplers) whose run-to-run spread is 0.1-0.25
    # B/msg, i.e. 16-33% relative. Runs 33961873612 and 33966278396 were ruled INCONCLUSIVE
    # on that spread alone while every other row passed. Sub-byte allocation differences are
    # not evidence in either direction, so the allocation gate carries a 1 B/msg noise floor;
    # consumer lanes at ~2 KB/msg are unaffected because their 3% tolerance is ~60 B/msg.
    Metric("alloc", "Allocation", "B/msg", False, noise_floor=1.0),
    Metric("stability", "Steady/peak ratio", "ratio", True, floor=True),
    Metric("max", "Latency max", "ms", False),
    Metric("averageRequest", "Average request", "KiB", True, gated=False),
)

DEFAULT_STABILITY_FLOOR = 0.85


def _finite_number(value):
    return (
        isinstance(value, (int, float))
        and not isinstance(value, bool)
        and isfinite(value)
    )


def _single_result(directory):
    files = sorted(Path(directory).rglob("stress-test-results*.json"))
    if len(files) != 1:
        raise ValueError(
            f"Expected exactly one stress result under {directory}, found {len(files)}"
        )

    with files[0].open(encoding="utf-8") as handle:
        envelope = json.load(handle)
    results = envelope.get("results") if isinstance(envelope, dict) else None
    if not isinstance(results, list) or len(results) != 1:
        raise ValueError(f"Expected exactly one result in {files[0]}")
    if not isinstance(results[0], dict):
        raise ValueError(f"Expected a result object in {files[0]}")
    return results[0]


def _identity(result):
    return (
        str(result.get("scenario", "")).casefold(),
        str(result.get("client", "")).casefold(),
        result.get("brokerCount"),
        result.get("durationMinutes"),
        result.get("messageSizeBytes"),
        result.get("deliveryLatencyTargetMs"),
        result.get("idempotent"),
        result.get("roundTripSteadySeconds"),
    )


def _average_request_kib(result):
    diagnostics = result.get("producerDeliveryDiagnostics") or {}
    brokers = diagnostics.get("brokerProduceRequests") or []
    request_count = sum(item.get("requestCount", 0) or 0 for item in brokers)
    if request_count <= 0:
        return None
    total_bytes = sum(
        (item.get("requestCount", 0) or 0)
        * (item.get("averageRequestBytes", 0) or 0)
        for item in brokers
    )
    return total_bytes / request_count / 1024


def _latency_ms(latency, key):
    value = latency.get(key)
    return value / 1000 if _finite_number(value) else None


def _measurements(result):
    latency = result.get("latency") or {}
    if not isinstance(latency, dict):
        raise ValueError("Expected a latency object")
    measurements = {
        "throughput": effective_rate(result),
        "medianThroughput": median_interval_rate(result),
        "p50": _latency_ms(latency, "p50Us"),
        "p95": _latency_ms(latency, "p95Us"),
        "p99": _latency_ms(latency, "p99Us"),
        "max": _latency_ms(latency, "maxUs"),
        "cpu": cpu_micros_per_message(result),
        "alloc": result.get("allocatedBytesPerMessage"),
        "stability": result.get("steadyStatePeakRatio"),
        "averageRequest": _average_request_kib(result),
    }
    missing = [
        metric.label
        for metric in METRICS
        if not _finite_number(measurements.get(metric.key)) or measurements[metric.key] < 0
    ]
    if missing:
        raise ValueError(f"Missing finite nonnegative metric(s): {', '.join(missing)}")
    if measurements["cpu"] <= 0:
        raise ValueError("CPU evidence requires positive CPU time per completed message")
    if any(measurements[key] <= 0 for key in ("p50", "p95", "p99", "max")):
        raise ValueError("Latency evidence requires positive latency quantiles and maximum")
    count = latency.get("count")
    if not isinstance(count, int) or isinstance(count, bool) or count <= 0:
        raise ValueError("Latency evidence requires a positive integer sample count")
    if count < MIN_LATENCY_SAMPLES:
        raise ValueError(f"Aggregate screening requires at least {MIN_LATENCY_SAMPLES} latency samples per segment")
    return measurements


def _errors(result):
    throughput = result.get("throughput") or {}
    return (throughput.get("totalErrors", 0) or 0) + (
        throughput.get("totalDeliveryErrors", 0) or 0
    )


def _delivery_mismatch(result):
    delivered = result.get("deliveredMessages")
    accepted = (result.get("throughput") or {}).get("totalMessages")
    return delivered is not None and accepted is not None and delivered != accepted


def _stability_breached(result):
    breached = result.get("steadyStatePeakThresholdBreached")
    if isinstance(breached, bool):
        return breached

    ratio = result.get("steadyStatePeakRatio")
    threshold = result.get("steadyStatePeakRatioThreshold")
    if not _finite_number(threshold):
        threshold = DEFAULT_STABILITY_FLOOR
    return _finite_number(ratio) and ratio < threshold


def _percent_change(value, baseline):
    # A nonzero value has no finite percentage change from zero. Keep it null in JSON
    # and display n/a in Markdown; the gate uses absolute values, not this percentage.
    if baseline == 0:
        return 0.0 if value == 0 else None
    return 100 * (value - baseline) / baseline


def _validate_control_rates(measurements):
    if measurements["throughput"] <= 0 or measurements["medianThroughput"] <= 0:
        raise ValueError("A baseline control requires positive delivered and median throughput")


def _validate_completed_messages(result):
    completed = result.get("deliveredMessages")
    if completed is None:
        completed = (result.get("throughput") or {}).get("totalMessages")
    if not isinstance(completed, int) or isinstance(completed, bool) or completed <= 0:
        raise ValueError("A comparison segment requires positive integer completed messages")


def _drift_percent(first, second):
    mean = (first + second) / 2
    return 0.0 if mean == 0 else 100 * abs(second - first) / mean


def _is_adverse(metric, candidate, control, tolerance_percent):
    loss = control - candidate if metric.higher_is_better else candidate - control
    return loss > metric.noise_floor and loss > control * tolerance_percent / 100


def _metric_status(
    metric,
    baseline_a,
    candidate,
    baseline_a2,
    tolerance_percent,
    max_control_drift_percent,
    floor_status=None,
    candidate_b2=None,
):
    baseline_mean = (baseline_a + baseline_a2) / 2
    candidate_samples = [candidate] if candidate_b2 is None else [candidate, candidate_b2]
    candidate_mean = sum(candidate_samples) / len(candidate_samples)
    candidate_drift_percent = (
        0.0 if candidate_b2 is None else _drift_percent(candidate, candidate_b2)
    )
    control_drift_percent = _drift_percent(baseline_a, baseline_a2)
    control_spread = abs(baseline_a2 - baseline_a)
    noise_floor = metric.noise_floor
    if not metric.gated:
        status = "recorded"
    elif metric.floor:
        status = floor_status
    else:
        # Means are descriptive only. Every candidate segment must meet the tolerance
        # against each control; averaging cannot conceal a loss or conflicting samples.
        adverse_pairs = [
            _is_adverse(metric, sample, control, tolerance_percent)
            for sample in candidate_samples
            for control in (baseline_a, baseline_a2)
        ]
        controls_disagree = (
            control_drift_percent > max_control_drift_percent
            and control_spread > noise_floor
        )
        candidates_disagree = (
            candidate_drift_percent > max_control_drift_percent
            and abs(candidate_samples[-1] - candidate_samples[0]) > noise_floor
        )

        # A loss beyond tolerance in every pairing remains grounds to reject. Otherwise
        # drift or conflicting pairings cannot establish either acceptance or regression.
        if all(adverse_pairs):
            status = "regression"
        elif controls_disagree or candidates_disagree or any(adverse_pairs):
            status = "inconclusive"
        else:
            status = "pass"

    return {
        "key": metric.key,
        "label": metric.label,
        "unit": metric.unit,
        "baselineA": baseline_a,
        "candidate": candidate_mean,
        "candidateB": candidate_samples[0],
        "candidateB2": candidate_b2,
        "candidateDriftPercent": candidate_drift_percent,
        "baselineA2": baseline_a2,
        "baselineMean": baseline_mean,
        "deltaPercent": _percent_change(candidate_mean, baseline_mean),
        "deltaVsBaselineAPercent": _percent_change(candidate, baseline_a),
        "deltaVsBaselineA2Percent": _percent_change(candidate, baseline_a2),
        "b2DeltaVsBaselineAPercent": (
            None if candidate_b2 is None else _percent_change(candidate_b2, baseline_a)
        ),
        "b2DeltaVsBaselineA2Percent": (
            None if candidate_b2 is None else _percent_change(candidate_b2, baseline_a2)
        ),
        "controlDriftPercent": control_drift_percent,
        "noiseFloor": noise_floor,
        "status": status,
        "gated": metric.gated,
    }


def compare(
    baseline_a_result,
    candidate_result,
    baseline_a2_result,
    tolerance_percent=DEFAULT_TOLERANCE_PERCENT,
    max_control_drift_percent=DEFAULT_MAX_CONTROL_DRIFT_PERCENT,
    candidate_b2_result=None,
):
    if any(not _finite_number(value) or value < 0
           for value in (tolerance_percent, max_control_drift_percent)):
        raise ValueError("Comparison thresholds must be finite numbers and cannot be negative")

    results = [baseline_a_result, candidate_result, baseline_a2_result]
    if candidate_b2_result is not None:
        results.append(candidate_b2_result)
    for result in results:
        for field in ("throughput", "producerDeliveryDiagnostics"):
            if not isinstance(result.get(field), dict):
                raise ValueError(f"Expected a {field} object")
        _validate_completed_messages(result)

    identities = {
        _identity(baseline_a_result),
        _identity(candidate_result),
        _identity(baseline_a2_result),
    }
    if candidate_b2_result is not None:
        identities.add(_identity(candidate_b2_result))
    if len(identities) != 1:
        raise ValueError("A-B-A results do not have the same workload identity")
    if _errors(baseline_a_result) or _errors(baseline_a2_result):
        raise ValueError("A baseline control contains producer or delivery errors")
    if _delivery_mismatch(baseline_a_result) or _delivery_mismatch(baseline_a2_result):
        raise ValueError("A baseline control did not deliver every accepted message")

    baseline_a = _measurements(baseline_a_result)
    candidate = _measurements(candidate_result)
    baseline_a2 = _measurements(baseline_a2_result)
    candidate_b2 = (
        None if candidate_b2_result is None else _measurements(candidate_b2_result)
    )
    _validate_control_rates(baseline_a)
    _validate_control_rates(baseline_a2)
    if _stability_breached(candidate_result) or (
        candidate_b2_result is not None and _stability_breached(candidate_b2_result)
    ):
        stability_status = "regression"
    elif _stability_breached(baseline_a_result) or _stability_breached(baseline_a2_result):
        stability_status = "inconclusive"
    else:
        stability_status = "pass"
    metrics = [
        _metric_status(
            metric,
            baseline_a[metric.key],
            candidate[metric.key],
            baseline_a2[metric.key],
            tolerance_percent,
            max_control_drift_percent,
            floor_status=stability_status if metric.floor else None,
            candidate_b2=None if candidate_b2 is None else candidate_b2[metric.key],
        )
        for metric in METRICS
    ]

    candidate_failed = _errors(candidate_result) > 0 or _delivery_mismatch(candidate_result)
    if candidate_b2_result is not None:
        candidate_failed = candidate_failed or (
            _errors(candidate_b2_result) > 0 or _delivery_mismatch(candidate_b2_result)
        )
    statuses = {item["status"] for item in metrics if item["gated"]}
    if candidate_failed or "regression" in statuses:
        verdict = "regression"
    elif "inconclusive" in statuses:
        verdict = "inconclusive"
    else:
        verdict = "pass"

    return {
        "verdict": verdict,
        "tolerancePercent": tolerance_percent,
        "maxControlDriftPercent": max_control_drift_percent,
        "candidateErrors": _errors(candidate_result)
        + (0 if candidate_b2_result is None else _errors(candidate_b2_result)),
        "candidateDeliveryMismatch": _delivery_mismatch(candidate_result)
        or (candidate_b2_result is not None and _delivery_mismatch(candidate_b2_result)),
        "candidateSegments": 1 if candidate_b2_result is None else 2,
        "minimumLatencySamples": MIN_LATENCY_SAMPLES,
        "latencySampleCounts": {
            "baselineA": baseline_a_result["latency"]["count"],
            "candidateB": candidate_result["latency"]["count"],
            "baselineA2": baseline_a2_result["latency"]["count"],
            "candidateB2": None if candidate_b2_result is None else candidate_b2_result["latency"]["count"],
        },
        "identity": list(_identity(candidate_result)),
        "metrics": metrics,
    }


def _format_number(value):
    if abs(value) >= 1000:
        return f"{value:,.0f}"
    return f"{value:.3f}"


def _format_percent(value):
    return "n/a" if value is None else f"{value:+.2f}%"


def markdown(comparison, baseline_sha, candidate_sha):
    verdict = comparison["verdict"].upper()
    lines = [
        "## Exact-SHA stress A-B-A comparison",
        "",
        f"**Verdict: {verdict}**",
        "",
        "This is an aggregate metric screen, not full PR performance acceptance. "
        "Runner/fixture identity, warmup and runtime activity, sampling uncertainty, "
        "hot-path MemoryDiagnoser evidence and sustained stability require separate validation.",
        f"Each segment requires at least {MIN_LATENCY_SAMPLES} latency samples. This conservative "
        "screening floor does not establish tail precision or comparable sampling coverage; "
        "those still require the workload's sampling design and uncertainty analysis.",
        "",
        f"Baseline: `{baseline_sha}` · Candidate: `{candidate_sha}`",
    ]
    if comparison.get("validationError"):
        lines.extend(["", f"Evidence validation failed: {comparison['validationError']}"])
        return "\n".join(lines) + "\n"
    lines.extend([
        "",
        f"adverse tolerance: {comparison['tolerancePercent']:.1f}% · "
        f"maximum control drift: {comparison['maxControlDriftPercent']:.1f}% · "
        "allocation noise floor: 1.0 B/msg",
    ])
    two_candidates = comparison.get("candidateSegments", 1) == 2
    sample_counts = comparison["latencySampleCounts"]
    labels = "A / B / A2"
    keys = ["baselineA", "candidateB", "baselineA2"]
    if two_candidates:
        lines[-1] += " · four segments (A-B-A-B): each candidate is gated separately"
        labels += " / B2"
        keys.append("candidateB2")
    lines.extend(["", f"Latency samples ({labels}): " + " / ".join(str(sample_counts[key]) for key in keys)])
    if two_candidates:
        lines.extend(
            [
                "",
                "| Metric | Baseline A | Candidate B | Baseline A2 | Candidate B2 | B vs A | B vs A2 | B2 vs A | B2 vs A2 | Control drift | Candidate drift | Gate |",
                "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|",
            ]
        )
    else:
        lines.extend(
            [
                "",
                "| Metric | Baseline A | Candidate B | Baseline A2 | B vs A | B vs A2 | Control drift | Gate |",
                "|---|---:|---:|---:|---:|---:|---:|---|",
            ]
        )
    for item in comparison["metrics"]:
        cells = [
            f"{item['label']} ({item['unit']})",
            _format_number(item["baselineA"]),
            _format_number(item["candidateB"]),
            _format_number(item["baselineA2"]),
        ]
        if two_candidates:
            cells.append(_format_number(item["candidateB2"]))
        cells.extend([
            _format_percent(item["deltaVsBaselineAPercent"]),
            _format_percent(item["deltaVsBaselineA2Percent"]),
        ])
        if two_candidates:
            cells.extend([
                _format_percent(item["b2DeltaVsBaselineAPercent"]),
                _format_percent(item["b2DeltaVsBaselineA2Percent"]),
            ])
        cells.append(f"{item['controlDriftPercent']:.2f}%")
        if two_candidates:
            cells.append(f"{item['candidateDriftPercent']:.2f}%")
        cells.append(item["status"])
        lines.append("| " + " | ".join(cells) + " |")
    if comparison["candidateErrors"] or comparison["candidateDeliveryMismatch"]:
        lines.extend(
            [
                "",
                f"Candidate errors: {comparison['candidateErrors']}; "
                f"delivery mismatch: {comparison['candidateDeliveryMismatch']}.",
            ]
        )
    lines.extend(
        [
            "",
            "Percent changes from a zero control are n/a unless both values are zero. "
            "Means in JSON are descriptive only; they do not decide the gate.",
            "",
            "REGRESSION rejects the measured candidate; INCONCLUSIVE does not establish acceptance. "
            "The first INCONCLUSIVE permits one exact repeat. After a second, synthesize "
            "the evidence and improve the experiment or obtain maintainer direction; "
            "do not automatically repeat again.",
        ]
    )
    return "\n".join(lines) + "\n"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-a", required=True)
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--baseline-a2", required=True)
    parser.add_argument("--candidate-b2", help="Optional fourth segment (A-B-A-B).")
    parser.add_argument("--baseline-sha", required=True)
    parser.add_argument("--candidate-sha", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--summary")
    parser.add_argument(
        "--tolerance-percent", type=float, default=DEFAULT_TOLERANCE_PERCENT
    )
    parser.add_argument(
        "--max-control-drift-percent",
        type=float,
        default=DEFAULT_MAX_CONTROL_DRIFT_PERCENT,
    )
    args = parser.parse_args(argv)

    try:
        comparison = compare(
            _single_result(args.baseline_a),
            _single_result(args.candidate),
            _single_result(args.baseline_a2),
            args.tolerance_percent,
            args.max_control_drift_percent,
            candidate_b2_result=None if not args.candidate_b2 else _single_result(args.candidate_b2),
        )
        serialized = json.dumps(comparison, indent=2, allow_nan=False) + "\n"
    except (ValueError, OSError, TypeError, AttributeError, OverflowError) as error:
        # Invalid input or an unrepresentable comparison must not publish a pass.
        comparison = {"verdict": "inconclusive", "validationError": str(error), "metrics": []}
        serialized = json.dumps(comparison, indent=2, allow_nan=False) + "\n"
    report = markdown(comparison, args.baseline_sha, args.candidate_sha)
    print(report, end="")

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(serialized, encoding="utf-8")
    if args.summary:
        with Path(args.summary).open("a", encoding="utf-8") as handle:
            handle.write(report)
    return 0 if comparison["verdict"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
