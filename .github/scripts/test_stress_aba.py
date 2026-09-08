import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

from stress_aba import compare, main, markdown


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
    latency_count=10_000,
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
            "count": latency_count,
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
    def test_zero_latency_is_invalid_in_every_segment(self):
        for field in ("p50Us", "p95Us", "p99Us", "maxUs"):
            for segment in range(4):
                segments = [result() for _ in range(4)]
                segments[segment]["latency"][field] = 0
                with self.subTest(field=field, segment=segment), self.assertRaisesRegex(ValueError, "positive latency"):
                    compare(*segments[:3], candidate_b2_result=segments[3])

    def test_latency_sample_count_must_be_a_positive_integer(self):
        for count in (0, -1, None, True, 1.5, float("nan"), "100"):
            for segment in range(4):
                segments = [result() for _ in range(4)]
                segments[segment]["latency"]["count"] = count
                with self.subTest(count=count, segment=segment), self.assertRaisesRegex(ValueError, "sample count"):
                    compare(*segments[:3], candidate_b2_result=segments[3])

    def test_missing_latency_count_is_not_inferred_from_positive_quantiles(self):
        candidate = result()
        del candidate["latency"]["count"]
        with self.assertRaisesRegex(ValueError, "sample count"):
            compare(result(), candidate, result())

    def test_undersampled_latency_is_invalid_in_every_segment(self):
        for count in (1, 100, 1000, 9999):
            for segment in range(4):
                segments = [result() for _ in range(4)]
                segments[segment]["latency"]["count"] = count
                with self.subTest(count=count, segment=segment):
                    with self.assertRaisesRegex(ValueError, "at least 10000 latency samples"):
                        compare(*segments[:3], candidate_b2_result=segments[3])

    def test_records_latency_sample_counts_for_all_segments(self):
        comparison = compare(
            result(latency_count=10000), result(latency_count=20000), result(latency_count=30000),
            candidate_b2_result=result(latency_count=40000),
        )
        self.assertEqual(
            {"baselineA": 10000, "candidateB": 20000, "baselineA2": 30000, "candidateB2": 40000},
            comparison["latencySampleCounts"],
        )
        self.assertIn("Latency samples (A / B / A2 / B2): 10000 / 20000 / 30000 / 40000", markdown(comparison, "a", "b"))
        single_candidate = compare(result(), result(), result())
        self.assertIsNone(single_candidate["latencySampleCounts"]["candidateB2"])
        self.assertIn("Latency samples (A / B / A2): 10000 / 10000 / 10000", markdown(single_candidate, "a", "b"))

    def test_zero_throughput_controls_are_invalid(self):
        for key in ("throughput", "median_throughput"):
            for control in (0, 2):
                segments = [result(), result(), result()]
                segments[control] = result(**{key: 0})
                with self.subTest(key=key, control=control), self.assertRaisesRegex(ValueError, "positive.*throughput"):
                    compare(*segments)

    def test_controls_require_completed_messages_even_with_positive_cached_rates(self):
        for control in (0, 2):
            segments = [result(), result(), result()]
            segments[control]["throughput"]["totalMessages"] = 0
            segments[control]["deliveredMessages"] = 0
            with self.subTest(control=control), self.assertRaisesRegex(ValueError, "completed messages"):
                compare(*segments)

    def test_zero_candidate_throughput_with_valid_controls_is_regression(self):
        self.assertEqual("regression", compare(result(), result(throughput=0), result())["verdict"])

    def test_main_retains_zero_work_controls_as_inconclusive(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for label in ("a", "b", "a2"):
                path = root / label
                path.mkdir()
                payload = {"results": [result(throughput=0)]}
                (path / "stress-test-results.json").write_text(json.dumps(payload), encoding="utf-8")
            output, summary = root / "comparison.json", root / "summary.md"
            with contextlib.redirect_stdout(io.StringIO()):
                exit_code = main([
                    "--baseline-a", str(root / "a"), "--candidate", str(root / "b"),
                    "--baseline-a2", str(root / "a2"), "--baseline-sha", "a" * 40,
                    "--candidate-sha", "b" * 40, "--output", str(output),
                    "--summary", str(summary),
                ])
            self.assertEqual(1, exit_code)
            comparison = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual("inconclusive", comparison["verdict"])
            self.assertIn("positive", comparison["validationError"])
            self.assertIn("Verdict: INCONCLUSIVE", summary.read_text(encoding="utf-8"))

    def test_zero_allocation_controls_are_valid(self):
        for allocation, verdict in ((0, "pass"), (0.5, "pass"), (2, "regression")):
            with self.subTest(allocation=allocation):
                comparison = compare(
                    result(allocation=0), result(allocation=allocation), result(allocation=0)
                )
                self.assertEqual(verdict, comparison["verdict"])
                json.dumps(comparison, allow_nan=False)
                self.assertIn("Allocation", markdown(comparison, "a" * 40, "b" * 40))

    def test_maximum_latency_regression_is_gated(self):
        comparison = compare(result(), result(maximum=60), result())
        self.assertEqual("regression", comparison["verdict"])
        maximum = next(metric for metric in comparison["metrics"] if metric["key"] == "max")
        self.assertTrue(maximum["gated"])

    def test_control_mean_cannot_hide_loss_against_one_control(self):
        for a, b, a2 in (
            (result(p99=100), result(p99=104), result(p99=104)),
            (result(throughput=100), result(throughput=96), result(throughput=96)),
        ):
            with self.subTest(candidate=b):
                self.assertEqual("inconclusive", compare(a, b, a2)["verdict"])

    def test_candidate_mean_cannot_hide_loss_in_one_segment(self):
        comparison = compare(
            result(p99=100), result(p99=98), result(p99=100),
            candidate_b2_result=result(p99=104),
        )
        self.assertEqual("inconclusive", comparison["verdict"])

    def test_candidate_drift_cannot_pass_even_when_both_candidates_improve(self):
        comparison = compare(
            result(p99=100), result(p99=80), result(p99=100),
            candidate_b2_result=result(p99=60),
        )
        self.assertEqual("inconclusive", comparison["verdict"])

    def test_records_each_candidate_delta_against_each_control(self):
        comparison = compare(
            result(p99=100), result(p99=102), result(p99=104),
            candidate_b2_result=result(p99=101),
        )
        p99 = next(metric for metric in comparison["metrics"] if metric["key"] == "p99")
        self.assertAlmostEqual(2, p99["deltaVsBaselineAPercent"])
        self.assertAlmostEqual(-100 * 2 / 104, p99["deltaVsBaselineA2Percent"])
        self.assertAlmostEqual(1, p99["b2DeltaVsBaselineAPercent"])
        self.assertAlmostEqual(-100 * 3 / 104, p99["b2DeltaVsBaselineA2Percent"])
        report = markdown(comparison, "a" * 40, "b" * 40)
        for column in ("B vs A", "B vs A2", "B2 vs A", "B2 vs A2"):
            self.assertIn(column, report)

    def test_nonfinite_thresholds_are_rejected(self):
        for value in (float("nan"), float("inf"), True):
            for name in ("tolerance_percent", "max_control_drift_percent"):
                with self.subTest(value=value, name=name), self.assertRaises(ValueError):
                    compare(result(), result(), result(), **{name: value})

    def test_negative_metrics_are_rejected(self):
        for option in ("throughput", "p99", "maximum", "cpu", "allocation"):
            with self.subTest(option=option), self.assertRaisesRegex(ValueError, "nonnegative"):
                compare(result(), result(**{option: -1}), result())

    def test_zero_control_deltas_are_explicit_and_finite(self):
        comparison = compare(result(allocation=0), result(allocation=2), result(allocation=0))
        alloc = next(metric for metric in comparison["metrics"] if metric["key"] == "alloc")
        self.assertIsNone(alloc["deltaVsBaselineAPercent"])
        self.assertIsNone(alloc["deltaVsBaselineA2Percent"])
        self.assertIsNone(alloc["deltaPercent"])
        self.assertEqual(0, alloc["controlDriftPercent"])
        report = markdown(comparison, "a" * 40, "b" * 40)
        self.assertIn("| 0.000 | 2.000 | 0.000 | n/a | n/a |", report)

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

    def test_sub_byte_allocation_control_drift_is_not_inconclusive(self):
        # Runs 33961873612 (0.527 / 0.542 / 0.620 B/msg) and 33966278396
        # (0.866 / 0.649 / 0.619 B/msg): 16-33% relative control drift at sub-byte scale on
        # a 0 B/msg hot path ruled otherwise-passing comparisons inconclusive.
        for baseline_a, candidate, baseline_a2 in (
            (0.527, 0.542, 0.620),
            (0.866, 0.649, 0.619),
        ):
            with self.subTest(baseline_a=baseline_a, candidate=candidate):
                comparison = compare(
                    result(allocation=baseline_a),
                    result(allocation=candidate),
                    result(allocation=baseline_a2),
                )

                self.assertEqual("pass", comparison["verdict"])
                alloc = next(
                    metric for metric in comparison["metrics"] if metric["key"] == "alloc"
                )
                self.assertEqual("pass", alloc["status"])
                self.assertGreater(alloc["controlDriftPercent"], 10.0)
                self.assertEqual(1.0, alloc["noiseFloor"])

    def test_sub_byte_allocation_candidate_delta_is_not_a_regression(self):
        # +50% relative but +0.3 B/msg absolute: below what the metric can resolve.
        comparison = compare(
            result(allocation=0.6), result(allocation=0.9), result(allocation=0.6)
        )

        self.assertEqual("pass", comparison["verdict"])

    def test_allocation_regression_above_noise_floor_still_rejected(self):
        # +2 B/msg above both controls is a real per-message allocation, floor or not.
        comparison = compare(
            result(allocation=0.6), result(allocation=2.7), result(allocation=0.6)
        )

        self.assertEqual("regression", comparison["verdict"])
        alloc = next(
            metric for metric in comparison["metrics"] if metric["key"] == "alloc"
        )
        self.assertEqual("regression", alloc["status"])

    def test_allocation_percentage_gates_unchanged_at_consumer_scale(self):
        # ~2 KB/msg string consumers: 4% over the control mean is 80 B/msg, far above the
        # floor, so the percentage gates decide exactly as before.
        comparison = compare(
            result(allocation=2024.0),
            result(allocation=2105.0),
            result(allocation=2024.0),
        )

        self.assertEqual("regression", comparison["verdict"])
        noisy = compare(
            result(allocation=1900.0),
            result(allocation=2000.0),
            result(allocation=2150.0),
        )
        alloc = next(metric for metric in noisy["metrics"] if metric["key"] == "alloc")
        self.assertEqual("inconclusive", alloc["status"])

    def test_four_segments_retain_descriptive_mean_and_pass(self):
        comparison = compare(
            result(100), result(104), result(100), candidate_b2_result=result(102)
        )

        self.assertEqual("pass", comparison["verdict"])
        self.assertEqual(2, comparison["candidateSegments"])
        throughput = next(
            metric for metric in comparison["metrics"] if metric["key"] == "throughput"
        )
        self.assertAlmostEqual(103.0, throughput["candidate"])
        self.assertAlmostEqual(104.0, throughput["candidateB"])
        self.assertAlmostEqual(102.0, throughput["candidateB2"])
        self.assertGreater(throughput["candidateDriftPercent"], 1.0)

    def test_four_segments_candidate_drift_is_inconclusive_like_control_drift(self):
        # Candidates disagree while both controls agree; their mean cannot pass the gate.
        comparison = compare(
            result(100), result(95), result(100), candidate_b2_result=result(116)
        )

        throughput = next(
            metric for metric in comparison["metrics"] if metric["key"] == "throughput"
        )
        self.assertEqual("inconclusive", throughput["status"])
        self.assertEqual("inconclusive", comparison["verdict"])

    def test_four_segments_need_both_candidates_to_pass(self):
        both_better = compare(
            result(p99=15.0), result(p99=12.0), result(p99=16.0), candidate_b2_result=result(p99=13.0)
        )
        p99 = next(metric for metric in both_better["metrics"] if metric["key"] == "p99")
        self.assertEqual("pass", p99["status"])

        one_worse = compare(
            result(p99=15.0), result(p99=12.0), result(p99=16.0), candidate_b2_result=result(p99=17.0)
        )
        p99 = next(metric for metric in one_worse["metrics"] if metric["key"] == "p99")
        self.assertNotEqual("pass", p99["status"])

    def test_four_segments_second_candidate_stability_breach_is_regression(self):
        comparison = compare(
            result(100), result(100), result(100), candidate_b2_result=result(100, stability=0.7)
        )

        self.assertEqual("regression", comparison["verdict"])

    def test_four_segments_markdown_shows_both_candidates(self):
        comparison = compare(
            result(100), result(104), result(100), candidate_b2_result=result(102)
        )
        report = markdown(comparison, "a" * 40, "b" * 40)

        self.assertIn("four segments (A-B-A-B)", report)
        self.assertIn("| Candidate B2 |", report)
        self.assertIn("Candidate drift", report)

    def test_candidate_beating_both_noisy_controls_remains_inconclusive(self):
        # Improvement against noisy controls does not establish a valid experiment.
        comparison = compare(
            result(p99=17.25),
            result(p99=14.85),
            result(p99=15.25),
        )

        self.assertEqual("inconclusive", comparison["verdict"])
        p99 = next(
            metric for metric in comparison["metrics"] if metric["key"] == "p99"
        )
        self.assertEqual("inconclusive", p99["status"])
        self.assertGreater(p99["controlDriftPercent"], 10.0)

    def test_bracketed_candidate_with_noisy_controls_stays_inconclusive(self):
        comparison = compare(result(p99=13.0), result(p99=15.0), result(p99=17.0))

        self.assertEqual("inconclusive", comparison["verdict"])
        p99 = next(
            metric for metric in comparison["metrics"] if metric["key"] == "p99"
        )
        self.assertEqual("inconclusive", p99["status"])

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
            self.assertIn("not full PR performance acceptance", summary.read_text(encoding="utf-8"))

    def test_main_retains_invalid_evidence_reason_in_both_outputs(self):
        missing_metric = result()
        del missing_metric["latency"]["maxUs"]
        empty_latency = result(p50=0, p95=0, p99=0, maximum=0, latency_count=0)
        for invalid, expected in (
            (None, "found 0"),
            ("not json", "Expecting value"),
            (json.dumps({"results": [None]}), "Expected a result object"),
            (json.dumps({"results": [[]]}), "Expected a result object"),
            (json.dumps({"results": [missing_metric]}), "Latency max"),
            (json.dumps({"results": [empty_latency]}), "positive latency"),
            (json.dumps({"results": [result(latency_count=0)]}), "sample count"),
            (json.dumps({"results": [result(latency_count=1)]}), "at least 10000 latency samples"),
            (json.dumps({"results": [{**result(), "latency": ["invalid"]}]}), "latency object"),
            (json.dumps({"results": [{**result(), "throughput": ["invalid"]}]}), "throughput object"),
            (json.dumps({"results": [{**result(), "producerDeliveryDiagnostics": ["invalid"]}]}), "producerDeliveryDiagnostics object"),
            (json.dumps({"results": [result(scenario="other")]}), "workload identity"),
        ):
            with self.subTest(expected=expected), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for label in ("a", "b", "a2"):
                    path = root / label
                    path.mkdir()
                    payload = invalid if label == "b" else json.dumps({"results": [result()]})
                    if payload is not None:
                        (path / "stress-test-results.json").write_text(payload, encoding="utf-8")
                output = root / "comparison.json"
                summary = root / "summary.md"
                with contextlib.redirect_stdout(io.StringIO()):
                    exit_code = main([
                        "--baseline-a", str(root / "a"), "--candidate", str(root / "b"),
                        "--baseline-a2", str(root / "a2"), "--baseline-sha", "a" * 40,
                        "--candidate-sha", "b" * 40, "--output", str(output),
                        "--summary", str(summary),
                    ])
                comparison = json.loads(output.read_text(encoding="utf-8"))
                self.assertEqual(1, exit_code)
                self.assertEqual("inconclusive", comparison["verdict"])
                self.assertIn(expected, comparison["validationError"])
                self.assertIn(expected, summary.read_text(encoding="utf-8"))

    def test_nested_result_objects_are_validated_in_every_segment(self):
        for field in ("throughput", "producerDeliveryDiagnostics"):
            for value in (None, [], ["invalid"], "invalid", 1, False):
                for segment in range(4):
                    segments = [result() for _ in range(4)]
                    segments[segment][field] = value
                    with self.subTest(field=field, value=value, segment=segment):
                        with self.assertRaisesRegex(ValueError, f"{field} object"):
                            compare(*segments[:3], candidate_b2_result=segments[3])

    def test_report_does_not_prescribe_unlimited_repeats(self):
        report = markdown(compare(result(90), result(100), result(110)), "a" * 40, "b" * 40)
        self.assertIn("first INCONCLUSIVE permits one exact repeat", report)
        self.assertIn("do not automatically repeat again", report)


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
        self.assertIn('.timeout_minutes = $segment_budget * $aba_segments', self.workflow)
        self.assertIn('.aba_second_candidate = ($aba_segments == 4)', self.workflow)
        self.assertIn('aba_second_candidate:', self.workflow)

    def test_runs_baseline_candidate_baseline_in_that_order(self):
        first = self.workflow.index("run_aba \\\n              baseline-a \\")
        candidate = self.workflow.index("run_aba \\\n              candidate-b \\")
        second = self.workflow.index("run_aba \\\n              baseline-a2 \\")
        fourth = self.workflow.index("run_aba \\\n                candidate-b2 \\")
        self.assertLess(first, candidate)
        self.assertLess(candidate, second)
        self.assertLess(second, fourth)
        self.assertIn('if [ "${{ matrix.aba_second_candidate }}" = "true" ]; then', self.workflow)

    def test_compares_and_uploads_controls_separately(self):
        self.assertIn("python3 .github/scripts/stress_aba.py", self.workflow)
        self.assertIn("--candidate tools/Dekaf.StressTests/results", self.workflow)
        self.assertIn("extra+=(--candidate-b2 aba-results/candidate-b2)", self.workflow)
        self.assertIn("name: aba-controls-", self.workflow)
        self.assertIn("path: aba-results/", self.workflow)


if __name__ == "__main__":
    unittest.main()
