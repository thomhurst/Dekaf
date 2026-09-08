import unittest

from stress_timeout import budget


class StressTimeoutTests(unittest.TestCase):
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

    def test_paid_lanes_are_unchanged(self):
        matrix = {"include": [{"lane": "producer-1b", "timeout_minutes": 180}, {"baseline_sha": "", "timeout_minutes": 30}]}
        self.assertEqual(matrix, budget(matrix, 30, 180))

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
