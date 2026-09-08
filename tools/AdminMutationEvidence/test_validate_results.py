import copy
import json
from pathlib import Path
import tempfile
import unittest

from validate_results import validate


class AccountingTests(unittest.TestCase):
    def setUp(self):
        start = dict(Seconds=0, Completed=0, CpuTicks=0, AllocatedBytes=0)
        middle = dict(Seconds=.5, Completed=2, CpuTicks=2, AllocatedBytes=20)
        end = dict(Seconds=1, Completed=4, CpuTicks=4, AllocatedBytes=40)
        self.data = dict(Seconds=1, Completed=4, CallsPerSecond=4, CpuNsPerCall=100,
                         AllocatedBytesPerCall=10, P50Ns=2, P99Ns=9, MaxNs=9,
                         StopwatchFrequency=1_000_000_000, Start=start, End=end,
                         Latencies=[dict(Ticks=1, Count=1), dict(Ticks=2, Count=2), dict(Ticks=9, Count=1)],
                         Intervals=[dict(Start=start, End=middle, Latencies=[dict(Ticks=1, Count=1), dict(Ticks=2, Count=1)]),
                                    dict(Start=middle, End=end, Latencies=[dict(Ticks=2, Count=1), dict(Ticks=9, Count=1)])])

    def check(self, data):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'sample.json'
            path.write_text(json.dumps(data), encoding='utf-8')
            return validate(path, 1)

    def test_accepts_complete_accounting(self):
        self.assertEqual(self.check(self.data)['Completed'], 4)

    def test_rejects_lost_maximum(self):
        self.data['Latencies'].pop()
        with self.assertRaises(ValueError): self.check(self.data)

    def test_rejects_forged_cpu_scope(self):
        self.data['CpuNsPerCall'] = 1
        with self.assertRaises(ValueError): self.check(self.data)

    def test_rejects_broken_interval(self):
        self.data['Intervals'][1]['Start'] = copy.copy(self.data['Intervals'][1]['Start'])
        self.data['Intervals'][1]['Start']['Seconds'] = .6
        with self.assertRaises(ValueError): self.check(self.data)

    def test_rejects_forged_tail(self):
        self.data['P99Ns'] = 2
        with self.assertRaises(ValueError): self.check(self.data)


if __name__ == '__main__':
    unittest.main()
