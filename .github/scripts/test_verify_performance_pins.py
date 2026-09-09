import json
from pathlib import Path
import re
import subprocess
import unittest
from unittest.mock import patch

import verify_performance_pins as pins


NOW = 1_800_000_000


class PinTests(unittest.TestCase):
    def setUp(self):
        self.harness, self.baseline, self.candidate = 'a' * 40, 'b' * 40, 'c' * 40
        self.main = self.baseline
        self.head = self.candidate
        self.state = 'open'
        self.checked_out = self.harness
        self.ancestor_of_candidate = True
        self.baseline_on_main = True
        self.baseline_committed = NOW - 3 * 86400
        self.main_ahead = 0
        self.calls = []

    def command(self, *args):
        self.calls.append(args)
        if args == ('git', 'rev-parse', 'HEAD'):
            return self.checked_out
        if args == ('git', 'rev-parse', 'FETCH_HEAD'):
            return self.main
        if args[:3] == ('git', 'rev-parse', '--verify'):
            return args[3].removesuffix('^{commit}')
        if args[:3] == ('git', 'merge-base', '--is-ancestor'):
            on_main = args[4] == self.main
            if (on_main and not self.baseline_on_main) or (not on_main and not self.ancestor_of_candidate):
                raise subprocess.CalledProcessError(1, args)
            return ''
        if args[:2] == ('git', 'show'):
            return str(self.baseline_committed)
        if args[:3] == ('git', 'rev-list', '--count'):
            return str(self.main_ahead)
        if args[:2] == ('gh', 'api'):
            return json.dumps({'state': self.state, 'head': {'sha': self.head}})
        return ''

    def verify(self, suite='admin'):
        with patch.object(pins, 'command', side_effect=self.command):
            return pins.verify(self.harness, self.baseline, self.candidate, 3128, suite, 'thomhurst/Dekaf', now=NOW)

    def test_focused_recovery_suites_reject_unrelated_products(self):
        for suite in ('micro-completion', 'outbox-recovery'):
            with self.assertRaisesRegex(ValueError, 'covers PR'):
                self.verify(suite)
        self.assertEqual(self.calls, [])

    def test_outbox_commit_recovery_still_verifies_exact_product_pins(self):
        with patch.object(pins, 'command', side_effect=self.command):
            result = pins.verify(self.harness, self.baseline, self.candidate, 3171,
                                 'outbox-recovery', 'thomhurst/Dekaf', now=NOW)
        self.assertEqual('VERIFIED', result['status'])
        self.assertIn(('gh', 'api', 'repos/thomhurst/Dekaf/pulls/3171'), self.calls)

    def test_distinct_harness_and_product_pins_are_retained(self):
        result = self.verify()
        self.assertEqual(result['harness_sha'], self.harness)
        self.assertEqual(result['candidate_sha'], self.candidate)
        self.assertEqual(result['status'], 'VERIFIED')
        self.assertEqual(result['main_commits_after_baseline'], 0)
        self.assertEqual(result['baseline_age_days'], 3.0)

    def test_moving_names_and_abbreviated_shas_fail_before_external_commands(self):
        for value in ('main', 'abc123', 'A' * 40, '-option'):
            with self.subTest(value=value):
                self.harness = value
                with self.assertRaises(ValueError):
                    self.verify()
        self.assertEqual(self.calls, [])

    def test_wrong_checkout_and_changed_pr_head_are_rejected(self):
        for attribute in ('checked_out', 'head'):
            with self.subTest(attribute=attribute):
                old = getattr(self, attribute)
                setattr(self, attribute, 'd' * 40)
                with self.assertRaises(ValueError):
                    self.verify()
                setattr(self, attribute, old)

    def test_main_moving_past_a_recent_baseline_keeps_the_campaign_valid(self):
        self.main, self.main_ahead = 'd' * 40, 4
        result = self.verify()
        self.assertEqual(result['main_at_start'], self.main)
        self.assertEqual(result['main_commits_after_baseline'], 4)

    def test_baseline_off_main_or_older_than_seven_days_is_rejected(self):
        self.main, self.baseline_on_main = 'd' * 40, False
        with self.assertRaisesRegex(ValueError, 'not on main'):
            self.verify()
        self.baseline_on_main = True
        self.baseline_committed = NOW - 8 * 86400
        with self.assertRaisesRegex(ValueError, '8.0 days old'):
            self.verify()
        self.baseline_committed = NOW - 7 * 86400
        self.assertEqual('VERIFIED', self.verify()['status'])

    def test_closed_pr_and_candidate_without_main_are_rejected(self):
        self.state = 'closed'
        with self.assertRaises(ValueError):
            self.verify()
        self.state, self.ancestor_of_candidate = 'open', False
        with self.assertRaises(subprocess.CalledProcessError):
            self.verify()

    def test_calibration_requires_same_product_and_does_not_use_pr_head(self):
        with self.assertRaisesRegex(ValueError, 'identical product'):
            self.verify('admin-calibration')
        self.candidate = self.baseline
        self.calls.clear()
        self.assertTrue(self.verify('admin-calibration')['calibration'])
        self.assertFalse(any(call[0] == 'gh' for call in self.calls))

    def test_unknown_suite_cannot_succeed_with_every_workflow_step_skipped(self):
        with self.assertRaisesRegex(ValueError, 'supported suite'):
            self.verify('typo')
        self.assertEqual(self.calls, [])

    def test_workflow_suite_choices_match_validation(self):
        workflow = Path(__file__).parents[1] / 'workflows/performance-comparison.yml'
        options = re.search(r'options: \[([^\]]+)\]', workflow.read_text()).group(1)
        self.assertEqual(tuple(value.strip() for value in options.split(',')), pins.SUITES)


if __name__ == '__main__':
    unittest.main()
