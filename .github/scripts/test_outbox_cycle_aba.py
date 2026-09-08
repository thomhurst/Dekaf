import csv
import json
from pathlib import Path
import tempfile
import unittest

from outbox_cycle_aba import MODES, comparison, read_case


class OutboxEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)
        (self.folder / 'results').mkdir()
        self.log = self.folder / 'run.log'
        self.write_case()

    def write_case(self, warmup_ns=1_000_000_000, samples=25, runtime_samples=25):
        case = {'Statistics': {'Mean': 250, 'N': samples},
                'Metrics': [{'Descriptor': {'Id': 'Allocated Memory'}, 'Value': 0}]}
        (self.folder / 'results/case-report-full.json').write_text(json.dumps({'Benchmarks': [case]}))
        self.log.write_text(''.join(f'WorkloadWarmup {i}: 100 op, {warmup_ns}.00 ns, 250 ns/op\n'
                                    for i in range(1, 31)) + 'RAW cycles=1000 bytes=0\n')
        with (self.folder / 'runtime.csv').open('w', newline='') as stream:
            writer = csv.DictWriter(stream, fieldnames=['workload', 'jit_methods', 'jit_ms'])
            writer.writeheader()
            for phase, count in [('WorkloadWarmup', 30), ('WorkloadActual', runtime_samples)]:
                writer.writerows({'workload': f'{phase} {i}', 'jit_methods': 42, 'jit_ms': 1}
                                 for i in range(count))

    def test_accepts_complete_raw_evidence(self):
        result = read_case(self.folder, self.log)
        self.assertEqual(30, result['warmup_seconds'])
        self.assertEqual(3000, result['warmup_cycles'])
        self.assertEqual(0, result['raw_1000_cycle_bytes'])

    def test_rejects_short_elapsed_warmup_despite_iteration_count(self):
        self.write_case(warmup_ns=500_000_000)
        with self.assertRaisesRegex(ValueError, 'elapsed workload warmup'):
            read_case(self.folder, self.log)

    def test_rejects_missing_measured_samples(self):
        self.write_case(samples=24)
        with self.assertRaisesRegex(ValueError, '25 valid measured samples'):
            read_case(self.folder, self.log)

    def test_rejects_missing_runtime_intervals(self):
        self.write_case(runtime_samples=24)
        with self.assertRaisesRegex(ValueError, 'Runtime series'):
            read_case(self.folder, self.log)

    def test_rejects_duplicate_case(self):
        report = self.folder / 'results/case-report-full.json'
        (report.parent / 'duplicate-report-full.json').write_text(report.read_text())
        with self.assertRaisesRegex(ValueError, 'exactly one case'):
            read_case(self.folder, self.log)

    def test_preserves_both_controls_instead_of_averaging_drift(self):
        phases = {phase: {mode: {'mean_ns': mean} for mode in MODES}
                  for phase, mean in [('A1', 100), ('B', 110), ('A2', 120)]}
        row = comparison(phases)[0]
        self.assertAlmostEqual(10, row['candidate_vs_A1_percent'])
        self.assertAlmostEqual(-100 / 12, row['candidate_vs_A2_percent'])
        self.assertAlmostEqual(20, row['control_drift_percent'])

    def test_rejects_incomplete_matrix(self):
        with self.assertRaisesRegex(ValueError, 'Incomplete'):
            comparison({'A1': {}})


if __name__ == '__main__':
    unittest.main()
