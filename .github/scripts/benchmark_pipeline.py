"""Select and validate the full or filtered benchmark workflow."""

import json
import math
import os
import re
import sys
from pathlib import Path

# Namespace.Class.Method from `--list flat`; generic classes print as Class<Int32>.
BENCHMARK_NAME = re.compile(r"[A-Za-z_]\w*(\.[A-Za-z_][\w<>,]*)+\.[A-Za-z_]\w*")


def pipeline_mode(event_name, benchmark_filter):
    if event_name == "workflow_dispatch" and benchmark_filter not in ("", "*"):
        return "filtered"
    return "full"


def validate_reports(directory):
    paths = sorted(Path(directory).glob("*-report-full.json"))
    if not paths:
        raise ValueError("No full benchmark reports were produced; check the filter and run log.")
    count = 0
    for path in paths:
        report = json.loads(path.read_text(encoding="utf-8-sig"))
        benchmarks = report.get("Benchmarks")
        if not isinstance(benchmarks, list) or not benchmarks:
            raise ValueError(f"{path.name}: no benchmark cases were reported.")
        for benchmark in benchmarks:
            statistics = benchmark.get("Statistics") or {}
            mean = statistics.get("Mean")
            samples = statistics.get("N")
            if (not isinstance(mean, (int, float)) or isinstance(mean, bool)
                    or not math.isfinite(mean) or mean < 0
                    or not isinstance(samples, int) or isinstance(samples, bool) or samples < 1):
                name = benchmark.get("FullName", benchmark.get("DisplayInfo", "unknown case"))
                raise ValueError(f"{path.name}: {name} did not produce successful measurements.")
            count += 1
    return count


def shard_filters(listing, shard, count):
    """Return exact class filters for one shard of a BenchmarkDotNet ``--list flat`` output.

    Classes are sorted and dealt round-robin, so the shards cover every listed class exactly once
    and new fixtures join a shard without editing the workflow.
    """
    if count < 1 or not 1 <= shard <= count:
        raise ValueError(f"Shard {shard} is outside 1..{count}.")
    names = (line.strip() for line in listing.splitlines())
    classes = sorted({name.rsplit(".", 1)[0] for name in names if BENCHMARK_NAME.fullmatch(name)})
    if not classes:
        raise ValueError("The benchmark listing contains no benchmark classes.")
    return [f"{name}.*" for name in classes[shard - 1::count]]


def validate_gate(mode, is_main, results):
    if mode not in ("full", "filtered"):
        raise ValueError("Benchmark pipeline selection failed.")
    expected = {"PLAN": "success", "FILTERED": "skipped", "UNIT": "success",
                "CLIENT": "success", "NATIVE": "success", "SUMMARY": "success",
                "HISTORY": "success" if is_main else "skipped",
                "DOCS": "success" if is_main else "skipped"}
    if mode == "filtered":
        expected = {name: "skipped" for name in expected}
        expected.update(PLAN="success", FILTERED="success")
    failures = [f"{name}: expected {state}, got {results.get(name)!r}"
                for name, state in expected.items() if results.get(name) != state]
    if failures:
        raise ValueError("; ".join(failures))


def main(arguments):
    command, *arguments = arguments
    if command == "plan":
        event_name, benchmark_filter = arguments
        print(f"mode={pipeline_mode(event_name, benchmark_filter)}")
    elif command == "validate":
        directory, = arguments
        print(f"Validated {validate_reports(directory)} benchmark cases.")
    elif command == "shard":
        shard, count = (int(value) for value in arguments)
        for benchmark_filter in shard_filters(sys.stdin.read(), shard, count):
            print(benchmark_filter)
    elif command == "gate":
        results = {name: os.environ.get(f"{name}_RESULT") for name in
                   ("PLAN", "FILTERED", "UNIT", "CLIENT", "NATIVE", "SUMMARY", "HISTORY", "DOCS")}
        validate_gate(os.environ.get("PIPELINE_MODE"), os.environ.get("GITHUB_REF") == "refs/heads/main", results)
    else:
        raise ValueError(f"Unknown command: {command}")


if __name__ == "__main__":
    try:
        main(sys.argv[1:])
    except (ValueError, OSError) as error:
        print(f"Benchmark pipeline failed: {error}", file=sys.stderr)
        sys.exit(1)
