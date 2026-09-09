import json
import os
import subprocess
import sys
import unittest
from unittest.mock import patch

import runner_affinity as runner


class RunnerTests(unittest.TestCase):
    def test_smt_siblings_remain_together_across_sockets(self):
        layout = runner.select_affinity([(0, 0, 0), (2, 0, 0), (1, 0, 1), (3, 0, 1)])
        self.assertEqual(layout, {'consumer': '1,3', 'infrastructure': '0,2'})

    def test_single_physical_core_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'two physical cores'):
            runner.select_affinity([(0, 0, 0), (1, 0, 0)])

    def test_driver_and_subprocesses_inherit_only_infrastructure_cpus(self):
        rows = [(0, 0, 0), (1, 1, 0), (2, 0, 0), (3, 1, 0)]
        with patch.object(runner, 'topology', return_value=rows), \
             patch.object(os, 'sched_setaffinity', create=True) as pin, \
             patch.dict(os.environ, {}, clear=True):
            self.assertEqual(runner.configure_affinity()[0], rows)
            pin.assert_called_once_with(0, {0, 2})
            self.assertEqual(os.environ['DEKAF_RUNNER_CPUS'], '0,1,2,3')


def linux_smoke():
    rows, layout = runner.configure_affinity()
    infrastructure = set(map(int, layout['infrastructure'].split(',')))
    consumer = set(map(int, layout['consumer'].split(',')))
    if set(os.sched_getaffinity(0)) != infrastructure:
        raise RuntimeError('Harness did not inherit infrastructure affinity')
    # A measured process must be able to use the reserved core, while its parent stays isolated.
    command = ['taskset', '-c', layout['consumer'], sys.executable, '-c',
               'import os,json; print(json.dumps(sorted(os.sched_getaffinity(0))))']
    actual = set(json.loads(subprocess.check_output(command, text=True)))
    if actual != consumer or actual & infrastructure:
        raise RuntimeError('Measured child affinity differs or overlaps infrastructure')
    print(json.dumps({'infrastructure': sorted(infrastructure), 'client': sorted(actual)}))


if __name__ == '__main__':
    if sys.argv[1:] == ['--linux-smoke']:
        linux_smoke()
    else:
        unittest.main()
