"""Validate runtime coverage and assess steady-state trends for stress comparison segments.

Coverage checks confirm that every phase retained its warmup (duration-based producers), its
runtime observations at both measurement boundaries and one-second interval samples across
the whole measured window. The trend assessment then compares the first and last third of
the measured intervals: throughput, CPU per completed message, allocations per message and
managed heap/working set must not drift beyond the declared tolerances, and the harness's own
intra-run slope/peak thresholds must not be breached. Delivery latency over time is not in the
retained schema; aggregate quantiles are compared separately by stress_aba.py.
"""

import argparse
import json
import math
from pathlib import Path
from statistics import median


QUIET_WORKLOAD_SECONDS = 10
MAX_SAMPLE_GAP_SECONDS = 2
MIN_TREND_INTERVALS = 30
TREND_TOLERANCE_PERCENT = 10.0
MEMORY_GROWTH_PERCENT = 25.0
ALLOCATION_NOISE_FLOOR_BYTES = 1.0
RUNTIME_FIELDS = (
    "threadPoolThreads", "pendingWorkItems",
    "cpuSeconds", "allocatedBytes", "heapBytes", "workingSetBytes",
    "gen0Collections", "gen1Collections", "gen2Collections", "gcPauseMilliseconds",
)

# Recorded, never gated: the retained schema keeps aggregate latency quantiles only.
UNASSESSED_METRICS = ("delivery latency over time",)


def number(value, name):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
        raise ValueError(f"Missing finite nonnegative {name}")
    return value


def runtime(sample):
    if not isinstance(sample, dict):
        raise ValueError("Missing runtime observation")
    for field in RUNTIME_FIELDS:
        number(sample.get(field), field)
    return sample


def object_value(value, name):
    if not isinstance(value, dict):
        raise ValueError(f"Missing {name} object")
    return value


def sample_list(value, name):
    if not isinstance(value, list) or any(not isinstance(item, dict) for item in value):
        raise ValueError(f"Missing {name} sample objects")
    return value


def quiet(samples, label):
    for previous, current in zip(samples, samples[1:]):
        if current["threadPoolThreads"] != previous["threadPoolThreads"]:
            raise ValueError(f"{label}: thread-pool size changes; steady state is not established")


def _requires_warmup(result):
    scenario = str(result.get("scenario") or "producer").casefold()
    return not scenario.startswith("consumer")


def _validate_warmup(warmup):
    requested = number(warmup.get("requestedSeconds"), "warmup requestedSeconds")
    workload = number(warmup.get("workloadSeconds"), "warmup workloadSeconds")
    if requested < 20 or workload < requested:
        raise ValueError("Warmup must complete the declared workload duration (at least 20 seconds)")
    if number(warmup.get("drainCycles"), "drainCycles") < 6:
        raise ValueError("Warmup must complete six drain/reuse cycles")
    completed = number(warmup.get("completedMessages"), "warmup completedMessages")
    if completed == 0:
        raise ValueError("Warmup did not complete any messages")
    samples = sample_list(warmup.get("samples"), "warmup")
    if len(samples) < 2:
        raise ValueError("Warmup runtime samples are missing")
    active = [number(sample.get("workloadSeconds"), "sample workloadSeconds") for sample in samples]
    if active[0] != 0 or active[-1] < workload:
        raise ValueError("Warmup samples do not cover the workload boundaries")
    if any(right < left or right - left > MAX_SAMPLE_GAP_SECONDS for left, right in zip(active, active[1:])):
        raise ValueError("Warmup runtime coverage has a gap or moves backwards")
    if samples[-1].get("completedMessages") != completed:
        raise ValueError("Final warmup sample does not confirm the drained message count")
    observations = [runtime(sample.get("runtime")) for sample in samples]
    tail_start = max(index for index, seconds in enumerate(active) if seconds <= workload - QUIET_WORKLOAD_SECONDS)
    quiet(observations[tail_start:], "Warmup tail")
    if samples[-1].get("acceptedMessages", 0) <= samples[tail_start].get("acceptedMessages", 0):
        raise ValueError("Warmup quiet window has no workload progress")
    return requested


