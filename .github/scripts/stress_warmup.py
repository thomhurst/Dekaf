"""Check producer runtime coverage; fail acceptance on unassessed startup trends."""

import argparse
import json
import math
from pathlib import Path


QUIET_WORKLOAD_SECONDS = 10
MAX_SAMPLE_GAP_SECONDS = 2
RUNTIME_FIELDS = (
    "threadPoolThreads", "pendingWorkItems",
    "cpuSeconds", "allocatedBytes", "heapBytes", "workingSetBytes",
    "gen0Collections", "gen1Collections", "gen2Collections", "gcPauseMilliseconds",
)


# The retained schema has no interval delivery-latency distribution or continuous
# completed-message counts. Runtime counter coverage alone cannot assess
# startup trends. Do not invent tolerances or accept an external "steady" flag.
UNASSESSED_METRICS = (
    "completed-message throughput", "CPU per completed message", "pending work",
    "allocations per completed message", "heap and working set", "GC activity",
    "delivery latency over time",
)


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


def validate(result):
    """Validate coverage and thread-count checks only, not steady state."""
    throughput = object_value(result.get("throughput"), "throughput")
    warmup = object_value(throughput.get("warmup"), "warmup")
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


def assessment_report(errors, result_count):
    coverage_verdict = "INCONCLUSIVE" if errors else "VALIDATED"
    errors = [*errors, "Startup trends are unassessed: " + ", ".join(UNASSESSED_METRICS)
              + ". Interval latency and completed-message evidence plus a declared trend assessment "
              "are required; repeating the current aggregate schema cannot establish steady state."]
    return {"verdict": "INCONCLUSIVE", "coverageVerdict": coverage_verdict, "errors": errors,
            "unassessedMetrics": list(UNASSESSED_METRICS), "resultCount": result_count,
            "scope": "Coverage and thread-count checks only. Steady state remains unassessed; "
                     "do not accept or publish these measurements as validated performance."}


def assess_results(results):
    """Assess the exact parsed phase objects used by the diagnostic comparator."""
    errors = []
    durations = set()
    for label, result in results.items():
        try:
            durations.add(validate(object_value(result, "result")))
        except ValueError as error:
            errors.append(f"{label}: {error}")
    if len(durations) > 1:
        errors.append("All phases must declare the same warmup duration")
    return assessment_report(errors, len(results))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directories", nargs="+")
    parser.add_argument("--output", required=True)
    parser.add_argument("--summary")
    parser.add_argument("--expected-results", type=int,
                        help="Validate all result files/client entries and require this total count.")
    args = parser.parse_args(argv)
    errors = []
    durations = set()
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
                try:
                    durations.add(validate(object_value(result, "result")))
                except ValueError as error:
                    errors.append(f"{path} result {index + 1}: {error}")
    if args.expected_results is not None and result_count != args.expected_results:
        errors.append(f"Expected {args.expected_results} results, found {result_count}")
    if len(durations) > 1:
        errors.append("All phases must declare the same warmup duration")
    report = assessment_report(errors, result_count)
    coverage_verdict = report["coverageVerdict"]
    errors = report["errors"]
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    summary = (f"## Warmup/runtime evidence: {report['verdict']}\n\n{report['scope']}\n"
               f"\nCoverage and thread-count checks: {coverage_verdict}.\n")
    if errors:
        summary += "\n" + "\n".join(f"- {error}" for error in errors) + "\n"
    print(summary)
    if args.summary:
        with Path(args.summary).open("a", encoding="utf-8") as handle:
            handle.write(summary)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
