import copy
import unittest
import run


class ShutdownValidationTests(unittest.TestCase):
    def sample(self):
        start = dict(Seconds=0, Completed=0, CpuTicks=0, AllocatedBytes=0, JitMethods=0, JitMs=0)
        end = dict(start, Seconds=2, Completed=256, CpuTicks=100, AllocatedBytes=512,
                   Threads=2, PendingWork=0, Gen0=0, Gen1=0, Gen2=0, HeapBytes=1000, RssBytes=2000,
                   StopMeanTicks=10, StopMaxTicks=10, MessageMeanTicks=20, MessageMaxTicks=20)
        return dict(Start=start, End=end, Series=[end], RecordsPerStop=128, Failures=0,
                    PendingAfterStop=0, BatchSize=16, Keys=2, StopwatchFrequency=1000000000,
                    StopTicks=[dict(Ticks=10, Count=2)], MessageTicks=[dict(Ticks=20, Count=256)])

    def test_completed_message_cpu_denominator(self):
        result = run.summarize_shutdown(self.sample(), 2)
        self.assertEqual(result['CpuNsPerMessage'], 10000 / 256)
        self.assertEqual(result['Stops'], 2)
        self.assertEqual(result['MessageMaxNs'], 20)

    def test_missing_message_samples_invalidates(self):
        data = self.sample()
        data['MessageTicks'][0]['Count'] -= 1
        with self.assertRaisesRegex(ValueError, 'Incomplete shutdown histogram'):
            run.summarize_shutdown(data, 2)

    def test_retrospectively_trimmed_maximum_invalidates(self):
        data = self.sample()
        data['MessageTicks'][0]['Ticks'] = 19
        with self.assertRaisesRegex(ValueError, 'maximum differs'):
            run.summarize_shutdown(data, 2)

    def test_pending_work_invalidates(self):
        data = self.sample()
        data['PendingAfterStop'] = 1
        with self.assertRaisesRegex(ValueError, 'correctness'):
            run.summarize_shutdown(data, 2)

    def test_short_warmup_invalidates(self):
        with self.assertRaisesRegex(ValueError, 'duration'):
            run.summarize_shutdown(self.sample(), 120)

    def test_duplicate_bucket_invalidates(self):
        data = self.sample()
        data['StopTicks'] = [dict(Ticks=10, Count=1), dict(Ticks=10, Count=1)]
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            run.summarize_shutdown(data, 2)

    def test_missing_runtime_counters_invalidates(self):
        data = copy.deepcopy(self.sample())
        del data['End']['HeapBytes']
        with self.assertRaises((ValueError, KeyError)):
            run.summarize_shutdown(data, 2)
