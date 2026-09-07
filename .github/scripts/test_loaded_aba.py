import json
from pathlib import Path
import tempfile
import unittest
from loaded_aba import comparison, validate_workload


class LoadedEvidenceTests(unittest.TestCase):
    def test_missing_or_mismatched_controls_are_rejected(self):
        with self.assertRaises(ValueError):
            comparison({'A1': {}, 'B': {}}, [])
        with self.assertRaises(ValueError):
            comparison({'A1': {'mode': {}}, 'B': {}, 'A2': {}}, [])

    def test_zero_baseline_allocation_is_not_divided_or_hidden(self):
        report = comparison({p: {'mode': {'allocated': v}} for p, v in [('A1', 0), ('B', 8), ('A2', 0)]}, ['allocated'])
        delta = report['cases'][0]['deltas']['allocated']
        self.assertEqual(delta['B_minus_A1'], 8)
        self.assertIsNone(delta['B_vs_A1_percent'])
        self.assertEqual(report['acceptance'], 'NOT_EVALUATED')

    def test_dropped_records_missing_latency_and_missing_cpu_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            metrics = {'Completed': 22000, 'Measured': 2000, 'Failures': 0, 'BacklogAtEnd': 0,
                       'CpuNsPerMessage': 5, 'AllocatedBytesPerMessage': 0, 'P50Ns': 10,
                       'P99Ns': 20, 'MaxNs': 30, 'MessagesPerSecond': 1000}
            producer = {'Sent': 22000, 'Acknowledged': 22000, 'Failed': 0}
            (folder / 'metrics.json').write_text(json.dumps(metrics))
            (folder / 'producer.json').write_text(json.dumps(producer))
            (folder / 'latency-ticks.bin').write_bytes(bytes(16000))
            validate_workload(folder, 2, 1000)
            producer['Acknowledged'] -= 1
            (folder / 'producer.json').write_text(json.dumps(producer))
            with self.assertRaises(ValueError):
                validate_workload(folder, 2, 1000)
            producer['Acknowledged'] += 1
            (folder / 'producer.json').write_text(json.dumps(producer))
            (folder / 'latency-ticks.bin').write_bytes(bytes(8))
            with self.assertRaises(ValueError):
                validate_workload(folder, 2, 1000)
            (folder / 'latency-ticks.bin').write_bytes(bytes(16000))
            metrics.pop('CpuNsPerMessage')
            (folder / 'metrics.json').write_text(json.dumps(metrics))
            with self.assertRaises(ValueError):
                validate_workload(folder, 2, 1000)


if __name__ == '__main__':
    unittest.main()
