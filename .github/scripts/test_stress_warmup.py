import copy
import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

from stress_warmup import MIN_TREND_INTERVALS, assess_results, assess_trends, main, validate


INTERVALS = 40


def observation(second=0):
    # Cumulative counters advance linearly with steady per-message costs.
    return dict(threadPoolThreads=4,
                pendingWorkItems=0, cpuSeconds=10 + second, allocatedBytes=1000 + second * 100, heapBytes=100,
                workingSetBytes=200, gen0Collections=1, gen1Collections=0,
                gen2Collections=0, gcPauseMilliseconds=1)


def result(scenario=None, intervals=INTERVALS):
    data = {"throughput": {
        "warmup": {"requestedSeconds": 20, "workloadSeconds": 20, "elapsedSeconds": 21,
                   "completedMessages": 2000, "drainCycles": 6,
                   "samples": [{"workloadSeconds": second, "elapsedSeconds": second,
                                "acceptedMessages": second * 100, "completedMessages": second * 100,
                                "runtime": observation()} for second in range(21)]},
        "runtimeStart": observation(), "runtimeEnd": observation(intervals), "elapsedSeconds": intervals + 1,
        "intervalSamples": [{"elapsedSeconds": second, "messagesPerSecond": 1000.0,
                             "acceptedMessages": second * 1000, "runtime": observation(second)}
                            for second in range(1, intervals + 1)]}}
    if scenario is not None:
        data["scenario"] = scenario
    return data


def consumer_result():
    data = result(scenario="consumer")
    del data["throughput"]["warmup"]
    return data


def write(root, *results, name="stress-test-results.json"):
    (root / name).write_text(json.dumps({"results": list(results)}), encoding="utf-8")


def run_main(root, *arguments):
    output = root / "warmup-validation.json"
    summary = root / "summary.md"
    with contextlib.redirect_stdout(io.StringIO()):
        code = main([str(root), *arguments, "--output", str(output), "--summary", str(summary)])
    return code, json.loads(output.read_text(encoding="utf-8")), summary.read_text(encoding="utf-8")


