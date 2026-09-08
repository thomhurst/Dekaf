"""Validate producer warmup and runtime coverage without trimming measured samples."""

import argparse
import json
import math
from pathlib import Path


QUIET_WORKLOAD_SECONDS = 10
MAX_SAMPLE_GAP_SECONDS = 2
RUNTIME_FIELDS = (
    "compiledMethods", "compilationMilliseconds", "threadPoolThreads", "pendingWorkItems",
    "cpuSeconds", "allocatedBytes", "heapBytes", "workingSetBytes",
    "gen0Collections", "gen1Collections", "gen2Collections", "gcPauseMilliseconds",
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
        if (current["compiledMethods"] != previous["compiledMethods"]
                or current["compilationMilliseconds"] != previous["compilationMilliseconds"]):
            raise ValueError(f"{label}: JIT activity remains; retain samples and investigate or extend all phase warmups")
        if current["threadPoolThreads"] != previous["threadPoolThreads"]:
            raise ValueError(f"{label}: thread-pool size changes; steady state is not established")


def validate(result):
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
    # Preserve both boundaries, but do not confuse setup between warmup and Start with
    # compilation inside measurement. Any measured activity, including observer work,
    # remains inconclusive until a trace attributes it; no measured sample is trimmed.
    quiet([start] + [runtime(item.get("runtime")) for item in intervals] + [end], "Measurement")
    return requested


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directories", nargs="+")
    parser.add_argument("--output", required=True)
    parser.add_argument("--summary")
    args = parser.parse_args(argv)
    errors = []
    durations = set()
    for directory in args.directories:
        try:
            paths = sorted(Path(directory).rglob("stress-test-results*.json"))
            if len(paths) != 1:
                raise ValueError(f"Expected one stress result, found {len(paths)}")
            envelope = object_value(json.loads(paths[0].read_text(encoding="utf-8-sig")), "result envelope")
            results = envelope.get("results")
            if not isinstance(results, list) or len(results) != 1 or not isinstance(results[0], dict):
                raise ValueError("Expected one result object")
            durations.add(validate(results[0]))
        except (ValueError, OSError) as error:
            errors.append(f"{directory}: {error}")
    if len(durations) > 1:
        errors.append("All phases must declare the same warmup duration")
    report = {"verdict": "INCONCLUSIVE" if errors else "VALIDATED", "errors": errors,
              "scope": "Warmup/runtime coverage only; this does not establish full performance acceptance."}
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    summary = f"## Warmup/runtime evidence: {report['verdict']}\n\n{report['scope']}\n"
    if errors:
        summary += "\n" + "\n".join(f"- {error}" for error in errors) + "\n"
    print(summary)
    if args.summary:
        with Path(args.summary).open("a", encoding="utf-8") as handle:
            handle.write(summary)
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
