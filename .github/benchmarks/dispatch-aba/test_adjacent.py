import unittest
import run_adjacent


class AdjacentScheduleTests(unittest.TestCase):
    def test_loaded_only_preserves_every_public_mode_and_both_revision_validation(self):
        plan = list(run_adjacent.schedule(loaded_only=True))
        self.assertEqual(len(plan), 20)
        self.assertTrue(all(item['kind'] == 'loaded' for item in plan))
        self.assertTrue(all(item['smoke'] for item in plan[:8]))
        for index in range(0, 8, 2):
            self.assertEqual([item['label'] for item in plan[index:index+2]], ['A','B'])
        for index in range(8, 20, 3):
            group = plan[index:index+3]
            self.assertEqual([item['phase'] for item in group], ['A1','B','A2'])
            self.assertEqual(len({item['name'] for item in group}), 1)
        self.assertEqual({item['name'] for item in plan}, set(run_adjacent.run.MODES))

    def test_all_both_revision_validations_precede_measurement(self):
        plan = list(run_adjacent.schedule())
        self.assertEqual(len(plan), 70)
        self.assertTrue(all(item['smoke'] for item in plan[:28]))
        self.assertTrue(all(not item['smoke'] for item in plan[28:]))
        for index in range(0, 28, 2):
            self.assertEqual([item['label'] for item in plan[index:index+2]], ['A', 'B'])

    def test_controls_are_adjacent_for_every_identical_workload(self):
        measured = [item for item in run_adjacent.schedule() if not item['smoke']]
        for index in range(0, len(measured), 3):
            group = measured[index:index+3]
            self.assertEqual([item['phase'] for item in group], ['A1', 'B', 'A2'])
            self.assertEqual([item['label'] for item in group], ['A', 'B', 'A'])
            self.assertEqual(len({(item['kind'], item['name']) for item in group}), 1)
            self.assertTrue(all(item['value'] == group[0]['value'] for item in group))

    def test_every_original_case_is_preserved(self):
        a1 = [item for item in run_adjacent.schedule() if item['phase'] == 'A1']
        self.assertEqual(sum(item['kind'] == 'micro' for item in a1), 6)
        self.assertEqual(sum(item['kind'] == 'loaded' for item in a1), 4)
        self.assertEqual(sum(item['kind'] == 'shutdown' for item in a1), 4)


if __name__ == '__main__':
    unittest.main()
