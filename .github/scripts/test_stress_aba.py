import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

from stress_aba import compare, main


REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "stress-tests.yml"


def result(
    throughput=100.0,
    median_throughput=None,
    p50=7.0,
    p95=10.0,
    p99=15.0,
    maximum=50.0,
    cpu=0.8,
    allocation=1.2,
    stability=0.95,
    errors=0,
    delivered=None,
    scenario="producer",
):
    total_messages = 1_000_000
    if delivered is None:
        delivered = total_messages
    return {
        "scenario": scenario,
        "client": "Dekaf",
        "brokerCount": 1,
        "durationMinutes": 15,
        "messageSizeBytes": 1000,
        "deliveryLatencyTargetMs": 10,
        "idempotent": False,
        "roundTripSteadySeconds": None,
        "effectiveMessagesPerSecond": throughput,
        "medianIntervalMessagesPerSecond": (
            throughput if median_throughput is None else median_throughput
        ),
        "latency": {
            "p50Us": p50 * 1000,
            "p95Us": p95 * 1000,
            "p99Us": p99 * 1000,
            "maxUs": maximum * 1000,
        },
        "cpuMicrosPerMessage": cpu,
        "allocatedBytesPerMessage": allocation,
        "steadyStatePeakRatio": stability,
        "steadyStatePeakRatioThreshold": 0.85,
        "steadyStatePeakThresholdBreached": stability < 0.85,
        "throughput": {
            "totalMessages": total_messages,
            "totalErrors": errors,
            "totalDeliveryErrors": 0,
        },
        "deliveredMessages": delivered,
        "producerDeliveryDiagnostics": {
            "brokerProduceRequests": [
                {
                    "requestCount": 1000,
                    "averageRequestBytes": 1024 * 900,
                }
            ]
        },
    }