def validate(result):
    """Validate runtime coverage. Returns the declared warmup seconds (None for consumer replay)."""
    throughput = object_value(result.get("throughput"), "throughput")
    warmup = throughput.get("warmup")
    if warmup is None and not _requires_warmup(result):
        requested = None
    else:
        requested = _validate_warmup(object_value(warmup, "warmup"))

    start = runtime(throughput.get("runtimeStart"))
    end = runtime(throughput.get("runtimeEnd"))
    intervals = sample_list(throughput.get("intervalSamples"), "measured runtime")
    elapsed = number(throughput.get("elapsedSeconds"), "measured elapsedSeconds")
    times = [0] + [number(item.get("elapsedSeconds"), "interval elapsedSeconds") for item in intervals] + [elapsed]
    if elapsed <= 0 or not intervals or any(
            right < left or right - left > MAX_SAMPLE_GAP_SECONDS for left, right in zip(times, times[1:])):
        raise ValueError("Measured runtime samples do not cover the complete window, including drain")
    # Preserve all measured samples, including both boundaries and drain.
    quiet([start] + [runtime(item.get("runtime")) for item in intervals] + [end], "Measurement")
    return requested


def _percent_change(first, last):
    if first == 0:
        return 0.0 if last == 0 else None
    return 100 * (last - first) / first


def _per_message(segment, field):
    first, last = segment[0], segment[-1]
    messages = last.get("acceptedMessages", 0) - first.get("acceptedMessages", 0)
    if messages <= 0:
        return None
    return (last["runtime"][field] - first["runtime"][field]) / messages


def assess_trends(result):
    """Compare the first and last third of the measured intervals; return findings and metrics."""
    intervals = (result.get("throughput") or {}).get("intervalSamples") or []
    findings = []
    metrics = {"intervals": len(intervals)}
    if len(intervals) < MIN_TREND_INTERVALS:
        findings.append(f"only {len(intervals)} interval samples; at least {MIN_TREND_INTERVALS} are needed to assess trends")
        return {"findings": findings, "metrics": metrics}
    third = len(intervals) // 3
    first, last = intervals[:third], intervals[-third:]

    rates = [median(item.get("messagesPerSecond", 0) for item in part) for part in (first, last)]
    metrics["throughputFirstThird"], metrics["throughputLastThird"] = rates
    drift = _percent_change(rates[0], rates[1])
    metrics["throughputDriftPercent"] = drift
    if drift is None or abs(drift) > TREND_TOLERANCE_PERCENT:
        findings.append(f"throughput drifted {drift:+.1f}% between the first and last third" if drift is not None
                        else "throughput trend could not be computed from a zero first third")
    for flag, label in (("throughputSlopeThresholdBreached", "throughput slope"),
                        ("intraRunThroughputThresholdBreached", "intra-run throughput drift"),
                        ("steadyStatePeakThresholdBreached", "steady/peak ratio")):
        if result.get(flag) is True:
            findings.append(f"{label} breached the harness threshold")

    has_runtime = all(isinstance(item.get("runtime"), dict) for item in first + last)
    if has_runtime:
        cpu = [_per_message(part, "cpuSeconds") for part in (first, last)]
        alloc = [_per_message(part, "allocatedBytes") for part in (first, last)]
        if None in cpu or None in alloc:
            findings.append("no completed-message progress in a measured third")
        else:
            metrics["cpuMicrosPerMessageFirstThird"], metrics["cpuMicrosPerMessageLastThird"] = [value * 1e6 for value in cpu]
            cpu_drift = _percent_change(cpu[0], cpu[1])
            metrics["cpuDriftPercent"] = cpu_drift
            if cpu_drift is None or abs(cpu_drift) > TREND_TOLERANCE_PERCENT:
                findings.append(f"CPU per completed message drifted {cpu_drift:+.1f}% between the first and last third"
                                if cpu_drift is not None else "CPU per message trend could not be computed")
            metrics["allocatedBytesPerMessageFirstThird"], metrics["allocatedBytesPerMessageLastThird"] = alloc
            alloc_drift = _percent_change(alloc[0], alloc[1])
            metrics["allocationDriftPercent"] = alloc_drift
            if abs(alloc[1] - alloc[0]) > ALLOCATION_NOISE_FLOOR_BYTES and (
                    alloc_drift is None or abs(alloc_drift) > TREND_TOLERANCE_PERCENT):
                findings.append("allocations per completed message drifted "
                                f"{alloc_drift:+.1f}% between the first and last third" if alloc_drift is not None
                                else "allocation trend could not be computed")
        for field, label in (("heapBytes", "managed heap"), ("workingSetBytes", "working set")):
            levels = [median(item["runtime"][field] for item in part) for part in (first, last)]
            growth = _percent_change(levels[0], levels[1])
            metrics[f"{field}GrowthPercent"] = growth
            if growth is not None and growth > MEMORY_GROWTH_PERCENT:
                findings.append(f"{label} grew {growth:+.1f}% between the first and last third")
        metrics["pendingWorkItemsLastThirdMax"] = max(item["runtime"]["pendingWorkItems"] for item in last)
        metrics["gen2CollectionsDelta"] = last[-1]["runtime"]["gen2Collections"] - first[0]["runtime"]["gen2Collections"]
    else:
        findings.append("interval samples carry no runtime observations; CPU, allocation and memory trends are unassessed")
    return {"findings": findings, "metrics": metrics}


