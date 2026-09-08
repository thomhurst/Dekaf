import json
import hashlib
from pathlib import Path
import tempfile
import unittest
from run_comparison import (compare, validate_probe, validate_primer_segments, retain_loaded_binaries,
                            validate_bdn_phase, validate_engine_primer, validate_bdn_workload_warmup)

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

    def test_reentry_primer_requires_every_elapsed_segment(self):
        with tempfile.TemporaryDirectory() as directory:
            primer = Path(directory) / 'primer.json'
            segments_path = primer.with_name('segments-primer.json')
            segment = self.data()
            segment['Seconds'] = .05
            segments = [dict(segment) for _ in range(128)]
            segments_path.write_text(json.dumps(segments), encoding='utf-8')
            validate_primer_segments(primer)
            segments_path.write_text(json.dumps(segments[:-1]), encoding='utf-8')
            with self.assertRaises(ValueError): validate_primer_segments(primer)
            segments[-1]['Seconds'] = .049
            segments_path.write_text(json.dumps(segments), encoding='utf-8')
            with self.assertRaises(ValueError): validate_primer_segments(primer)

    def test_loaded_binary_identity_rejects_replaced_product(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            original = root / 'original'
            original.mkdir()
            product = original / 'Dekaf.dll'
            product.write_bytes(b'observed loaded product')
            manifest = root / 'binaries.json'
            manifest.write_text(json.dumps([{'Path': str(product),
                'Sha256': hashlib.sha256(product.read_bytes()).hexdigest()}]), encoding='utf-8')
            retain_loaded_binaries(manifest, original, root / 'archive')
            self.assertEqual(product.read_bytes(), (root / 'archive' / 'Dekaf.dll').read_bytes())
            product.write_bytes(b'replaced product')
            with self.assertRaises(ValueError):
                retain_loaded_binaries(manifest, original, root / 'other-archive')

    def test_bdn_boundaries_match_the_observed_worker_clock(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            warmup = root / 'inventory-16-42.json'
            clock = dict(StartedTimestamp=1000, StopwatchFrequency=100, ProcessId=42)
            warmup.with_name('clock-' + warmup.name).write_text(json.dumps(clock), encoding='utf-8')
            runtime = [{'Seconds': value} for value in [0, 1, 2, 3, 4]]
            warmup.with_name('runtime-' + warmup.name).write_text(json.dumps(runtime), encoding='utf-8')
            signals = [dict(Signal=signal, Timestamp=timestamp, StopwatchFrequency=100, ProcessId=42)
                       for signal, timestamp in [('BeforeActualRun', 1150), ('AfterActualRun', 1250)]]
            path = root / 'signals-inventory-16-42.jsonl'
            path.write_text('\n'.join(json.dumps(row) for row in signals), encoding='utf-8')
            result = validate_bdn_phase(warmup)
            self.assertEqual(result['actual_start_seconds'], 1.5)
            self.assertEqual(len(result['overlapping_runtime_intervals']), 2)
            signals[1]['ProcessId'] = 43
            path.write_text('\n'.join(json.dumps(row) for row in signals), encoding='utf-8')
            with self.assertRaises(ValueError): validate_bdn_phase(warmup)
            signals[1]['ProcessId'] = 42
            signals[1]['Timestamp'] = 1450
            path.write_text('\n'.join(json.dumps(row) for row in signals), encoding='utf-8')
            with self.assertRaises(ValueError): validate_bdn_phase(warmup)

    def test_reused_worker_pid_does_not_combine_different_cases(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for case, started in [('legacy-16', 1000), ('delete-16', 2000)]:
                warmup = root / f'{case}-42.json'
                clock = dict(StartedTimestamp=started, StopwatchFrequency=100, ProcessId=42)
                warmup.with_name('clock-' + warmup.name).write_text(json.dumps(clock), encoding='utf-8')
                warmup.with_name('runtime-' + warmup.name).write_text(
                    json.dumps([{'Seconds': value} for value in [0, 1, 2, 3]]), encoding='utf-8')
                signals = [dict(Signal=signal, Timestamp=started + offset, StopwatchFrequency=100, ProcessId=42)
                           for signal, offset in [('BeforeActualRun', 150), ('AfterActualRun', 250)]]
                warmup.with_name('signals-' + warmup.stem + '.jsonl').write_text(
                    '\n'.join(json.dumps(row) for row in signals), encoding='utf-8')
            for case in ['legacy-16', 'delete-16']:
                self.assertEqual(validate_bdn_phase(root / f'{case}-42.json')['actual_start_seconds'], 1.5)

    def test_engine_primer_requires_elapsed_work_and_matching_counts(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'engine-primer.json'
            valid = dict(Seconds=10, CallbackPairs=100, FormattedMeasurements=100)
            path.write_text(json.dumps(valid), encoding='utf-8')
            self.assertEqual(validate_engine_primer(path), valid)
            for changes in [dict(Seconds=9.999), dict(Seconds=float('nan')),
                            dict(CallbackPairs=0, FormattedMeasurements=0), dict(FormattedMeasurements=99)]:
                with self.subTest(changes=changes):
                    path.write_text(json.dumps(dict(valid, **changes)), encoding='utf-8')
                    with self.assertRaises(ValueError): validate_engine_primer(path)

    def test_bdn_warmup_requires_elapsed_workload_not_iteration_count_alone(self):
        row = dict(IterationMode='Workload', IterationStage='Warmup', Nanoseconds=400_000_000, Operations=100)
        valid = {'Measurements': [dict(row) for _ in range(50)]}
        result = validate_bdn_workload_warmup(valid)
        self.assertEqual((result['seconds'], result['completed']), (20, 5000))
        for measurements in [valid['Measurements'][:-1],
                             [dict(row, Nanoseconds=399_000_000) for _ in range(50)],
                             [dict(row, IterationMode='Overhead') for _ in range(50)],
                             [dict(row, Nanoseconds=float('nan')) for _ in range(50)],
                             [dict(row, Operations=0) for _ in range(50)]]:
            with self.subTest(measurements=measurements[:1]):
                with self.assertRaises(ValueError):
                    validate_bdn_workload_warmup({'Measurements': measurements})

    def test_only_explicit_smoke_can_omit_bdn_workload_warmup(self):
        benchmark = {'Measurements': []}
        with self.assertRaises(ValueError): validate_bdn_workload_warmup(benchmark)
        self.assertTrue(validate_bdn_workload_warmup(benchmark, smoke=True)['smoke'])

if __name__ == '__main__': unittest.main()
