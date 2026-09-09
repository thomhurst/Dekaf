import json
from pathlib import Path
import struct
import tempfile
import unittest

from outbox_loaded_aba import (validate_phase, execution_plan, validate_recovery,
                               fixture_settings, validate_notification_coverage)


class ExecutionPlanTests(unittest.TestCase):
    def test_adjacent_controls_keep_all_cases_and_smoke_first(self):
        plan = list(execution_plan(True))
        self.assertEqual(len(plan), 20)
        self.assertTrue(all(row[2] for row in plan[:8]))
        self.assertTrue(all(not row[2] for row in plan[8:]))
        for index in range(8, 20, 3):
            group = plan[index:index+3]
            self.assertEqual([row[:2] for row in group], [('A1','A'),('B','B'),('A2','A')])
            self.assertEqual(len({row[3:] for row in group}), 1)
        self.assertEqual(len({row[3:] for row in plan[8:]}), 4)

    def test_default_phase_order_is_unchanged(self):
        plan = list(execution_plan())
        self.assertEqual([row[0] for row in plan],
                         ['DryA']*4 + ['DryB']*4 + ['A1']*4 + ['B']*4 + ['A2']*4)


class PhaseValidationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.data = dict(Cycles=2, Completed=1000, BatchCount=500, Seconds=2, RequestedSeconds=2,
                         Start=dict(Timestamp=1, Pending=0, Completed=0, CpuTicks=0, Allocated=0),
                         End=dict(Timestamp=2001, Pending=0, Completed=1000, CpuTicks=200, Allocated=100000),
                         StopwatchFrequency=1000, MessagesPerSecond=500, CpuNsPerMessage=20,
                         AllocatedBytesPerMessage=100, P50Ns=100_000_000,
                         P99Ns=200_000_000, MaxNs=200_000_000)
        self.raw = struct.pack('<qqqq', 2, 102, 103, 303)

    def write(self):
        (self.root / 'measured.json').write_text(json.dumps(self.data), encoding='utf-8')
        (self.root / 'measured-cycles.bin').write_bytes(self.raw)

    def test_preserved_raw_latencies_validate(self):
        self.write()
        self.assertEqual(validate_phase(self.root, 'measured', 2)['Completed'], 1000)

    def test_truncated_samples_fail(self):
        self.raw = self.raw[:-1]
        self.write()
        with self.assertRaisesRegex(ValueError, 'Missing raw'):
            validate_phase(self.root, 'measured', 2)

    def test_invented_completion_count_fails(self):
        self.data['Completed'] = 1001
        self.write()
        with self.assertRaisesRegex(ValueError, 'denominator'):
            validate_phase(self.root, 'measured', 2)

    def test_trimmed_maximum_fails(self):
        self.data['MaxNs'] = 100_000_000
        self.write()
        with self.assertRaisesRegex(ValueError, 'MaxNs'):
            validate_phase(self.root, 'measured', 2)

    def test_overlapping_cycles_fail(self):
        self.raw = struct.pack('<qqqq', 2, 102, 100, 300)
        self.write()
        with self.assertRaisesRegex(ValueError, 'boundaries'):
            validate_phase(self.root, 'measured', 2)

    def test_short_warmup_fails(self):
        self.data['Seconds'] = 1.9
        self.write()
        with self.assertRaisesRegex(ValueError, 'elapsed'):
            validate_phase(self.root, 'measured', 2)

    def test_pending_rows_fail(self):
        self.data['End']['Pending'] = 500
        self.write()
        with self.assertRaisesRegex(ValueError, 'leftover'):
            validate_phase(self.root, 'measured', 2)

    def test_wrong_cpu_denominator_fails(self):
        self.data['CpuNsPerMessage'] *= 2
        self.write()
        with self.assertRaisesRegex(ValueError, 'CpuNsPerMessage'):
            validate_phase(self.root, 'measured', 2)


class RecoveryValidationTests(unittest.TestCase):
    def setUp(self):
        self.phases = {name: {'Faults': 2} for name in ('primer', 'warmup', 'measured')}
        self.completion = dict(ExpectedRetainedRows=500, ShutdownAcknowledged=500,
                               ShutdownPublisherInFlight=False, InjectedFailures=6, RecoveredFailures=6,
                               ObservedFailureLogs=6, LeaseLossScenario=False, Failures=6)

    def test_every_recovery_shard_keeps_its_own_triplet(self):
        for case in ('legacy-failure-off', 'legacy-failure-on', 'renewal-loss-off', 'renewal-loss-on'):
            plan = list(execution_plan(False, case, True))
            self.assertEqual([row[0] for row in plan], ['DryA', 'DryB', 'A1', 'B', 'A2'])
            self.assertTrue(all('-'.join(row[3:]) == case for row in plan))

    def test_acknowledged_lease_loss_is_not_a_failed_publish(self):
        self.completion.update(LeaseLossScenario=True, Failures=0, ObservedFailureLogs=12)
        validate_recovery(self.completion, self.phases, True, 'on', True)
        self.completion['Failures'] = 6
        with self.assertRaisesRegex(ValueError, 'failure telemetry'):
            validate_recovery(self.completion, self.phases, True, 'on', True)

    def test_each_phase_must_exercise_recovery(self):
        self.phases['measured']['Faults'] = 0
        with self.assertRaisesRegex(ValueError, 'Missing recovery coverage'):
            validate_recovery(self.completion, self.phases, True, 'on', True)

    def test_unrecovered_rows_fail(self):
        self.completion['RecoveredFailures'] = 5
        with self.assertRaisesRegex(ValueError, 'recover all retained rows'):
            validate_recovery(self.completion, self.phases, True, 'on', True)

    def test_unobserved_shutdown_work_fails(self):
        self.completion['ShutdownPublisherInFlight'] = True
        with self.assertRaisesRegex(ValueError, 'unobserved publisher'):
            validate_recovery(self.completion, self.phases, True, 'on', True)

    def test_deleting_shutdown_rows_fails(self):
        self.completion['ExpectedRetainedRows'] = 0
        with self.assertRaisesRegex(ValueError, 'retain its acknowledged rows'):
            validate_recovery(self.completion, self.phases, True, 'on', True)


class NotificationCoverageTests(unittest.TestCase):
    def test_commit_product_uses_notifications_without_requiring_metrics_api(self):
        self.assertEqual(dict(telemetry=False, notifications=False), fixture_settings(3171, 'A'))
        self.assertEqual(dict(telemetry=False, notifications=True), fixture_settings(3171, 'B'))
        self.assertEqual(dict(telemetry=True, notifications=False), fixture_settings(3085, 'B'))

    def test_one_notification_per_new_batch_includes_shutdown_but_not_retries(self):
        completion = dict(TotalCompleted=1500, ExpectedRetainedRows=500,
                          CommitNotificationsEnabled=True, CommitNotifications=4)
        validate_notification_coverage(completion, True)
        for count in (0, 3, 5):
            completion['CommitNotifications'] = count
            with self.assertRaisesRegex(ValueError, 'committed-batch notifications'):
                validate_notification_coverage(completion, True)

    def test_disabled_or_missing_candidate_binding_is_not_coverage(self):
        with self.assertRaisesRegex(ValueError, 'fixture binding'):
            validate_notification_coverage(dict(TotalCompleted=500), True)
        with self.assertRaisesRegex(ValueError, 'committed-batch notifications'):
            validate_notification_coverage(dict(CommitNotifications=1), False)
        validate_notification_coverage({}, False)


if __name__ == '__main__':
    unittest.main()
