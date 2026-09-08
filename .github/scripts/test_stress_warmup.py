import copy
import unittest
from pathlib import Path

from stress_warmup import validate


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
        self.assertIn("matrix.baseline_sha != '' && steps.warmup.outcome == 'success'", workflow)
        self.assertLess(workflow.index('Build Stress Tests (exact baseline)'), workflow.index('run_aba()'))


if __name__ == "__main__":
    unittest.main()
