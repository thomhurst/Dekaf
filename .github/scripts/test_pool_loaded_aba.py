import importlib.util
from pathlib import Path
import subprocess
import tempfile
import threading
import unittest
from unittest.mock import patch

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
