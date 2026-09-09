import unittest
import run


class HandlerStageTests(unittest.TestCase):
    def setUp(self):
        self.producer = dict(ScheduledStart=100, OfferBurst=2, Rate=2)
        self.metrics = dict(Completed=4, Measured=2, WarmupCompleted=2, StopwatchFrequency=10)

    def test_preserves_largest_total_and_handler_for_same_record(self):
        result = run.summarize_stages([101, 102, 112, 118], [2, 3, 30, 9], self.producer, self.metrics)
        self.assertEqual(result['measured'], 2)
        largest = result['largest_total'][0]
        self.assertEqual(largest, dict(sequence=2, scheduled=110, handler_start=112,
                                     completed=140, before_handler_ticks=2, handler_ticks=28, total_ticks=30))
        self.assertEqual(result['largest_handler'][0], largest)

    def test_rejects_missing_start(self):
        with self.assertRaisesRegex(ValueError, 'Missing handler stage samples'):
            run.summarize_stages([101], [2, 3, 4, 9], self.producer, self.metrics)

    def test_rejects_start_before_offer_or_after_completion(self):
        for start in (109, 115):
            with self.subTest(start=start), self.assertRaisesRegex(ValueError, 'Invalid handler stage boundary'):
                run.summarize_stages([101, 102, start, 118], [2, 3, 4, 9], self.producer, self.metrics)

    def test_rejects_wrong_measured_denominator(self):
        self.metrics['Measured'] = 3
        with self.assertRaisesRegex(ValueError, 'Incorrect handler stage denominator'):
            run.summarize_stages([101, 102, 112, 118], [2, 3, 4, 9], self.producer, self.metrics)


if __name__ == '__main__':
    unittest.main()
