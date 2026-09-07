import copy
import json
from pathlib import Path
import tempfile
import unittest

from benchmark_aba import compare, reports


class AbaEvidenceTests(unittest.TestCase):
    def test_missing_control_case_cannot_pass(self):
        with self.assertRaises(ValueError):
            compare({'A1': {'case': {}}, 'B': {'case': {}}, 'A2': {}})

    def test_controls_are_reported_separately(self):
        phases = {phase: {'case': {'mean_ns': mean, 'allocated_bytes': 0}}
                  for phase, mean in [('A1', 100), ('B', 110), ('A2', 120)]}
        row, = compare(phases)
        self.assertAlmostEqual(row['candidate_vs_A1_percent'], 10)
        self.assertAlmostEqual(row['candidate_vs_A2_percent'], -8.3333333333)
        self.assertAlmostEqual(row['control_drift_percent'], 20)

    def test_failed_or_duplicate_measurements_are_rejected(self):
        case = {'Namespace': 'Test', 'Type': 'Suite', 'Method': 'Run', 'Parameters': '',
                'Statistics': {'Mean': 100, 'N': 25},
                'Metrics': [{'Descriptor': {'Id': 'Allocated Memory'}, 'Value': 0}]}
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'test-report-full.json'
            path.write_text(json.dumps({'Benchmarks': [case]}))
            self.assertEqual(len(reports(Path(directory))), 1)
            path.write_text(json.dumps({'Benchmarks': [case, case]}))
            with self.assertRaises(ValueError):
                reports(Path(directory))
            failed = copy.deepcopy(case)
            failed['Statistics'] = None
            path.write_text(json.dumps({'Benchmarks': [case, failed]}))
            with self.assertRaises(ValueError):
                reports(Path(directory))

    def test_missing_allocations_are_not_treated_as_zero(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'test-report-full.json'
            path.write_text(json.dumps({'Benchmarks': [{'Statistics': {'Mean': 100, 'N': 25}}]}))
            with self.assertRaises(ValueError):
                reports(Path(directory))


if __name__ == '__main__':
    unittest.main()
