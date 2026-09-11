import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from uuid import uuid4

from stress_aba import compare
from stress_telemetry import validate_telemetry
from test_stress_aba import result
from test_fixed_worker_pool import bash_path, workflow_script


def subscribed():
    segment = result(scenario="hosted-share-telemetry")
    segment["idempotent"] = True
    segment["shareTelemetry"] = dict(pushIntervalMilliseconds=1000, maximumRetainedPayloads=64,
        maximumPayloadBytes=65536, retainedPayloads=4,
        requestedMetrics=["org.apache.kafka.consumer.share.", "com.example.dekaf.stress.worker"],
        workers=[dict(clientId=f"worker-{index}", clientInstanceId=str(uuid4()), periodicPayloads=1,
                      terminatingPayloads=1, payloadBytes=500, positiveFetch=True,
                      positiveRecords=True, positiveAcknowledgements=True) for index in range(2)])
    return segment


class SubscribedEvidenceTests(unittest.TestCase):
    def test_independent_worker_identities_do_not_change_workload_identity(self):
        self.assertEqual("pass", compare(subscribed(), subscribed(), subscribed())["verdict"])

    def test_subscription_dimensions_must_match(self):
        for field, value in (("pushIntervalMilliseconds", 2000), ("maximumRetainedPayloads", 128),
                             ("maximumPayloadBytes", 131072)):
            with self.subTest(field=field):
                candidate = subscribed()
                candidate["shareTelemetry"][field] = value
                with self.assertRaises(ValueError):
                    compare(subscribed(), candidate, subscribed())

    def test_missing_or_corrupt_evidence_cannot_pass(self):
        for mutation in ("missing", "scenario", "worker", "duplicate-id", "empty-id", "no-periodic", "no-terminating",
                         "duplicate-terminating", "no-fetch", "no-records", "no-acknowledgements", "no-application",
                         "retention", "counts", "bytes", "boolean-count"):
            with self.subTest(mutation=mutation):
                segment = subscribed()
                telemetry = segment["shareTelemetry"]
                worker = telemetry["workers"][0]
                if mutation == "missing": del segment["shareTelemetry"]
                elif mutation == "scenario": segment["scenario"] = "hosted-share"
                elif mutation == "worker": telemetry["workers"].pop()
                elif mutation == "duplicate-id": worker["clientInstanceId"] = telemetry["workers"][1]["clientInstanceId"]
                elif mutation == "empty-id": worker["clientInstanceId"] = "00000000-0000-0000-0000-000000000000"
                elif mutation == "no-periodic": worker["periodicPayloads"] = 0
                elif mutation == "no-terminating": worker["terminatingPayloads"] = 0
                elif mutation == "duplicate-terminating": worker["terminatingPayloads"] = 2
                elif mutation == "no-fetch": worker["positiveFetch"] = False
                elif mutation == "no-records": worker["positiveRecords"] = False
                elif mutation == "no-acknowledgements": worker["positiveAcknowledgements"] = False
                elif mutation == "no-application": telemetry["requestedMetrics"].pop()
                elif mutation == "retention": telemetry["retainedPayloads"] = 65
                elif mutation == "counts": telemetry["retainedPayloads"] = 5
                elif mutation == "bytes": worker["payloadBytes"] = 0
                elif mutation == "boolean-count": worker["periodicPayloads"] = True
                with self.assertRaises(ValueError): validate_telemetry(segment)

    @unittest.skipUnless(bash_path() and shutil.which("jq"), "bash and jq required for actual lane selection")
    def test_actual_selector_keeps_subscription_lane_explicit(self):
        for lane, full, baseline in (("hosted-share-telemetry-1b", False, ""),
                                     ("hosted-share-telemetry-1b", False, "a" * 40),
                                     ("all", False, ""), ("all", True, "")):
            with self.subTest(lane=lane, full=full, baseline=baseline), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / ".github/scripts").mkdir(parents=True)
                shutil.copyfile(Path(__file__).with_name("stress_timeout.py"), root / ".github/scripts/stress_timeout.py")
                script = 'python3() { "$TEST_PYTHON" "$@"; }\n' + workflow_script("Select lanes for this run")
                (root / "workflow.sh").write_text(script, encoding="utf-8", newline="\n")
                environment = dict(os.environ, EVENT_NAME="workflow_dispatch", LANE=lane,
                    FULL_RUN=str(full).lower(), GITHUB_REF="refs/heads/main", BASELINE_SHA=baseline,
                    FIXED_WORKER_THREADS="0", DISPATCH_SHAPE="cheap", PROFILE_MODE="off",
                    CONSUMER_FETCH_DIAGNOSTICS="false", ADAPTIVE_CONNECTIONS="false", ABA_SECOND_CANDIDATE="false",
                    DURATION_MINUTES="5", PRODUCER_WARMUP_SECONDS="180", GITHUB_OUTPUT="output.txt",
                    TEST_PYTHON=Path(sys.executable).as_posix())
                completed = subprocess.run([bash_path(), "-e", "-o", "pipefail", "workflow.sh"], cwd=root,
                    env=environment, capture_output=True, encoding="utf-8", timeout=30)
                self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
                outputs = dict(line.split("=", 1) for line in (root / "output.txt").read_text().splitlines())
                lanes = json.loads(outputs["matrix"])["include"]
                if lane == "all":
                    self.assertNotIn("hosted-share-telemetry-1b", [item["lane"] for item in lanes])
                else:
                    self.assertEqual(1, len(lanes))
                    self.assertEqual("hosted-share-telemetry", lanes[0]["scenario"])
                    self.assertEqual("dekaf", lanes[0]["client"])
                    self.assertEqual(1, lanes[0]["brokers"])
                    self.assertEqual(baseline, lanes[0].get("baseline_sha", ""))
                    if not baseline: self.assertEqual(1, lanes[0]["producer_samples"])


if __name__ == "__main__":
    unittest.main()
