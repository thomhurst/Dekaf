import importlib.util
from pathlib import Path
import subprocess
import tempfile
import threading
import unittest
from unittest.mock import patch
from unittest.mock import Mock

spec = importlib.util.spec_from_file_location('driver', Path(__file__).with_name('pool_loaded_aba.py'))
driver = importlib.util.module_from_spec(spec)
spec.loader.exec_module(driver)


class BrokerSamplerTests(unittest.TestCase):
    def test_stop_preserves_final_sample(self):
        stop, errors = threading.Event(), []
        def sample(*args, **kwargs):
            self.assertIn('--no-stream', args[0])
            self.assertEqual(kwargs['timeout'], 10)
            stop.set()
            return subprocess.CompletedProcess(args[0], 0, '{"CPUPerc":"1%","MemUsage":"1MiB / 1GiB"}')
        with tempfile.TemporaryDirectory() as folder, patch.object(driver.subprocess, 'run', side_effect=sample):
            path = Path(folder) / 'stats.jsonl'
            driver.broker_samples('broker', path, stop, errors)
            self.assertEqual(len(path.read_text().splitlines()), 1)
            self.assertEqual(errors, [])

    def test_failure_reaches_owner(self):
        failure = subprocess.TimeoutExpired('docker', 10)
        with tempfile.TemporaryDirectory() as folder, patch.object(driver.subprocess, 'run', side_effect=failure):
            errors = []
            driver.broker_samples('broker', Path(folder) / 'stats.jsonl', threading.Event(), errors)
            self.assertEqual(errors, [failure])


class ProfileWindowTests(unittest.TestCase):
    def test_client_exit_before_marker_is_failure(self):
        stop, errors = threading.Event(), []
        stop.set()
        with tempfile.TemporaryDirectory() as folder:
            driver.profile_windows(Path(folder), stop, errors, Mock(), Path('dotnet-trace'))
        self.assertEqual(len(errors), 1)
        self.assertIn('before profiling measurement marker', str(errors[0]))

    def test_declared_windows_attach_to_exact_client(self):
        dispatch = Mock()
        dispatch.AFFINITY = {'infrastructure': '0,1'}
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)
            (path / 'measured-start.json').write_text('{"ProcessId":123,"StartedUtc":"2000-01-01T00:00:00+00:00"}')
            errors = []
            driver.profile_windows(path, threading.Event(), errors, dispatch, Path('dotnet-trace'))
        self.assertEqual(errors, [])
        self.assertEqual(dispatch.command.call_count, 2)
        for call in dispatch.command.call_args_list:
            command = call.args[0]
            self.assertEqual(command[command.index('--process-id') + 1], 123)
            self.assertEqual(command[command.index('--profile') + 1], 'gc-verbose')