def assessment_report(errors, result_count, trends=None):
    trends = trends or {}
    coverage_verdict = "INCONCLUSIVE" if errors else "VALIDATED"
    trend_findings = [f"{label}: {finding}" for label, trend in trends.items() for finding in trend["findings"]]
    trend_verdict = "INCONCLUSIVE" if trend_findings or not trends else "VALIDATED"
    verdict = "VALIDATED" if coverage_verdict == trend_verdict == "VALIDATED" else "INCONCLUSIVE"
    return {"verdict": verdict, "coverageVerdict": coverage_verdict, "trendVerdict": trend_verdict,
            "errors": [*errors, *trend_findings], "trends": trends,
            "unassessedMetrics": list(UNASSESSED_METRICS), "resultCount": result_count,
            "tolerances": {"trendPercent": TREND_TOLERANCE_PERCENT, "memoryGrowthPercent": MEMORY_GROWTH_PERCENT,
                           "allocationNoiseFloorBytes": ALLOCATION_NOISE_FLOOR_BYTES,
                           "minimumIntervals": MIN_TREND_INTERVALS},
            "scope": "Runtime coverage and first-third/last-third steady-state trends. Delivery latency over time "
                     "is not in the retained schema; aggregate quantiles are compared by the A-B-A screen."}


def _assess(label, result, errors, trends, durations):
    try:
        durations.add(validate(object_value(result, "result")))
    except ValueError as error:
        errors.append(f"{label}: {error}")
        return
    trends[label] = assess_trends(result)


def assess_results(results):
    """Assess the exact parsed phase objects used by the A-B-A comparator."""
    errors, trends, durations = [], {}, set()
    for label, result in results.items():
        _assess(label, result, errors, trends, durations)
    if len(durations) > 1:
        errors.append("All phases must declare the same warmup duration")
    return assessment_report(errors, len(results), trends)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directories", nargs="+")
    parser.add_argument("--output", required=True)
    parser.add_argument("--summary")
    parser.add_argument("--expected-results", type=int,
                        help="Validate all result files/client entries and require this total count.")
    args = parser.parse_args(argv)
    errors, trends, durations = [], {}, set()
    result_count = 0
    if args.expected_results is not None and args.expected_results <= 0:
        errors.append("Expected result count must be positive")
    for directory in args.directories:
        try:
            paths = sorted(Path(directory).rglob("stress-test-results*.json"))
            if not paths or (args.expected_results is None and len(paths) != 1):
                raise ValueError(f"Expected one stress result, found {len(paths)}")
        except (ValueError, OSError) as error:
            errors.append(f"{directory}: {error}")
            continue
        for path in paths:
            try:
                envelope = object_value(json.loads(path.read_text(encoding="utf-8-sig")), "result envelope")
                results = envelope.get("results")
                if not isinstance(results, list) or not results or (
                        args.expected_results is None and len(results) != 1):
                    raise ValueError("Expected nonempty result objects (one per exact-SHA phase)")
            except (ValueError, OSError) as error:
                errors.append(f"{path}: {error}")
                continue
            for index, result in enumerate(results):
                result_count += 1
                _assess(f"{path} result {index + 1}", result, errors, trends, durations)
    if args.expected_results is not None and result_count != args.expected_results:
        errors.append(f"Expected {args.expected_results} results, found {result_count}")
    if len(durations) > 1:
        errors.append("All phases must declare the same warmup duration")
    report = assessment_report(errors, result_count, trends)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    summary = (f"## Warmup/runtime evidence: {report['verdict']}\n\n{report['scope']}\n"
               f"\nCoverage: {report['coverageVerdict']}; steady-state trends: {report['trendVerdict']} "
               f"({report['resultCount']} result(s)).\n")
    if report["errors"]:
        summary += "\n" + "\n".join(f"- {error}" for error in report["errors"]) + "\n"
    print(summary)
    if args.summary:
        with Path(args.summary).open("a", encoding="utf-8") as handle:
            handle.write(summary)
    return 0 if report["verdict"] == "VALIDATED" else 1


if __name__ == "__main__":
    raise SystemExit(main())
