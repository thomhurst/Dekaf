import json
import re
import shutil
import subprocess
import unittest
from pathlib import Path

from stress_timeout import budget


class StressTimeoutTests(unittest.TestCase):
    @unittest.skipUnless(shutil.which("jq"), "jq is required to exercise workflow lane selection")
    def test_workflow_outbox_budget_includes_aba_scaling(self):
        workflow = (Path(__file__).resolve().parents[1] / "workflows/stress-tests.yml").read_text(encoding="utf-8")
        lanes = re.search(r"cat > lanes\.json << 'EOF'\n(?P<body>.*?)\n\s*EOF\n", workflow, re.DOTALL)
        selection = re.search(r"matrix=\$\(jq -c .*? '\n(?P<filter>.*?)' lanes\.json\)", workflow, re.DOTALL)
        self.assertIsNotNone(lanes)
        self.assertIsNotNone(selection)
        for baseline, segments, duration, warmup, expected in (
            ("", 3, 15, 3600, 154),
            ("a" * 40, 3, 15, 900, 230),
            ("a" * 40, 4, 15, 900, 278),
            ("a" * 40, 4, 5, 180, 120),
            ("a" * 40, 4, 15, 999999, None),
        ):
            with self.subTest(baseline=bool(baseline), segments=segments, warmup=warmup):
                selected = subprocess.run(
                    ["jq", "-c", "--arg", "keyed_shape", "scalar", "--arg", "lane", "outbox-1b", "--arg", "client", "dekaf",
                     "--arg", "shape", "cheap", "--arg", "profile_mode", "off",
                     "--arg", "baseline_sha", baseline, "--argjson", "aba_segments", str(segments),
                     selection.group("filter")],
                    input=lanes.group("body"), encoding="utf-8", capture_output=True, check=True, timeout=30,
                )
                matrix = json.loads(selected.stdout)
                if expected is None:
                    with self.assertRaisesRegex(ValueError, "360-minute"):
                        budget(matrix, duration, warmup)
                else:
                    lane = budget(matrix, duration, warmup)["include"][0]
                    self.assertEqual(expected, lane["timeout_minutes"])
                    self.assertTrue(lane["manual_only"])
                    if baseline:
                        self.assertEqual(segments == 4, lane["aba_second_candidate"])
                    else:
                        self.assertEqual(1, lane["producer_samples"])

    def test_regular_outbox_budgets_both_warmups_without_doubling_result_count(self):
        for paired, expected in ((1, 154), (2, 292)):
            with self.subTest(paired=paired):
                lane = dict(scenario="outbox", client="dekaf", paired_samples=paired, timeout_minutes=120)
                actual = budget({"include": [lane]}, 15, 3600)["include"][0]
                self.assertEqual(expected, actual["timeout_minutes"])
                self.assertEqual(paired, actual["producer_samples"])

    def test_outbox_aba_budgets_two_warmups_for_each_sample_and_both_preflights(self):
        for second_candidate, expected in ((False, 230), (True, 278)):
            with self.subTest(second_candidate=second_candidate):
                lane = dict(scenario="outbox", baseline_sha="a" * 40,
                            aba_second_candidate=second_candidate, timeout_minutes=120)
                actual = budget({"include": [lane]}, 15, 900)["include"][0]
                self.assertEqual(expected, actual["timeout_minutes"])
                self.assertNotIn("producer_samples", actual)

    def test_outbox_preserves_sufficient_existing_budget(self):
        lane = dict(scenario="outbox", baseline_sha="a" * 40, timeout_minutes=120)
        self.assertEqual(120, budget({"include": [lane]}, 5, 180)["include"][0]["timeout_minutes"])

    def test_outbox_rejects_excessive_regular_and_aba_budgets(self):
        for baseline in ("", "a" * 40):
            with self.subTest(baseline=baseline):
                lane = dict(scenario="outbox", client="dekaf", baseline_sha=baseline, timeout_minutes=120)
                with self.assertRaisesRegex(ValueError, "360-minute"):
                    budget({"include": [lane]}, 15, 999999)

    def test_outbox_accepts_hosted_limit_and_rejects_the_next_minute(self):
        for warmup, expected in ((1680, 360), (1686, None)):
            with self.subTest(warmup=warmup):
                lane = dict(scenario="outbox", baseline_sha="a" * 40, timeout_minutes=120)
                if expected is None:
                    with self.assertRaisesRegex(ValueError, "360-minute"):
                        budget({"include": [lane]}, 15, warmup)
                else:
                    self.assertEqual(expected, budget({"include": [lane]}, 15, warmup)["include"][0]["timeout_minutes"])

    def test_workflow_outbox_lane_enables_regular_runtime_validation(self):
        workflow = (Path(__file__).resolve().parents[1] / "workflows/stress-tests.yml").read_text(encoding="utf-8")
        heredoc = re.search(r"cat > lanes\.json << 'EOF'\n(?P<body>.*?)\n\s*EOF\n", workflow, re.DOTALL)
        self.assertIsNotNone(heredoc)
        lane = next(lane for lane in json.loads(heredoc.group("body")) if lane["lane"] == "outbox-1b")
        self.assertEqual("dekaf", lane["client"])
        self.assertTrue(lane["manual_only"])
        actual = budget({"include": [lane]}, 15, 180)["include"][0]
        self.assertEqual(1, actual["producer_samples"])
        step = workflow.split("      - name: Validate regular workload warmup and runtime coverage\n", 1)[1]
        step = step.split("      - name:", 1)[0]
        self.assertIn("if: always() && matrix.baseline_sha == '' && matrix.producer_samples > 0", step)
        self.assertIn("python3 .github/scripts/stress_warmup.py", step)
        self.assertIn('--expected-results "${{ matrix.producer_samples }}"', step)

    def test_regular_producers_budget_every_selected_sample(self):
        for client, paired, control, adaptive, count in (
            ("dekaf", 1, False, False, 1),
            ("dekaf", 1, True, False, 2),
            ("dekaf", 1, True, True, 3),
            ("all", 2, True, True, 6),
            ("all", 2, False, False, 4),
        ):
            with self.subTest(client=client, paired=paired, control=control, adaptive=adaptive):
                lane = dict(scenario="producer", client=client, paired_samples=paired,
                            run_3conn=control, run_adaptive=adaptive, timeout_minutes=30)
                actual = budget({"include": [lane]}, 30, 180)["include"][0]
                self.assertGreaterEqual(actual["timeout_minutes"], count * 36.5 + 15)
                self.assertEqual(count, actual["producer_samples"])

    def test_adaptive_override_does_not_expect_skipped_variant(self):
        lane = dict(scenario="producer-idempotent", client="dekaf", run_adaptive=True, timeout_minutes=30)
        actual = budget({"include": [lane]}, 30, 180, adaptive_connections=True)["include"][0]
        self.assertEqual(1, actual["producer_samples"])
        self.assertEqual(52, actual["timeout_minutes"])

    def test_other_scenarios_keep_their_own_budget(self):
        for scenario in ("producer-transactional", "producer-roundtrip-steady"):
            lane = dict(scenario=scenario, timeout_minutes=90)
            expected = lane.copy()
            self.assertEqual(expected, budget({"include": [lane]}, 30, 180)["include"][0])

    def test_consumer_samples_include_declared_warmup(self):
        for scenario, client, paired, count in (
            ("consumer", "all", 2, 4), ("consumer-batch", "dekaf", 1, 1),
            ("consumer-raw", "dekaf", 1, 1), ("consumer-raw-batch", "dekaf", 1, 1),
            ("hosted-share", "dekaf", 1, 1),
        ):
            with self.subTest(scenario=scenario):
                lane = dict(scenario=scenario, client=client, paired_samples=paired, timeout_minutes=60)
                actual = budget({"include": [lane]}, 15, 3600)["include"][0]
                self.assertGreaterEqual(actual["timeout_minutes"], count * 78.5 + 15)
                self.assertEqual(count, actual["producer_samples"])
        with self.assertRaisesRegex(ValueError, "360-minute"):
            budget({"include": [dict(scenario="consumer", client="all", timeout_minutes=90)]}, 15, 999999)

    def test_regular_producer_cannot_exceed_hosted_limit(self):
        lane = dict(scenario="producer", client="all", paired_samples=2, timeout_minutes=180)
        with self.assertRaisesRegex(ValueError, "360-minute"):
            budget({"include": [lane]}, 30, 999999)

    def test_thirty_minute_aba_includes_warmup_validation_and_drain(self):
        matrix = {"include": [{"baseline_sha": "a" * 40, "timeout_minutes": 90}]}
        self.assertEqual(140, budget(matrix, 30, 180)["include"][0]["timeout_minutes"])

    def test_second_candidate_has_its_own_warmup_and_drain_budget(self):
        matrix = {"include": [{"baseline_sha": "a" * 40, "aba_second_candidate": True, "timeout_minutes": 120}]}
        self.assertEqual(176, budget(matrix, 30, 180)["include"][0]["timeout_minutes"])

    def test_requested_duration_and_warmup_scale_budget(self):
        matrix = {"include": [{"baseline_sha": "a" * 40, "timeout_minutes": 90}]}
        self.assertEqual(175, budget(matrix, 30, 600)["include"][0]["timeout_minutes"])

    def test_existing_larger_budget_is_preserved(self):
        matrix = {"include": [{"baseline_sha": "a" * 40, "timeout_minutes": 180}]}
        self.assertEqual(180, budget(matrix, 30, 180)["include"][0]["timeout_minutes"])

    def test_existing_scheduled_budget_is_preserved_when_sufficient(self):
        lane = dict(scenario="producer", client="all", paired_samples=2,
                    run_3conn=True, run_adaptive=True, timeout_minutes=180)
        actual = budget({"include": [lane]}, 15, 180)["include"][0]
        self.assertEqual(180, actual["timeout_minutes"])
        self.assertEqual(6, actual["producer_samples"])

    def test_impossible_hosted_job_is_rejected(self):
        for duration, warmup in ((120, 180), (30, 999999)):
            with self.subTest(duration=duration, warmup=warmup):
                with self.assertRaisesRegex(ValueError, "360-minute"):
                    budget({"include": [{"baseline_sha": "a" * 40, "timeout_minutes": 90}]}, duration, warmup)

    def test_invalid_duration_or_warmup_is_rejected(self):
        for duration, warmup in ((0, 180), (-1, 180), (float("nan"), 180), (float("inf"), 180), (30, 19)):
            with self.subTest(duration=duration, warmup=warmup):
                with self.assertRaises(ValueError):
                    budget({"include": [{"baseline_sha": "a" * 40, "timeout_minutes": 90}]}, duration, warmup)
