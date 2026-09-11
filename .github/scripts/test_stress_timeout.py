import unittest

from stress_timeout import budget


class StressTimeoutTests(unittest.TestCase):
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
