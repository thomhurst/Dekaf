import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import runner_resources as runner


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

    def test_snapshot_retains_raw_pressure_and_steal_counters(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ('stat', 'meminfo', 'vmstat', 'loadavg', 'pressure/cpu', 'pressure/memory', 'pressure/io'):
                path = root / name
                path.parent.mkdir(exist_ok=True)
                path.write_text(name + ' raw counters')
            with patch.object(os, 'statvfs', create=True) as disk:
                disk.return_value.f_bavail, disk.return_value.f_frsize = 7, 4096
                result = runner.snapshot(root)
            self.assertEqual(result['stat'], 'stat raw counters')
            self.assertEqual(result['pressure/cpu'], 'pressure/cpu raw counters')
            self.assertEqual(result['disk_available_bytes'], 7 * 4096)
            (root / 'pressure/cpu').unlink()
            with self.assertRaises(FileNotFoundError):
                runner.snapshot(root)

    def test_child_failure_remains_failure_and_samples_are_retained(self):
        with tempfile.TemporaryDirectory() as directory, \
             patch.object(runner, 'configure_affinity', return_value=([], {})), \
             patch.object(runner, 'snapshot', return_value={'raw': 'sample'}):
            output = Path(directory) / 'results'
            code = runner.monitor([sys.executable, '-c', 'raise SystemExit(7)'], output)
            self.assertEqual(code, 7)
            self.assertEqual(json.loads((output / 'completion.json').read_text())['exit_code'], 7)
            self.assertGreaterEqual(len((output / 'series.jsonl').read_text().splitlines()), 2)

    def test_missing_counters_stop_before_workload_launch(self):
        with tempfile.TemporaryDirectory() as directory, \
             patch.object(runner, 'configure_affinity', return_value=([], {})), \
             patch.object(runner, 'snapshot', side_effect=OSError('missing PSI')), \
             patch.object(subprocess, 'Popen') as launch:
            with self.assertRaisesRegex(OSError, 'missing PSI'):
                runner.monitor(['workload'], Path(directory) / 'results')
            launch.assert_not_called()

    def test_sampling_failure_terminates_running_process_group(self):
        with tempfile.TemporaryDirectory() as directory, \
             patch.object(runner, 'configure_affinity', return_value=([], {})), \
             patch.object(runner, 'snapshot', side_effect=[{'raw': 'sample'}, OSError('counter failed')]), \
             patch.object(subprocess, 'Popen') as launch, \
             patch.object(os, 'killpg', create=True) as stop:
            child = launch.return_value
            child.poll.return_value = None
            child.pid = 123
            with self.assertRaisesRegex(OSError, 'counter failed'):
                runner.monitor(['workload'], Path(directory) / 'results')
            stop.assert_called_once_with(123, runner.signal.SIGTERM)
            child.wait.assert_called_once_with(timeout=5)


def linux_smoke():
    rows = runner.topology()
    layout = runner.select_affinity(rows)
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
