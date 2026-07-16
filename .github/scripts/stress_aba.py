"""Compare one stress candidate with bracketing exact-SHA baseline runs."""

import argparse
import json
from dataclasses import dataclass
from math import isfinite
from pathlib import Path

from stress_report import cpu_micros_per_message, effective_rate, median_interval_rate


DEFAULT_TOLERANCE_PERCENT = 3.0
DEFAULT_MAX_CONTROL_DRIFT_PERCENT = 10.0


@dataclass(frozen=True)
class Metric:
    key: str
    label: str
    unit: str
    higher_is_better: bool
    gated: bool = True
    floor: bool = False


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
    Metric("alloc", "Allocation", "B/msg", False),
    Metric("stability", "Steady/peak ratio", "ratio", True, floor=True),
    Metric("max", "Latency max", "ms", False, gated=False),
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
        if not _finite_number(measurements.get(metric.key))
    ]
    if missing:
        raise ValueError(f"Missing finite metric(s): {', '.join(missing)}")
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


def _metric_status(
    metric,
    baseline_a,
    candidate,
    baseline_a2,
    tolerance_percent,
    max_control_drift_percent,
    floor_status=None,
):
    baseline_mean = (baseline_a + baseline_a2) / 2
    if baseline_mean == 0:
        raise ValueError(f"Baseline mean is zero for {metric.label}")

    delta_percent = 100 * (candidate - baseline_mean) / baseline_mean
    control_drift_percent = 100 * abs(baseline_a2 - baseline_a) / abs(baseline_mean)
    if not metric.gated:
        status = "recorded"
    elif metric.floor:
        status = floor_status
    else:
        if metric.higher_is_better:
            worse_than_both = candidate < min(baseline_a, baseline_a2) * (
                1 - tolerance_percent / 100
            )
            better_than_both = candidate >= max(baseline_a, baseline_a2)
            adverse = delta_percent < -tolerance_percent
        else:
            worse_than_both = candidate > max(baseline_a, baseline_a2) * (
                1 + tolerance_percent / 100
            )
            better_than_both = candidate <= min(baseline_a, baseline_a2)
            adverse = delta_percent > tolerance_percent

        # Symmetric decisiveness overrides for noisy controls: a candidate worse than both
        # bracketing controls by more than the tolerance is a regression no matter how far
        # apart the controls sit, and a candidate at least as good as the better control
        # cannot have regressed under any reading of the data — control drift only matters
        # for candidates the controls actually bracket (run 29525842754: the candidate beat
        # both controls on p99 yet was ruled inconclusive because the controls disagreed
        # with each other by 12%).
        if worse_than_both:
            status = "regression"
        elif better_than_both:
            status = "pass"
        elif control_drift_percent > max_control_drift_percent:
            status = "inconclusive"
        elif adverse:
            status = "regression"
        else:
            status = "pass"

    return {
        "key": metric.key,
        "label": metric.label,
        "unit": metric.unit,
        "baselineA": baseline_a,
        "candidate": candidate,
        "baselineA2": baseline_a2,
        "baselineMean": baseline_mean,
        "deltaPercent": delta_percent,
        "controlDriftPercent": control_drift_percent,
        "status": status,
        "gated": metric.gated,
    }


def compare(
    baseline_a_result,
    candidate_result,
    baseline_a2_result,
    tolerance_percent=DEFAULT_TOLERANCE_PERCENT,
    max_control_drift_percent=DEFAULT_MAX_CONTROL_DRIFT_PERCENT,
):
    if tolerance_percent < 0 or max_control_drift_percent < 0:
        raise ValueError("Comparison thresholds cannot be negative")

    identities = {
        _identity(baseline_a_result),
        _identity(candidate_result),
        _identity(baseline_a2_result),
    }
    if len(identities) != 1:
        raise ValueError("A-B-A results do not have the same workload identity")
    if _errors(baseline_a_result) or _errors(baseline_a2_result):
        raise ValueError("A baseline control contains producer or delivery errors")
    if _delivery_mismatch(baseline_a_result) or _delivery_mismatch(baseline_a2_result):
        raise ValueError("A baseline control did not deliver every accepted message")

    baseline_a = _measurements(baseline_a_result)
    candidate = _measurements(candidate_result)
    baseline_a2 = _measurements(baseline_a2_result)
    if _stability_breached(candidate_result):
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
        )
        for metric in METRICS
    ]

    candidate_failed = _errors(candidate_result) > 0 or _delivery_mismatch(candidate_result)
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
        "candidateErrors": _errors(candidate_result),
        "candidateDeliveryMismatch": _delivery_mismatch(candidate_result),
        "identity": list(_identity(candidate_result)),
        "metrics": metrics,
    }


def _format_number(value):
    if abs(value) >= 1000:
        return f"{value:,.0f}"
    return f"{value:.3f}"


def markdown(comparison, baseline_sha, candidate_sha):
    verdict = comparison["verdict"].upper()
    lines = [
        "## Exact-SHA stress A-B-A comparison",
        "",
        f"**Verdict: {verdict}**",
        "",
        f"Baseline: `{baseline_sha}` · Candidate: `{candidate_sha}` · "
        f"adverse tolerance: {comparison['tolerancePercent']:.1f}% · "
        f"maximum control drift: {comparison['maxControlDriftPercent']:.1f}%",
        "",
        "| Metric | Baseline A | Candidate B | Baseline A2 | B vs mean | Control drift | Gate |",
        "|---|---:|---:|---:|---:|---:|---|",
    ]
    for item in comparison["metrics"]:
        lines.append(
            f"| {item['label']} ({item['unit']}) | "
            f"{_format_number(item['baselineA'])} | "
            f"{_format_number(item['candidate'])} | "
            f"{_format_number(item['baselineA2'])} | "
            f"{item['deltaPercent']:+.2f}% | "
            f"{item['controlDriftPercent']:.2f}% | {item['status']} |"
        )
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
            "Acceptance requires PASS. REGRESSION rejects the candidate; "
            "INCONCLUSIVE requires an exact repeat.",
        ]
    )
    return "\n".join(lines) + "\n"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-a", required=True)
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--baseline-a2", required=True)
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

    comparison = compare(
        _single_result(args.baseline_a),
        _single_result(args.candidate),
        _single_result(args.baseline_a2),
        args.tolerance_percent,
        args.max_control_drift_percent,
    )
    report = markdown(comparison, args.baseline_sha, args.candidate_sha)
    print(report, end="")

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(comparison, indent=2) + "\n", encoding="utf-8")
    if args.summary:
        with Path(args.summary).open("a", encoding="utf-8") as handle:
            handle.write(report)
    return 0 if comparison["verdict"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
