import unittest
from analyze_admin_sampler_control import call_bounds, overlap


class ClockBoundsTests(unittest.TestCase):
    def test_anchor_uncertainty_moves_both_call_endpoints_together(self):
        result=call_bounds(dict(StartTimestamp=1100,EndTimestamp=1200),dict(BeforeTimestamp=1000,AfterTimestamp=1010),500,1000)
        self.assertEqual(result,dict(start_earliest_ms=590,start_latest_ms=600,end_earliest_ms=690,end_latest_ms=700))
        self.assertEqual(overlap(result,610,650),'guaranteed within clock bounds')
        self.assertEqual(overlap(result,580,595),'possible within clock bounds')
        self.assertEqual(overlap(result,700,800),'none')
        self.assertEqual(overlap(result,100,590),'none')

    def test_zero_uncertainty_retains_touching_boundary_as_no_overlap(self):
        result=call_bounds(dict(StartTimestamp=1100,EndTimestamp=1200),dict(BeforeTimestamp=1000,AfterTimestamp=1000),0,1000)
        self.assertEqual(overlap(result,200,300),'none')
        self.assertEqual(overlap(result,199,300),'guaranteed within clock bounds')


if __name__ == '__main__':
    unittest.main()
