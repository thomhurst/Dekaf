import copy
import unittest

from admin_timing import validate_timing


class TimingTests(unittest.TestCase):
    def setUp(self):
        self.capture = dict(TraceClock=dict(BeforeTimestamp=1, AfterTimestamp=2),
            Start=dict(Timestamp=3), StopwatchFrequency=1000,
            Intervals=[dict(Start=dict(Timestamp=3), End=dict(Timestamp=10),
                MaximumCall=dict(StartTimestamp=4, EndTimestamp=9, Ticks=5),
                Latencies=[dict(Ticks=2, Count=3), dict(Ticks=5, Count=1)])])

    def test_valid_capture_retains_original_data(self):
        original = copy.deepcopy(self.capture)
        self.assertIs(validate_timing(self.capture), self.capture)
        self.assertEqual(original, self.capture)

    def test_rejects_call_outside_interval_even_when_duration_matches(self):
        self.capture['Intervals'][0]['MaximumCall'].update(StartTimestamp=6, EndTimestamp=11)
        with self.assertRaisesRegex(ValueError, 'outside its interval'):
            validate_timing(self.capture)

    def test_rejects_timestamp_difference_that_does_not_match_histogram(self):
        self.capture['Intervals'][0]['MaximumCall']['EndTimestamp'] = 8
        with self.assertRaisesRegex(ValueError, 'differs from retained histogram'):
            validate_timing(self.capture)

    def test_rejects_clock_bracket_after_capture_starts(self):
        self.capture['TraceClock']['AfterTimestamp'] = 4
        with self.assertRaisesRegex(ValueError, 'clock bracket'):
            validate_timing(self.capture)

    def test_rejects_missing_timing_and_invalid_frequency(self):
        del self.capture['Intervals'][0]['MaximumCall']
        with self.assertRaises(KeyError):
            validate_timing(self.capture)
        self.capture['StopwatchFrequency'] = 0
        with self.assertRaisesRegex(ValueError, 'frequency'):
            validate_timing(self.capture)


if __name__ == '__main__':
    unittest.main()
