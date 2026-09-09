import json
from pathlib import Path
import struct
import tempfile
import unittest

from outbox_loaded_aba import validate_phase, execution_plan


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


if __name__ == '__main__':
    unittest.main()
