import copy
import json
import re
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from stress_aba import compare, markdown, validate_keyed_consumer
from test_stress_aba import consumer
from test_fixed_worker_pool import bash_path, workflow_script


def keyed_result():
    value = consumer()
    value["scenario"] = "consumer-keyed"
    value["throughput"]["totalMessages"] = 196608
    value["keyedConsumer"] = {
        "shape": "scalar", "keySizeBytes": 4, "partitions": 6,
        "keysPerPartition": 32, "recordsPerPartition": 32768,
        "handlerConcurrency": 4, "bufferedRecordsPerPartition": 256,
        "yieldEveryKeyRecords": 64, "completedPasses": 1, "completedRecords": 196608,
    }
    value.pop("deliveredMessages", None)
    return value


class KeyedConsumerTests(unittest.TestCase):
    def test_identical_replays_pass_without_latency(self):
        result = keyed_result()
        comparison = compare(result, result, result)
        self.assertEqual("pass", comparison["verdict"])
        self.assertIn("shape=scalar", markdown(comparison, "a" * 40, "b" * 40))
        self.assertTrue(all(not metric["gated"] for metric in comparison["metrics"] if metric["key"] in ("p50", "p95", "p99")))

    def test_workload_dimensions_cannot_change_between_revisions(self):
        for field, value in (("shape", "binary"), ("handlerConcurrency", 8),
                             ("bufferedRecordsPerPartition", 512), ("yieldEveryKeyRecords", 128)):
            with self.subTest(field=field):
                control = keyed_result()
                candidate = copy.deepcopy(control)
                candidate["keyedConsumer"][field] = value
                if field == "shape":
                    candidate["keyedConsumer"]["keySizeBytes"] = 16
                with self.assertRaisesRegex(ValueError, "workload identity"):
                    compare(control, candidate, control)

    def test_missing_tail_and_missing_dimensions_are_rejected(self):
        for field in ("completedRecords", "completedPasses", "partitions", "recordsPerPartition"):
            with self.subTest(field=field):
                value = keyed_result()
                value["keyedConsumer"][field] += 1
                with self.assertRaisesRegex(ValueError, "incomplete pass"):
                    validate_keyed_consumer(value)
        value = keyed_result()
        del value["keyedConsumer"]["shape"]
        with self.assertRaisesRegex(ValueError, "dimensions are missing"):
            validate_keyed_consumer(value)

    def test_replay_age_cannot_be_reported_as_delivery_latency(self):
        value = keyed_result()
        value["latency"] = {"count": 100}
        with self.assertRaisesRegex(ValueError, "does not record delivery latency"):
            validate_keyed_consumer(value)

    @unittest.skipUnless(bash_path(), "bash is required for dispatch validation")
    def test_invalid_shape_and_size_fail_before_paid_job(self):
        script = workflow_script("Select lanes for this run").split("# Manual full_run", 1)[0]
        for shape, lane, size, full in (("bad", "consumer-keyed-1b", "128", "false"),
                                      ("binary", "consumer-1b", "128", "false"),
                                      ("binary", "consumer-keyed-1b", "128", "true"),
                                      ("scalar", "consumer-keyed-1b", "7", "false"),
                                      ("scalar", "consumer-keyed-1b", "4097", "false"),
                                      ("scalar", "consumer-keyed-1b", "1+1", "false")):
            with self.subTest(shape=shape, lane=lane, size=size, full=full):
                environment = dict(os.environ, EVENT_NAME="workflow_dispatch", LANE=lane,
                                   KEYED_SHAPE=shape, MESSAGE_SIZE=size, FULL_RUN=full,
                                   FIXED_WORKER_THREADS="0", BASELINE_SHA="")
                run = subprocess.run([bash_path(), "-e", "-o", "pipefail", "-c", script],
                                     env=environment, capture_output=True, encoding="utf-8", timeout=30)
                self.assertNotEqual(0, run.returncode)
                self.assertIn("::error::", run.stdout)

    @unittest.skipUnless(bash_path() and shutil.which("jq"), "bash and jq are required for full lane selection")
    def test_actual_selector_preserves_shapes_and_excludes_full_matrix(self):
        script = 'python3() { "$TEST_PYTHON" "$@"; }\n' + workflow_script("Select lanes for this run")
        for shape, lane, full, baseline in (
                ("scalar", "consumer-keyed-1b", "false", ""),
                ("binary", "consumer-keyed-1b", "false", "a" * 40),
                ("large-distinct", "consumer-keyed-1b", "false", "a" * 40),
                ("large-colliding", "consumer-keyed-1b", "false", "a" * 40),
                ("scalar", "all", "false", ""), ("scalar", "all", "true", "")):
            with self.subTest(shape=shape, full=full), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / ".github/scripts").mkdir(parents=True)
                shutil.copyfile(Path(__file__).with_name("stress_timeout.py"), root / ".github/scripts/stress_timeout.py")
                environment = dict(os.environ, EVENT_NAME="workflow_dispatch", LANE=lane,
                                   KEYED_SHAPE=shape, MESSAGE_SIZE="128", FULL_RUN=full,
                                   FIXED_WORKER_THREADS="0", BASELINE_SHA=baseline,
                                   GITHUB_REF="refs/heads/main", DISPATCH_SHAPE="cheap", PROFILE_MODE="off",
                                   CONSUMER_FETCH_DIAGNOSTICS="false", ADAPTIVE_CONNECTIONS="false",
                                   ABA_SECOND_CANDIDATE="false", DURATION_MINUTES="5",
                                   PRODUCER_WARMUP_SECONDS="180", GITHUB_OUTPUT="output.txt",
                                   TEST_PYTHON=Path(sys.executable).as_posix())
                (root / "workflow.sh").write_text(script, encoding="utf-8", newline="\n")
                run = subprocess.run([bash_path(), "-e", "-o", "pipefail", "workflow.sh"],
                                     cwd=root, env=environment, capture_output=True, encoding="utf-8", timeout=30)
                self.assertEqual(0, run.returncode, run.stdout + run.stderr)
                outputs = dict(line.split("=", 1) for line in (root / "output.txt").read_text().splitlines())
                selected = json.loads(outputs["matrix"])["include"]
                if lane == "all":
                    self.assertEqual(12, len(selected))
                    self.assertNotIn("consumer-keyed-1b", [item["lane"] for item in selected])
                else:
                    self.assertEqual(1, len(selected))
                    self.assertEqual(shape, selected[0]["keyed_shape"])
                    self.assertEqual(baseline, selected[0].get("baseline_sha", ""))
                    self.assertEqual("dekaf", selected[0]["client"])

    def test_lane_is_explicit_and_never_in_default_matrix(self):
        workflow = (Path(__file__).parents[1] / "workflows/stress-tests.yml").read_text()
        lanes = json.loads(re.search(r"cat > lanes.json << 'EOF'\n(.*?)\n\s*EOF", workflow, re.S)[1])
        lane = next(item for item in lanes if item["lane"] == "consumer-keyed-1b")
        self.assertTrue(lane["manual_only"])
        self.assertEqual("dekaf", lane["client"])
        self.assertEqual("consumer-keyed", lane["scenario"])
        self.assertIn('select(($lane == "all" and .manual_only != true) or .lane == $lane)', workflow)
        self.assertIn('consumer-1b|consumer-keyed-1b|consumer-batch-1b', workflow)
        self.assertIn('EXTRA_ARGS+=(--keyed-shape "$KEYED_SHAPE" --keyed-records-per-partition 32768 --partitions 6)', workflow)
        self.assertIn('scalar|binary|large-distinct|large-colliding)', workflow)


if __name__ == "__main__":
    unittest.main()
