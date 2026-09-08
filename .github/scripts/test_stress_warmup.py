import copy
import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

from stress_warmup import main, validate


def observation():
    return dict(compiledMethods=100, compilationMilliseconds=10, threadPoolThreads=4,
                pendingWorkItems=0, cpuSeconds=10, allocatedBytes=1000, heapBytes=100,
                workingSetBytes=200, gen0Collections=1, gen1Collections=0,
                gen2Collections=0, gcPauseMilliseconds=1)


def result():
    return {"throughput": {
        "warmup": {"requestedSeconds": 20, "workloadSeconds": 20, "elapsedSeconds": 21,
                   "completedMessages": 2000, "drainCycles": 6,
                   "samples": [{"workloadSeconds": second, "elapsedSeconds": second,
                                "acceptedMessages": second * 100, "completedMessages": second * 100,
                                "runtime": observation()} for second in range(21)]},
        "runtimeStart": observation(), "runtimeEnd": observation(), "elapsedSeconds": 5,
        "intervalSamples": [{"elapsedSeconds": second, "runtime": observation()} for second in range(1, 5)]}}


class StressWarmupTests(unittest.TestCase):
    def test_quiet_jit_cannot_validate_unassessed_startup_trends(self):
        changes = (None, "cpuSeconds", "pendingWorkItems", "allocatedBytes", "heapBytes",
                   "workingSetBytes", "gen0Collections", "gcPauseMilliseconds", "acceptedMessages")
        for changed in changes:
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as directory:
                candidate = result()
                for index, sample in enumerate(candidate["throughput"]["intervalSamples"]):
                    if changed == "acceptedMessages":
                        sample[changed] = (index + 1) ** 3 * 100
                    elif changed is not None:
                        sample["runtime"][changed] += (index + 1) ** 3 * 100
                # Aggregate latency cannot establish whether latency changes over time.
                candidate["latency"] = dict(count=10000, p50Ms=1, p99Ms=2, maxMs=10)
                root = Path(directory)
                source = root / "stress-test-results.json"
                source.write_text(json.dumps({"results": [candidate]}), encoding="utf-8")
                original = source.read_bytes()
                output, summary = root / "validation.json", root / "summary.md"
                with contextlib.redirect_stdout(io.StringIO()):
                    code = main([str(root), "--output", str(output), "--summary", str(summary)])
                self.assertEqual(1, code)
                report = json.loads(output.read_text(encoding="utf-8"))
                self.assertEqual("INCONCLUSIVE", report["verdict"])
                self.assertEqual("VALIDATED", report["coverageVerdict"])
                self.assertIn("delivery latency over time", report["unassessedMetrics"])
                self.assertIn("completed-message throughput", report["unassessedMetrics"])
                self.assertIn("INCONCLUSIVE", summary.read_text(encoding="utf-8"))
                self.assertEqual(original, source.read_bytes())

    def test_exact_phase_still_requires_one_file_and_one_client_result(self):
        for files, clients in ((2, 1), (1, 2), (1, 1)):
            with self.subTest(files=files, clients=clients), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for index in range(files):
                    (root / f"stress-test-results-{index}.json").write_text(
                        json.dumps({"results": [result() for _ in range(clients)]}), encoding="utf-8"
                    )
                output = root / "warmup-validation.json"
                with contextlib.redirect_stdout(io.StringIO()):
                    code = main([str(root), "--output", str(output)])
                self.assertEqual(1, code)
                report = json.loads(output.read_text(encoding="utf-8"))
                self.assertEqual("VALIDATED" if files == clients == 1 else "INCONCLUSIVE", report["coverageVerdict"])

    def test_regular_run_requires_same_declared_warmup(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            longer = result()
            warmup = longer["throughput"]["warmup"]
            warmup.update(requestedSeconds=21, workloadSeconds=21, completedMessages=2100)
            last = copy.deepcopy(warmup["samples"][-1])
            last.update(workloadSeconds=21, elapsedSeconds=22, acceptedMessages=2100, completedMessages=2100)
            warmup["samples"].append(last)
            (root / "stress-test-results.json").write_text(
                json.dumps({"results": [result(), longer]}), encoding="utf-8"
            )
            output = root / "warmup-validation.json"
            with contextlib.redirect_stdout(io.StringIO()):
                code = main([str(root), "--expected-results", "2", "--output", str(output)])
            self.assertEqual(1, code)
            self.assertIn("same warmup duration", " ".join(json.loads(output.read_text(encoding="utf-8"))["errors"]))

    def test_regular_run_validates_all_clients_and_variant_files(self):
        for invalid_index in (None, 0, 1, 2, 3):
            with self.subTest(invalid_index=invalid_index), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for file_index in range(2):
                    samples = [result(), result()]
                    for index, sample in enumerate(samples):
                        if file_index * 2 + index == invalid_index:
                            sample["throughput"]["runtimeEnd"]["compiledMethods"] += 1
                    (root / f"stress-test-results-{file_index}.json").write_text(
                        json.dumps({"results": samples}), encoding="utf-8"
                    )
                output = root / "warmup-validation.json"
                summary = root / "summary.md"
                with contextlib.redirect_stdout(io.StringIO()):
                    code = main([str(root), "--expected-results", "4", "--output", str(output), "--summary", str(summary)])
                report = json.loads(output.read_text(encoding="utf-8"))
                self.assertEqual(1, code)
                self.assertEqual("INCONCLUSIVE", report["verdict"])
                self.assertEqual("VALIDATED" if invalid_index is None else "INCONCLUSIVE", report["coverageVerdict"])
                self.assertEqual(4, report["resultCount"])
                self.assertIn(report["verdict"], summary.read_text(encoding="utf-8"))
                if invalid_index is not None:
                    self.assertIn("JIT", " ".join(report["errors"]))

    def test_regular_run_rejects_missing_or_malformed_results(self):
        for payload in (None, {"results": []}, {"results": [result()]}, {"results": [None, result()]}):
            with self.subTest(payload=payload), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                if payload is not None:
                    (root / "stress-test-results.json").write_text(json.dumps(payload), encoding="utf-8")
                output = root / "warmup-validation.json"
                with contextlib.redirect_stdout(io.StringIO()):
                    code = main([str(root), "--expected-results", "2", "--output", str(output)])
                self.assertEqual(1, code)
                self.assertEqual("INCONCLUSIVE", json.loads(output.read_text(encoding="utf-8"))["verdict"])

    def test_accepts_complete_quiet_runtime_coverage(self):
        self.assertEqual(20, validate(result()))

    def test_early_jit_is_allowed_but_tail_jit_is_not(self):
        early = result()
        early["throughput"]["warmup"]["samples"][0]["runtime"]["compiledMethods"] = 90
        self.assertEqual(20, validate(early))
        late = result()
        late["throughput"]["warmup"]["samples"][-1]["runtime"]["compiledMethods"] = 101
        with self.assertRaisesRegex(ValueError, "JIT"):
            validate(late)

    def test_measurement_and_boundary_activity_cannot_be_trimmed(self):
        for field, value in (("compiledMethods", 101), ("compilationMilliseconds", 11), ("threadPoolThreads", 5)):
            for location in ("runtimeStart", "runtimeEnd", "interval"):
                with self.subTest(field=field, location=location):
                    candidate = result()
                    throughput = candidate["throughput"]
                    target = throughput["intervalSamples"][0]["runtime"] if location == "interval" else throughput[location]
                    target[field] = value
                    with self.assertRaises(ValueError):
                        validate(candidate)

    def test_compilation_entirely_before_measurement_is_not_misclassified(self):
        candidate = result()
        throughput = candidate["throughput"]
        for sample in [throughput["runtimeStart"], throughput["runtimeEnd"]] + [
                item["runtime"] for item in throughput["intervalSamples"]]:
            sample["compiledMethods"] = 105
            sample["compilationMilliseconds"] = 11
        self.assertEqual(20, validate(candidate))

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


if __name__ == "__main__":
    unittest.main()
