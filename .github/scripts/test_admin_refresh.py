import importlib.util
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch
import admin_profile


ROOT = Path(__file__).resolve().parents[2]


class CalibrationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        with patch.dict(os.environ, PR='3128', BASELINE_SHA='a' * 40, CANDIDATE_SHA='a' * 40):
            spec = importlib.util.spec_from_file_location('admin_refresh_test', ROOT / '.github/scripts/admin_refresh.py')
            self.driver = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(self.driver)
        self.driver.ROOT = self.root
        self.driver.OUT = self.root / 'evidence'
        self.driver.SOURCE = self.root / 'fixture'
        self.driver.SOURCE.mkdir()
        (self.driver.SOURCE / 'Runner.csproj').write_text('<Project/>')
        helper = self.root / '.github/benchmarks/CompilationLog.cs'
        helper.parent.mkdir(parents=True)
        helper.write_text('// fixture')
        self.commands = []
        self.common = SimpleNamespace(
            CONTROLS=['legacy:16'], NEW_CASES=['new:16'], validate_probe=Mock(return_value={}),
            verify_copied_tree=Mock(), retain_loaded_binaries=Mock(),
            compare=Mock(return_value={'point_estimates_within_declared_limits': False}))
        self.common.validate = self.common.validate_probe
        self.common.validate_probe.return_value = dict(
            TraceClock=dict(BeforeTimestamp=1, AfterTimestamp=2), Start=dict(Timestamp=3),
            StopwatchFrequency=1000, Intervals=[dict(Start=dict(Timestamp=3), End=dict(Timestamp=10),
                MaximumCall=dict(StartTimestamp=4, EndTimestamp=9, Ticks=5), Latencies=[dict(Ticks=5, Count=1)])])
        tools = self.root / '.config/stress-diagnostics/dotnet-tools.json'
        tools.parent.mkdir(parents=True)
        tools.write_text('{"tools":{"dotnet-trace":{"version":"10.0.731102"}}}')

    def fake_run(self, command, log, **kwargs):
        self.commands.append(list(map(str, command)))
        if command[:2] == ['dotnet', 'build']:
            output = Path(command[2]).parent / 'bin/Release/net10.0'
            output.mkdir(parents=True)
            (output / 'Dekaf.Benchmarks.dll').write_bytes(b'identical compiled fixture')
        if 'collect' in command:
            output = Path(command[command.index('--output') + 1])
            output.parent.mkdir(parents=True, exist_ok=True)
            output.write_bytes(b'trace')
            (output.parent / 'jit.asm').write_bytes(b'code')

    def execute(self, calibration, profiling=False):
        with patch.dict(os.environ, ADMIN_CALIBRATION='1' if calibration else '0', ADMIN_PILOT='0', ADMIN_PROFILE='1' if profiling else '0'), \
             patch.object(self.driver, 'configure_affinity', return_value=([], {'consumer': '2,3', 'infrastructure': '0,1'})), \
             patch.object(self.driver, 'module', side_effect=lambda path, name: admin_profile if name == 'admin_profile' else self.common), \
             patch.object(self.driver, 'run', side_effect=self.fake_run), \
             patch.object(self.driver.subprocess, 'check_output', return_value='a' * 40):
            self.driver.execute()

    def test_profile_collects_three_matching_processes_and_retains_diagnostic_verdict(self):
        self.driver.PR = 3138
        self.execute(False, profiling=True)
        builds = [row for row in self.commands if row[:2] == ['dotnet', 'build']]
        self.assertEqual(len(builds), 1)
        traces = [row for row in self.commands if 'collect' in row]
        self.assertEqual(len(traces), 3)
        self.assertEqual(len({row[row.index('probe') - 1] for row in traces}), 1)
        self.assertTrue(all(row[-2:] == ['480', '180'] for row in traces))
        self.assertTrue(all(row[row.index('probe') + 1] == 'legacy-delete:16' for row in traces))
        import json
        report = json.loads((self.driver.OUT / 'calibration.json').read_text())
        self.assertIn('never product acceptance', report['verdict'])

    def test_calibration_runs_one_binary_in_all_fresh_processes_and_skips_new_apis(self):
        self.execute(True)
        builds = [row for row in self.commands if row[:2] == ['dotnet', 'build']]
        self.assertEqual(len(builds), 1)
        self.assertIn('-p:Candidate=false', builds[0])
        probes = [row for row in self.commands if 'probe' in row]
        self.assertEqual(len(probes), 5)  # Two validation launches, then three measured launches.
        self.assertEqual(len({row[4] for row in probes}), 1)
        self.assertTrue(all(row[6] == 'legacy:16' for row in probes))
        self.assertEqual([row[-2:] for row in probes], [['.2', '.2']] * 2 + [['480', '180']] * 3)
        import json
        report = json.loads((self.driver.OUT / 'calibration.json').read_text())
        self.assertFalse(report['point_estimates_within_declared_limits'])
        self.assertIn('never product acceptance', report['verdict'])

    def test_calibration_rejects_different_product_before_build(self):
        self.driver.B = 'b' * 40
        with self.assertRaisesRegex(ValueError, 'identical exact product'):
            self.execute(True)
        self.assertEqual(self.commands, [])

    def test_comparison_still_builds_both_fixtures_and_measures_candidate_only_cases(self):
        self.driver.B = 'b' * 40
        self.execute(False)
        builds = [row for row in self.commands if row[:2] == ['dotnet', 'build']]
        self.assertEqual(len(builds), 2)
        self.assertIn('-p:Candidate=true', builds[1])
        probes = [row for row in self.commands if 'probe' in row]
        self.assertEqual(len({row[4] for row in probes}), 2)
        self.assertEqual(probes[-1][6], 'new:16')
        self.assertFalse((self.driver.OUT / 'calibration.json').exists())

    def test_all_four_deployed_probes_match_the_tested_recorder(self):
        probes = list(ROOT.glob('tools/Admin*Evidence/Probe.cs'))
        self.assertEqual(len(probes), 4)
        self.assertEqual(len({path.read_bytes() for path in probes}), 1)


if __name__ == '__main__':
    unittest.main()