class StressWarmupTests(unittest.TestCase):
    def test_steady_covered_segment_is_validated_and_exits_zero(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write(root, result())
            source = (root / "stress-test-results.json").read_bytes()
            code, report, summary = run_main(root)
            self.assertEqual(0, code)
            self.assertEqual("VALIDATED", report["verdict"])
            self.assertEqual("VALIDATED", report["coverageVerdict"])
            self.assertEqual("VALIDATED", report["trendVerdict"])
            self.assertEqual([], report["errors"])
            self.assertEqual(["delivery latency over time"], report["unassessedMetrics"])
            self.assertIn("VALIDATED", summary)
            self.assertEqual(source, (root / "stress-test-results.json").read_bytes())
            trend = next(iter(report["trends"].values()))
            self.assertEqual(INTERVALS, trend["metrics"]["intervals"])
            self.assertAlmostEqual(0.0, trend["metrics"]["throughputDriftPercent"])
            self.assertAlmostEqual(0.0, trend["metrics"]["cpuDriftPercent"])

    def test_startup_trends_make_the_segment_inconclusive_with_the_metric_named(self):
        cases = {
            "cpuSeconds": ("CPU per completed message drifted", lambda sample, index: sample["runtime"].__setitem__("cpuSeconds", 10 + index * index)),
            "allocatedBytes": ("allocations per completed message drifted", lambda sample, index: sample["runtime"].__setitem__("allocatedBytes", 1000 + index * index * 100)),
            "heapBytes": ("managed heap grew", lambda sample, index: sample["runtime"].__setitem__("heapBytes", 100 + index * 10)),
            "workingSetBytes": ("working set grew", lambda sample, index: sample["runtime"].__setitem__("workingSetBytes", 200 + index * 20)),
            "messagesPerSecond": ("throughput drifted", lambda sample, index: sample.__setitem__("messagesPerSecond", 1000.0 + index * 50)),
        }
        for field, (expected, mutate) in cases.items():
            with self.subTest(field=field), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                candidate = result()
                for index, sample in enumerate(candidate["throughput"]["intervalSamples"]):
                    mutate(sample, index)
                write(root, candidate)
                code, report, summary = run_main(root)
                self.assertEqual(1, code)
                self.assertEqual("INCONCLUSIVE", report["verdict"])
                self.assertEqual("VALIDATED", report["coverageVerdict"])
                self.assertEqual("INCONCLUSIVE", report["trendVerdict"])
                self.assertTrue(any(expected in error for error in report["errors"]), report["errors"])
                self.assertIn(expected, summary)

    def test_harness_slope_and_peak_flags_are_findings(self):
        for flag in ("throughputSlopeThresholdBreached", "intraRunThroughputThresholdBreached", "steadyStatePeakThresholdBreached"):
            with self.subTest(flag=flag):
                candidate = result()
                candidate[flag] = True
                findings = assess_trends(candidate)["findings"]
                self.assertEqual(1, len(findings))
                self.assertIn("breached the harness threshold", findings[0])

    def test_too_few_intervals_cannot_assess_trends(self):
        findings = assess_trends(result(intervals=MIN_TREND_INTERVALS - 1))["findings"]
        self.assertEqual(1, len(findings))
        self.assertIn(f"at least {MIN_TREND_INTERVALS}", findings[0])

    def test_intervals_without_runtime_leave_resource_trends_unassessed(self):
        candidate = result()
        for sample in candidate["throughput"]["intervalSamples"]:
            del sample["runtime"]
        findings = assess_trends(candidate)["findings"]
        self.assertTrue(any("unassessed" in finding for finding in findings))

    def test_sub_byte_allocation_drift_is_within_the_noise_floor(self):
        candidate = result()
        for index, sample in enumerate(candidate["throughput"]["intervalSamples"]):
            sample["runtime"]["allocatedBytes"] = 1000 + index * 100 + (index // 20) * 500
        self.assertEqual([], assess_trends(candidate)["findings"])

    def test_consumer_replay_segments_need_no_producer_warmup(self):
        candidate = consumer_result()
        self.assertIsNone(validate(candidate))
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write(root, candidate)
            code, report, _ = run_main(root)
            self.assertEqual(0, code)
            self.assertEqual("VALIDATED", report["verdict"])

    def test_producer_segments_without_warmup_are_rejected(self):
        for scenario in (None, "producer", "producer-idempotent"):
            with self.subTest(scenario=scenario):
                candidate = result(scenario=scenario)
                del candidate["throughput"]["warmup"]
                with self.assertRaisesRegex(ValueError, "warmup"):
                    validate(candidate)

    def test_assess_results_mirrors_the_cli_for_parsed_phases(self):
        report = assess_results({"A": result(), "B": result(), "A2": result()})
        self.assertEqual("VALIDATED", report["verdict"])
        self.assertEqual(3, report["resultCount"])
        self.assertEqual({"A", "B", "A2"}, set(report["trends"]))
        drifting = result()
        drifting["throughput"]["intervalSamples"][-1]["runtime"]["cpuSeconds"] = 500
        report = assess_results({"A": result(), "B": drifting, "A2": result()})
        self.assertEqual("INCONCLUSIVE", report["verdict"])
        self.assertTrue(any(error.startswith("B: ") for error in report["errors"]))

    def test_exact_phase_still_requires_one_file_and_one_client_result(self):
        for files, clients in ((2, 1), (1, 2), (1, 1)):
            with self.subTest(files=files, clients=clients), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for index in range(files):
                    write(root, *[result() for _ in range(clients)], name=f"stress-test-results-{index}.json")
                code, report, _ = run_main(root)
                valid = files == clients == 1
                self.assertEqual(0 if valid else 1, code)
                self.assertEqual("VALIDATED" if valid else "INCONCLUSIVE", report["coverageVerdict"])

    def test_regular_run_requires_same_declared_warmup(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            longer = result()
            warmup = longer["throughput"]["warmup"]
            warmup.update(requestedSeconds=21, workloadSeconds=21, completedMessages=2100)
            last = copy.deepcopy(warmup["samples"][-1])
            last.update(workloadSeconds=21, elapsedSeconds=22, acceptedMessages=2100, completedMessages=2100)
            warmup["samples"].append(last)
            write(root, result(), longer)
            code, report, _ = run_main(root, "--expected-results", "2")
            self.assertEqual(1, code)
            self.assertIn("same warmup duration", " ".join(report["errors"]))

    def test_regular_run_validates_all_clients_and_variant_files(self):
        for invalid_index in (None, 0, 1, 2, 3):
            with self.subTest(invalid_index=invalid_index), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for file_index in range(2):
                    samples = [result(), result()]
                    for index, sample in enumerate(samples):
                        if file_index * 2 + index == invalid_index:
                            sample["throughput"]["runtimeEnd"]["cpuSeconds"] = -1
                    write(root, *samples, name=f"stress-test-results-{file_index}.json")
                code, report, summary = run_main(root, "--expected-results", "4")
                valid = invalid_index is None
                self.assertEqual(0 if valid else 1, code)
                self.assertEqual("VALIDATED" if valid else "INCONCLUSIVE", report["verdict"])
                self.assertEqual("VALIDATED" if valid else "INCONCLUSIVE", report["coverageVerdict"])
                self.assertEqual(4, report["resultCount"])
                self.assertIn(report["verdict"], summary)
                if not valid:
                    self.assertIn("cpuSeconds", " ".join(report["errors"]))

    def test_regular_run_rejects_missing_or_malformed_results(self):
        for payload in (None, {"results": []}, {"results": [result()]}, {"results": [None, result()]}):
            with self.subTest(payload=payload), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                if payload is not None:
                    (root / "stress-test-results.json").write_text(json.dumps(payload), encoding="utf-8")
                code, report, _ = run_main(root, "--expected-results", "2")
                self.assertEqual(1, code)
                self.assertEqual("INCONCLUSIVE", report["verdict"])

    def test_accepts_complete_quiet_runtime_coverage(self):
        self.assertEqual(20, validate(result()))

    def test_jit_diagnostics_do_not_gate_measurements(self):
        candidate = result()
        for phase in ("runtimeStart", "runtimeEnd"):
            candidate["throughput"][phase]["compiledMethods"] = 100 if phase == "runtimeStart" else 200
        self.assertEqual(20, validate(candidate))

    def test_measurement_and_boundary_activity_cannot_be_trimmed(self):
        for field, value in (("threadPoolThreads", 5),):
            for location in ("runtimeStart", "runtimeEnd", "interval"):
                with self.subTest(field=field, location=location):
                    candidate = result()
                    throughput = candidate["throughput"]
                    target = throughput["intervalSamples"][0]["runtime"] if location == "interval" else throughput[location]
                    target[field] = value
                    with self.assertRaises(ValueError):
                        validate(candidate)

    def test_drain_or_idle_time_cannot_replace_workload_warmup(self):
        candidate = result()
        candidate["throughput"]["warmup"].update(workloadSeconds=1, elapsedSeconds=60)
        with self.assertRaisesRegex(ValueError, "workload duration"):
            validate(candidate)

    def test_requires_reuse_cycles_and_completed_messages(self):
        for field, value in (("drainCycles", 1), ("completedMessages", 0), ("requestedSeconds", 19)):
            with self.subTest(field=field):
                candidate = result()
                candidate["throughput"]["warmup"][field] = value
                with self.assertRaises(ValueError):
                    validate(candidate)

    def test_runtime_coverage_includes_final_delivery_drain(self):
        candidate = result()
        candidate["throughput"]["elapsedSeconds"] = 10
        with self.assertRaisesRegex(ValueError, "complete window"):
            validate(candidate)

    def test_rejects_missing_runtime_or_warmup_samples(self):
        for missing in ("runtimeStart", "runtimeEnd", "warmup"):
            with self.subTest(missing=missing):
                candidate = result()
                del candidate["throughput"][missing]
                with self.assertRaises(ValueError):
                    validate(candidate)
        candidate = result()
        del candidate["throughput"]["warmup"]["samples"][8:13]
        with self.assertRaisesRegex(ValueError, "coverage"):
            validate(candidate)

    def test_validation_preserves_all_samples(self):
        candidate = result()
        original = copy.deepcopy(candidate)
        validate(candidate)
        assess_trends(candidate)
        self.assertEqual(original, candidate)

    def test_rejects_malformed_objects_and_samples(self):
        for field in ("warmup", "intervalSamples", "runtimeStart"):
            for value in (None, "invalid", [None]):
                with self.subTest(field=field, value=value):
                    candidate = result()
                    candidate["throughput"][field] = value
                    with self.assertRaises(ValueError):
                        validate(candidate)

    def test_workflow_isolates_comparison_hardware_fixtures_and_brokers(self):
        workflow = (Path(__file__).resolve().parents[1] / "workflows/stress-tests.yml").read_text(encoding="utf-8")
        self.assertIn("matrix.baseline_sha != '' && 'ubuntu-latest' || 'ubicloud-standard-8'", workflow)
        self.assertIn('rsync -a --delete --exclude bin --exclude obj', workflow)
        self.assertIn('git merge-base --is-ancestor "$BASELINE_SHA" "$GITHUB_SHA"', workflow)
        self.assertIn('dotnet build-server shutdown', workflow)
        self.assertIn("matrix.brokers == 1 && matrix.baseline_sha == '' && 'localhost:9092' || ''", workflow)
        self.assertIn('python3 .github/scripts/stress_warmup.py', workflow)
        self.assertIn("--require-startup-assessment", workflow)
        self.assertLess(workflow.index('Build Stress Tests (exact baseline)'), workflow.index('run_aba()'))
        self.assertNotIn("schedule:", workflow)
        self.assertIn("consumer-1b|consumer-batch-1b|consumer-raw-1b|consumer-raw-batch-1b) ;;", workflow)


if __name__ == "__main__":
    unittest.main()
