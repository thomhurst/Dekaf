import array
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('share_driver', Path(__file__).with_name('run.py'))
driver = importlib.util.module_from_spec(spec)
spec.loader.exec_module(driver)


class CompletedAcknowledgementValidation(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.folder = Path(self.temporary.name)
        self.metrics = dict(Processed=512, Completed=512, Measured=256, WarmupCompleted=256,
            Failures=0, BacklogAtEnd=0, ExplicitCommits=4, MessagesPerSecond=128,
            CpuNsPerMessage=100, AllocatedBytesPerMessage=8, P50Ns=100, P99Ns=100, MaxNs=100,
            CpuTicksStart=0, CpuTicksEnd=256, AllocatedBytesStart=0, AllocatedBytesEnd=2048,
            ActualWarmupSeconds=2, MeasuredDurationSeconds=2, StopwatchFrequency=1_000_000_000,
            MeasurementStart=0)
        self.producer = dict(Sent=512, Acknowledged=512, Failed=0, ScheduledStart=0, Rate=128, OfferBurst=128)
        sample = dict(Threads=1, PendingWork=0, CpuTicks=0,
            Gen0=0, Gen1=0, Gen2=0, HeapBytes=100, RssBytes=1000)
        self.series = [sample.copy() for _ in range(4)]
        for name, count in [('all-latency-ticks.bin', 512), ('latency-ticks.bin', 256)]:
            with (self.folder / name).open('wb') as stream:
                array.array('q', [100] * count).tofile(stream)

    def tearDown(self):
        self.temporary.cleanup()

    def validate(self, acceptance=False):
        for name, value in [('metrics.json', self.metrics), ('producer.json', self.producer), ('series.json', self.series)]:
            (self.folder / name).write_text(json.dumps(value))
        return driver.validate(self.folder, 2, 2, 128, acceptance)

    def test_complete_capture(self):
        self.assertEqual(self.validate()['Completed'], 512)

    def test_reject_yielded_but_unacknowledged(self):
        self.metrics['Completed'] -= 1
        with self.assertRaisesRegex(ValueError, 'acknowledged'):
            self.validate()

    def test_reject_callback_failure(self):
        self.metrics['Failures'] = 1
        with self.assertRaisesRegex(ValueError, 'Failed'):
            self.validate()

    def test_reject_missing_commit(self):
        self.metrics['ExplicitCommits'] -= 1
        with self.assertRaisesRegex(ValueError, 'commit'):
            self.validate()

    def test_reject_trimmed_maximum(self):
        self.metrics['MaxNs'] = 99
        with self.assertRaisesRegex(ValueError, 'MaxNs'):
            self.validate()

    def test_reject_different_measured_population(self):
        with (self.folder / 'latency-ticks.bin').open('wb') as stream:
            array.array('q', [101] * 256).tofile(stream)
        with self.assertRaisesRegex(ValueError, 'population'):
            self.validate()

    def test_reject_missing_runtime_counter(self):
        del self.series[1]['CpuTicks']
        with self.assertRaisesRegex(ValueError, 'CpuTicks'):
            self.validate()

    def test_reject_wrong_cpu_denominator(self):
        self.metrics['CpuNsPerMessage'] = 99
        with self.assertRaisesRegex(ValueError, 'denominator'):
            self.validate()

    def test_smoke_does_not_satisfy_acceptance_warmup(self):
        with self.assertRaisesRegex(ValueError, 'duration'):
            self.validate(acceptance=True)


if __name__ == '__main__':
    unittest.main()
