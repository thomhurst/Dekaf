import array
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import run


class CompletionApiTests(unittest.TestCase):
    def test_detects_api_from_each_archived_product(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'src/Dekaf/Consumer/PartitionedProcessing.cs'
            source.parent.mkdir(parents=True)
            for api, expected in [('TryEnqueue(record)', []),
                                  ('CreateCompletionBatch(int capacity)', ['-p:DefineConstants=COMPLETION_BATCHES'])]:
                source.write_text(api)
                self.assertEqual(run.completion_defines(root), expected)


class AffinityTests(unittest.TestCase):
    def test_interleaved_hyperthreads_stay_on_the_same_side(self):
        self.assertEqual(run.select_affinity([(0, 0, 0), (1, 1, 0), (2, 0, 0), (3, 1, 0)]),
                         {'consumer': '1,3', 'infrastructure': '0,2'})

    def test_adjacent_hyperthreads_stay_on_the_same_side(self):
        self.assertEqual(run.select_affinity([(0, 0, 0), (1, 0, 0), (2, 1, 0), (3, 1, 0)]),
                         {'consumer': '2,3', 'infrastructure': '0,1'})

    def test_single_core_cannot_supply_isolation(self):
        with self.assertRaisesRegex(ValueError, 'two physical cores'):
            run.select_affinity([(0, 0, 0), (1, 0, 0)])


class MicroDriverTests(unittest.TestCase):
    def exercise(self, smoke, actual_count):
        with tempfile.TemporaryDirectory() as directory:
            def write_log(args, log, **kwargs):
                count = actual_count if isinstance(actual_count, int) else next(actual_count)
                content = 'WARM completed seconds=20.020 operations=171442176\n'
                content += ''.join(f'WorkloadActual {index}: 48 op, 1000000000.00 ns\n'
                                   for index in range(1, count + 1))
                Path(log).write_text(content)
            with patch.object(run, 'command', side_effect=write_log):
                run.micro(Path('host.dll'), Path(directory), 'B', 'Smoke' if smoke else 'B', smoke)

    def test_smoke_accepts_single_iteration_without_acceptance_validation(self):
        self.exercise(smoke=True, actual_count=1)

    def test_measurement_requires_all_iterations(self):
        with self.assertRaisesRegex(ValueError, 'Missing measured BDN iterations'):
            self.exercise(smoke=False, actual_count=24)

    def test_measurement_accepts_complete_iteration_count(self):
        self.exercise(smoke=False, actual_count=25)

    def test_earlier_incomplete_case_is_not_hidden_by_final_complete_case(self):
        with self.assertRaisesRegex(ValueError, 'Missing measured BDN iterations'):
            self.exercise(smoke=False, actual_count=iter([24, 25, 25, 25, 25, 25]))


class CompilationValidationTests(unittest.TestCase):
    def test_rejects_lost_events_wrong_clock_and_missing_boundaries(self):
        valid = dict(frequency=1000, total_events=0, overflow=False, events=[],
                     phases=[dict(Name=name, Timestamp=index) for index, name in enumerate(
                         ['initialize', 'warmup', 'measured', 'drain', 'finalize'])])
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            for changed in ({}, {'overflow': True}, {'total_events': 1}, {'frequency': 1},
                            {'phases': valid['phases'][:-1]}, {'phases': list(reversed(valid['phases']))}):
                with self.subTest(changed=changed):
                    (folder / 'compilations.json').write_text(json.dumps(dict(valid, **changed)))
                    if changed:
                        with self.assertRaises(ValueError):
                            run.validate_compilations(folder, {'StopwatchFrequency': 1000})
                    else:
                        run.validate_compilations(folder, {'StopwatchFrequency': 1000})


class MeasurementValidationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        self.metrics = dict(Mode='sync-records', Completed=241, Measured=120, WarmupCompleted=121,
                            Failures=0, BacklogAtEnd=0, PendingAfterStop=0, CommittedOffsets=[61, 60, 60, 60],
                            BatchCounts=[0, 241] + [0] * 15, MeasuredBatchCounts=[0, 120] + [0] * 15,
                            MeasuredHandlerInvocations=120, MessagesPerSecond=1, CpuNsPerMessage=1,
                            AllocatedBytesPerMessage=1, AllocatedBytesPerHandlerInvocation=1,
                            P50Ns=10, P99Ns=20, MaxNs=30, ActualWarmupSeconds=120.5,
                            JitMethodsStart=100, JitMethodsEnd=100, JitMsStart=1, JitMsEnd=1,
                            MeasurementStart=122, MeasurementEnd=241, StopwatchFrequency=1)
        self.producer = dict(Acknowledged=241, Sent=241, Failed=0, ScheduledStart=1, Rate=1, OfferBurst=1)
        self.series = [dict(Timestamp=index, JitMethods=100, JitMs=1, Threads=2, PendingWork=0,
                            CpuTicks=index, Gen0=0, Gen1=0, Gen2=0, HeapBytes=100, RssBytes=1000)
                       for index in range(241)]
        (self.folder / 'latency-ticks.bin').write_bytes(array.array('q', [10] * 120).tobytes())
        (self.folder / 'all-latency-ticks.bin').write_bytes(array.array('q', [10] * 241).tobytes())

    def validate(self):
        for name, value in [('metrics.json', self.metrics), ('producer.json', self.producer), ('series.json', self.series)]:
            (self.folder / name).write_text(json.dumps(value))
        return run.validate_loaded(self.folder, 121, 120, 1, True)

    def test_valid_completed_measurement(self):
        result = self.validate()
        self.assertEqual(result['MeasuredJitDelta'], 0)
        self.assertEqual(result['MeasuredThreadRange'], [2, 2])

    def test_rejects_missing_or_invalid_evidence(self):
        valid = copy.deepcopy(self.metrics)
        for key, value in [('Completed', 240), ('WarmupCompleted', 120), ('Measured', 119),
                           ('ActualWarmupSeconds', 119.9), ('CpuNsPerMessage', float('nan')),
                           ('P99Ns', 31), ('MessagesPerSecond', True), ('PendingAfterStop', 1),
                           ('CommittedOffsets', [60, 60, 60, 60]), ('MeasuredHandlerInvocations', 119)]:
            with self.subTest(key=key):
                self.metrics = dict(valid, **{key: value})
                with self.assertRaises(ValueError):
                    self.validate()

    def test_rejects_missing_runtime_counter(self):
        del self.series[150]['JitMethods']
        with self.assertRaisesRegex(ValueError, 'Missing runtime metric'):
            self.validate()

    def test_rejects_truncated_latency_stream(self):
        (self.folder / 'latency-ticks.bin').write_bytes(b'')
        with self.assertRaisesRegex(ValueError, 'Missing measured latency'):
            self.validate()

    def test_rejects_unexercised_pending_batch(self):
        self.metrics.update(Mode='pending-batches', PendingCompletions=120)
        with self.assertRaisesRegex(ValueError, 'No 16-record'):
            self.validate()

    def test_retains_measured_runtime_transitions(self):
        self.metrics['JitMethodsEnd'] += 3
        self.series[-1]['JitMethods'] += 3
        self.series[-1]['Threads'] += 1
        result = self.validate()
        self.assertEqual(result['MeasuredJitDelta'], 3)
        self.assertEqual(result['MeasuredThreadRange'], [2, 3])

    def test_rejects_missing_boundary_even_with_complete_time_series(self):
        del self.metrics['JitMethodsStart']
        with self.assertRaisesRegex(ValueError, 'runtime measurement boundaries'):
            self.validate()

    def test_latency_series_preserves_outside_boundary_samples(self):
        self.validate()
        self.metrics['MeasurementStart'] = 140
        run.latency_series(self.folder, self.metrics)
        samples = json.loads((self.folder / 'latency-series.json').read_text())
        self.assertEqual(sum(sample['completed'] for sample in samples), 120)
        self.assertLess(samples[0]['second'], 0)
        self.assertGreater(samples[-1]['second'], 100)


if __name__ == '__main__':
    unittest.main()