class StressAbaComparisonTests(unittest.TestCase):
    def test_passes_pareto_safe_candidate(self):
        comparison = compare(
            result(throughput=100),
            result(throughput=102, p50=6.8, p95=9.8, p99=14.8, cpu=0.79),
            result(throughput=101),
        )

        self.assertEqual("pass", comparison["verdict"])
        self.assertTrue(
            all(
                metric["status"] in {"pass", "recorded"}
                for metric in comparison["metrics"]
            )
        )

    def test_rejects_candidate_worse_than_both_controls(self):
        comparison = compare(result(100), result(90), result(102))

        self.assertEqual("regression", comparison["verdict"])
        throughput = next(
            metric for metric in comparison["metrics"] if metric["key"] == "throughput"
        )
        self.assertEqual("regression", throughput["status"])

    def test_marks_large_control_drift_inconclusive(self):
        comparison = compare(result(90), result(100), result(110))

        self.assertEqual("inconclusive", comparison["verdict"])
        throughput = next(
            metric for metric in comparison["metrics"] if metric["key"] == "throughput"
        )
        self.assertEqual("inconclusive", throughput["status"])
        self.assertAlmostEqual(20.0, throughput["controlDriftPercent"])

    def test_decisive_regression_overrides_noisy_controls(self):
        comparison = compare(result(100), result(70), result(130))

        self.assertEqual("regression", comparison["verdict"])
        throughput = next(
            metric for metric in comparison["metrics"] if metric["key"] == "throughput"
        )
        self.assertEqual("regression", throughput["status"])

    def test_stability_gates_on_absolute_floor_not_shape_comparison(self):
        # Run 29518014693: candidate beat both controls on throughput yet its
        # steady/peak shape sat >3% under the control mean purely because its
        # adaptive window front-loaded throughput. The floor gate accepts it.
        comparison = compare(
            result(throughput=1172, stability=0.951),
            result(throughput=1211, stability=0.888, cpu=0.78),
            result(throughput=1185, stability=0.906),
        )

        self.assertEqual("pass", comparison["verdict"])
        stability = next(
            metric for metric in comparison["metrics"] if metric["key"] == "stability"
        )
        self.assertEqual("pass", stability["status"])

    def test_candidate_stability_floor_breach_forces_regression(self):
        # Run 29513570135: genuine intra-run throughput collapse (0.745) must
        # still reject the candidate.
        comparison = compare(
            result(stability=0.994),
            result(stability=0.745),
            result(stability=0.996),
        )

        self.assertEqual("regression", comparison["verdict"])
        stability = next(
            metric for metric in comparison["metrics"] if metric["key"] == "stability"
        )
        self.assertEqual("regression", stability["status"])

    def test_unstable_control_marks_stability_inconclusive(self):
        comparison = compare(
            result(stability=0.70),
            result(stability=0.95),
            result(stability=0.95),
        )

        self.assertEqual("inconclusive", comparison["verdict"])
        stability = next(
            metric for metric in comparison["metrics"] if metric["key"] == "stability"
        )
        self.assertEqual("inconclusive", stability["status"])

    def test_stability_floor_falls_back_to_ratio_when_flag_missing(self):
        candidate = result(stability=0.74)
        del candidate["steadyStatePeakThresholdBreached"]

        comparison = compare(result(), candidate, result())

        self.assertEqual("regression", comparison["verdict"])

    def test_candidate_errors_force_regression(self):
        comparison = compare(result(), result(errors=1), result())

        self.assertEqual("regression", comparison["verdict"])
        self.assertEqual(1, comparison["candidateErrors"])

    def test_candidate_delivery_mismatch_forces_regression(self):
        comparison = compare(result(), result(delivered=999_999), result())

        self.assertEqual("regression", comparison["verdict"])
        self.assertTrue(comparison["candidateDeliveryMismatch"])

    def test_rejects_mismatched_workload_identity(self):
        with self.assertRaisesRegex(ValueError, "same workload identity"):
            compare(result(), result(scenario="producer-acks-all"), result())

    def test_rejects_negative_thresholds(self):
        with self.assertRaisesRegex(ValueError, "cannot be negative"):
            compare(result(), result(), result(), tolerance_percent=-1)

    def test_rejects_missing_latency_metric(self):
        candidate = result()
        del candidate["latency"]["p99Us"]

        with self.assertRaisesRegex(ValueError, "Latency p99"):
            compare(result(), candidate, result())

    def test_main_writes_machine_and_markdown_outputs(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            paths = [root / name for name in ("a", "b", "a2")]
            for path, item in zip(
                paths,
                (result(100), result(101), result(100)),
                strict=True,
            ):
                path.mkdir()
                (path / "stress-test-results.json").write_text(
                    json.dumps({"results": [item]}), encoding="utf-8"
                )
            output = root / "comparison.json"
            summary = root / "summary.md"

            with contextlib.redirect_stdout(io.StringIO()):
                exit_code = main(
                    [
                        "--baseline-a",
                        str(paths[0]),
                        "--candidate",
                        str(paths[1]),
                        "--baseline-a2",
                        str(paths[2]),
                        "--baseline-sha",
                        "a" * 40,
                        "--candidate-sha",
                        "b" * 40,
                        "--output",
                        str(output),
                        "--summary",
                        str(summary),
                    ]
                )

            self.assertEqual(0, exit_code)
            self.assertEqual("pass", json.loads(output.read_text())["verdict"])
            self.assertIn("Verdict: PASS", summary.read_text(encoding="utf-8"))


class StressAbaWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = WORKFLOW.read_text(encoding="utf-8")

    def test_requires_exact_sha_and_one_unprofiled_lane(self):
        self.assertIn("baseline_sha:", self.workflow)
        self.assertIn('^[0-9a-fA-F]{40}$', self.workflow)
        self.assertIn(
            'if [ "$lane" = "all" ]; then\n'
            '              echo "::error::Exact-SHA A-B-A requires one explicit lane.',
            self.workflow,
        )
        self.assertIn(
            "Exact-SHA A-B-A and profiling are separate measurement modes",
            self.workflow,
        )
        self.assertIn(
            "Exact-SHA A-B-A requires a duration-based producer lane",
            self.workflow,
        )

    def test_matrix_forces_three_single_connection_dekaf_segments(self):
        self.assertIn('.baseline_sha = $baseline_sha', self.workflow)
        self.assertIn('.client = "dekaf"', self.workflow)
        self.assertIn('.paired_samples = 1', self.workflow)
        self.assertIn('| drop_3conn', self.workflow)
        self.assertIn('.timeout_minutes = $segment_budget * 3', self.workflow)

    def test_runs_baseline_candidate_baseline_in_that_order(self):
        first = self.workflow.index("run_aba \\\n              baseline-a \\")
        candidate = self.workflow.index("run_aba \\\n              candidate-b \\")
        second = self.workflow.index("run_aba \\\n              baseline-a2 \\")
        self.assertLess(first, candidate)
        self.assertLess(candidate, second)

    def test_compares_and_uploads_controls_separately(self):
        self.assertIn("python3 .github/scripts/stress_aba.py", self.workflow)
        self.assertIn("--candidate tools/Dekaf.StressTests/results", self.workflow)
        self.assertIn("name: aba-controls-", self.workflow)
        self.assertIn("path: aba-results/", self.workflow)


if __name__ == "__main__":
    unittest.main()
