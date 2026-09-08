import array
import json
import math
from pathlib import Path
import sys
import tempfile
import unittest

import assess


class PointComparisonTests(unittest.TestCase):
    def test_regression_against_one_control_is_not_averaged_away(self):
        result = assess.compare([100, 106, 120], 3, 3)
        self.assertTrue(result['loss_signal'])
        self.assertTrue(result['control_drift_exceeds_tolerance'])
        self.assertGreater(result['candidate_delta_percent'][0], 3)
        self.assertLess(result['candidate_delta_percent'][1], 0)

    def test_throughput_uses_opposite_loss_direction(self):
        result = assess.compare([100, 96, 100], 3, 3, higher_better=True)
        self.assertTrue(result['loss_signal'])
        self.assertFalse(result['control_drift_exceeds_tolerance'])

    def test_allocation_uses_absolute_one_byte_tolerance(self):
        result = assess.compare([400, 401.1, 401], 1, 1, absolute=True)
        self.assertTrue(result['loss_signal'])
        self.assertFalse(result['control_drift_exceeds_tolerance'])

    def test_allocation_control_tolerance_uses_greater_of_one_byte_or_three_percent(self):
        self.assertFalse(assess.compare([400, 400, 412], 1, 1, absolute=True)['control_drift_exceeds_tolerance'])
        self.assertTrue(assess.compare([400, 400, 413], 1, 1, absolute=True)['control_drift_exceeds_tolerance'])

    def test_zero_allocation_does_not_divide_by_zero(self):
        result = assess.compare([0, 0, 0], 1, 1, absolute=True)
        self.assertEqual(result['candidate_delta_percent'], [None, None])
        self.assertFalse(result['loss_signal'])

    def test_invalid_values_are_not_a_pass(self):
        for value in [float('nan'), float('inf'), -1]:
            with self.assertRaisesRegex(ValueError, 'Invalid metric values'):
                assess.compare([100, value, 100], 3, 3)


class RawEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def write_latencies(self, values):
        raw = array.array('q', values)
        if sys.byteorder != 'little':
            raw.byteswap()
        (self.root / 'latency-ticks.bin').write_bytes(raw.tobytes())
        (self.root / 'latency-series.json').write_text(json.dumps([{'completed': len(values)}]))
        ordered = sorted(values)
        return dict(Measured=len(values), StopwatchFrequency=1_000_000_000,
                    P50Ns=ordered[math.ceil(len(values)*.5)-1],
                    P99Ns=ordered[math.ceil(len(values)*.99)-1], MaxNs=ordered[-1])

    def test_raw_maximum_is_preserved(self):
        metrics = self.write_latencies([4, 1, 3, 2, 100000])
        assess.verify_latencies(self.root, metrics)
        metrics['MaxNs'] = 4
        with self.assertRaisesRegex(ValueError, 'Raw latency mismatch: MaxNs'):
            assess.verify_latencies(self.root, metrics)

    def test_extra_raw_sample_is_rejected(self):
        metrics = self.write_latencies([1, 2, 3])
        metrics['Measured'] = 2
        with self.assertRaisesRegex(ValueError, 'Extra latency samples'):
            assess.verify_latencies(self.root, metrics)

    def test_time_series_cannot_drop_samples(self):
        metrics = self.write_latencies([1, 2, 3])
        (self.root / 'latency-series.json').write_text('[{"completed":2}]')
        with self.assertRaisesRegex(ValueError, 'Latency series dropped samples'):
            assess.verify_latencies(self.root, metrics)

    def test_inventory_cannot_escape_archive(self):
        (self.root / 'sha256.json').write_text('{"../outside":"bad"}')
        with self.assertRaisesRegex(ValueError, 'Inventory path escapes evidence'):
            assess.verify_inventory(self.root)

    def test_missing_inventory_file_is_rejected(self):
        (self.root / 'sha256.json').write_text('{"missing":"bad"}')
        with self.assertRaisesRegex(ValueError, 'Missing retained file'):
            assess.verify_inventory(self.root)

    def test_tampered_inventory_file_is_rejected(self):
        (self.root / 'sha256.json').write_text('{"present":"bad"}')
        (self.root / 'present').write_text('changed')
        with self.assertRaisesRegex(ValueError, 'Artifact hash mismatch'):
            assess.verify_inventory(self.root)

    def write_micro(self, actual_count=25, duplicate=False, warmup_ns=1_000_000_000):
        folder = self.root / 'B-micro-Repeated-1/results'
        folder.mkdir(parents=True)
        samples = []
        for stage, count in [('Warmup', 30), ('Actual', actual_count), ('Result', 25)]:
            for index in range(1, count + 1):
                samples.append(dict(IterationMode='Workload', IterationStage=stage,
                    IterationIndex=1 if stage == 'Actual' and duplicate else index, Operations=1,
                    Nanoseconds=warmup_ns if stage == 'Warmup' else 1_000_000_000))
        benchmark = dict(Parameters='Pattern=Repeated&BatchSize=1', Measurements=samples)
        (folder / 'fixture-report-full.json').write_text(json.dumps({'Benchmarks': [benchmark]}))

    def test_incomplete_actual_samples_are_rejected_before_statistics(self):
        self.write_micro(actual_count=24)
        with self.assertRaisesRegex(ValueError, 'Wrong Actual sample count'):
            assess.micro(self.root, 'B', 'Repeated', 1)

    def test_repeated_iteration_number_does_not_satisfy_sample_count(self):
        self.write_micro(duplicate=True)
        with self.assertRaisesRegex(ValueError, 'Duplicated or missing Actual iteration'):
            assess.micro(self.root, 'B', 'Repeated', 1)

    def test_iteration_count_does_not_substitute_for_elapsed_warmup(self):
        self.write_micro(warmup_ns=1_000_000)
        with self.assertRaisesRegex(ValueError, 'Insufficient elapsed BDN workload warmup'):
            assess.micro(self.root, 'B', 'Repeated', 1)


if __name__ == '__main__':
    unittest.main()
