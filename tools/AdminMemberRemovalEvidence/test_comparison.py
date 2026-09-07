import json
from pathlib import Path
import tempfile
import unittest
from run_comparison import compare, validate_probe

class ComparisonTests(unittest.TestCase):
    def metrics(self):
        return dict(CallsPerSecond=100, CpuNsPerCall=100, P50Ns=10, P99Ns=20, MaxNs=30, AllocatedBytesPerCall=100)

    def test_candidate_must_meet_both_controls(self):
        a1, b, a2 = self.metrics(), self.metrics(), self.metrics()
        a2['CpuNsPerCall'] = 98
        b['CpuNsPerCall'] = 102
        self.assertFalse(compare(a1, b, a2)['point_estimates_within_declared_limits'])

    def test_control_drift_cannot_be_averaged(self):
        a1, b, a2 = self.metrics(), self.metrics(), self.metrics()
        a1['MaxNs'] = 50
        a2['MaxNs'] = 10
        self.assertFalse(compare(a1, b, a2)['point_estimates_within_declared_limits'])

    def test_allocation_loss_cannot_be_offset_by_speed(self):
        a, b = self.metrics(), self.metrics()
        b.update(CallsPerSecond=1000, CpuNsPerCall=10, AllocatedBytesPerCall=102)
        self.assertFalse(compare(a, b, a)['point_estimates_within_declared_limits'])

    def test_point_estimates_alone_never_grant_acceptance(self):
        result = compare(self.metrics(), self.metrics(), self.metrics())
        self.assertTrue(result['point_estimates_within_declared_limits'])
        self.assertTrue(result['verdict'].startswith('INCONCLUSIVE'))

    def data(self):
        histogram = [{'Ticks':10,'Count':99}, {'Ticks':50,'Count':1}]
        return dict(Seconds=30, Completed=100, CallsPerSecond=100/30, CpuNsPerCall=100,
                    AllocatedBytesPerCall=100, P50Ns=10, P99Ns=10, MaxNs=50,
                    StopwatchFrequency=1_000_000_000, Latencies=histogram,
                    Intervals=[{'Latencies':histogram}])

    def validate(self, data):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'probe.json'
            path.write_text(json.dumps(data))
            return validate_probe(path, 30)

    def test_histogram_preserves_maximum(self):
        self.validate(self.data())
        data = self.data()
        data['MaxNs'] = 10
        with self.assertRaises(ValueError): self.validate(data)

    def test_no_dropped_completions(self):
        data = self.data()
        data['Completed'] = 101
        with self.assertRaises(ValueError): self.validate(data)

    def test_warmup_duration_is_elapsed_workload(self):
        data = self.data()
        data['Seconds'] = 29.9
        with self.assertRaises(ValueError): self.validate(data)

    def test_every_interval_preserved(self):
        data = self.data()
        data['Intervals'] = []
        with self.assertRaises(ValueError): self.validate(data)

if __name__ == '__main__': unittest.main()
