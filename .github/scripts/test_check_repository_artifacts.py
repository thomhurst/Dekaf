import contextlib
import io
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

import check_repository_artifacts as policy


class ArtifactPolicyTests(unittest.TestCase):
    def test_generated_outputs_are_rejected_in_any_directory(self):
        for path in (
            'docs/performance-evidence/pr-3083/README.md',
            'tools/Dekaf.Benchmarks/BenchmarkDotNet.Artifacts/results/report.json',
            'other/run.LOG', 'capture.nettrace', 'capture.speedscope.json',
            'nested/build.binlog', 'TestResults/result.trx', '.artifacts/source.zip',
            'reports/Fixture-report-full.json', 'reports/Fixture-report-github.md',
            'reports/Fixture-report.csv', 'reports/Fixture-report.html',
            'tools/Dekaf.Benchmarks/BenchmarkResults/summary.json',
            r'docs\performance-evidence\plan.md',
            'tools/Dekaf.Benchmarks/Benchmarks/Unit/ShareConsumerAllocations.md',
            'tools/Dekaf.Benchmarks/Experiment.md',
        ):
            with self.subTest(path=path):
                self.assertIsNotNone(policy.violation(path))

    def test_product_docs_and_real_fixture_data_remain_allowed(self):
        for path in (
            'README.md', 'CLAUDE.md', 'docs/docs/guides/performance.md',
            '.github/benchmarks/STANDARD-TOOLS.md', '.github/scripts/performance_gate.py',
            'tests/Fixtures/protocol.json', 'tests/Fixtures/records.csv',
            'tools/Dekaf.Benchmarks/Benchmarks/Unit/OutboxBenchmarks.cs',
            'tools/Dekaf.Benchmarks/Dekaf.Benchmarks.csproj',
            'tools/Dekaf.StressTests/Dekaf.StressTests.csproj',
            'tools/Dekaf.Benchmarks/WORKFLOW.md',
        ):
            with self.subTest(path=path):
                self.assertIsNone(policy.violation(path))

    def test_shell_scripts_cannot_generate_benchmark_projects(self):
        for path in ('scripts/Compare.ps1', 'scripts/renamed.sh'):
            for body in ('<Project Sdk="Microsoft.NET.Sdk"><PackageReference Include="BenchmarkDotNet" /></Project>',
                         'dotnet new console\ndotnet add package BenchmarkDotNet'):
                with self.subTest(path=path, body=body):
                    self.assertIsNotNone(policy.violation(path, body))
            self.assertIsNone(policy.violation(path,
                'dotnet run --project tools/Dekaf.Benchmarks -c Release -- --filter "*Case*"'))

    def test_complete_tree_check_finds_unchanged_inherited_outputs(self):
        original = Path.cwd()
        with tempfile.TemporaryDirectory() as directory:
            try:
                os.chdir(directory)
                def git(*args):
                    return subprocess.check_output(['git', *args], stderr=subprocess.DEVNULL).decode().strip()
                git('init', '-q')
                git('config', 'user.email', 'policy@example.test')
                git('config', 'user.name', 'Policy test')
                Path('inherited.log').write_text('old output')
                git('add', '.')
                git('commit', '-qm', 'baseline')
                base = git('rev-parse', 'HEAD')
                Path('README.md').write_text('Product documentation')
                git('add', '.')
                git('commit', '-qm', 'docs only')
                with contextlib.redirect_stdout(io.StringIO()) as output:
                    self.assertEqual(0, policy.main(['--base', base]))
                    self.assertEqual(1, policy.main(['--all-tracked']))
                self.assertIn('inherited.log', output.getvalue())
                git('rm', 'inherited.log')
                git('commit', '-qm', 'remove inherited output')
                with contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0, policy.main(['--all-tracked']))
            finally:
                os.chdir(original)

    def test_legacy_and_renamed_standalone_projects_are_rejected(self):
        for path in (
            '.github/benchmarks/outbox-commit/Harness.csproj',
            '.github/benchmarks/dispatch-aba/DispatchBenchmarks.cs',
            '.github/benchmarks/share-loaded/run.py',
            'tools/AdminMutationEvidence/Program.cs',
            'scratch/Benchmarks.csproj', 'scratch/Harness.csproj',
            'tools/PerfRunner/Runner.csproj', 'tools/Experiment/Runner.fsproj',
        ):
            with self.subTest(path=path):
                self.assertIsNotNone(policy.violation(path))
        for reference in ('<PackageReference Include="BenchmarkDotNet" />',
                          "<Reference Include='BenchmarkDotNet' />"):
            self.assertIsNotNone(policy.violation('scratch/Runner.csproj', reference))
            self.assertIsNone(policy.violation('tools/Dekaf.Benchmarks/Dekaf.Benchmarks.csproj', reference))
            self.assertIsNone(policy.violation('tests/Dekaf.DocTests/Dekaf.DocTests.csproj', reference))
        for project in policy.TOOL_PROJECTS:
            self.assertIsNone(policy.violation(project, '<OutputType>Exe</OutputType>'))

    def test_git_diff_allows_cleanup_and_checks_modified_and_renamed_outputs(self):
        original = Path.cwd()
        with tempfile.TemporaryDirectory() as directory:
            try:
                os.chdir(directory)
                def git(*args):
                    return subprocess.check_output(['git', *args], stderr=subprocess.DEVNULL).decode().strip()
                git('init', '-q')
                git('config', 'user.email', 'policy@example.test')
                git('config', 'user.name', 'Policy test')
                Path('old.log').write_text('old')
                Path('keep.log').write_text('before')
                git('add', '.')
                git('commit', '-qm', 'baseline')
                base = git('rev-parse', 'HEAD')
                git('rm', 'old.log')
                Path('keep.log').write_text('after')
                Path('README.md').write_text('Product documentation')
                git('add', '.')
                git('commit', '-qm', 'change')
                self.assertEqual(['README.md', 'keep.log'], policy.changed_paths(base, 'HEAD'))
                with contextlib.redirect_stdout(io.StringIO()) as output:
                    self.assertEqual(1, policy.main(['--base', base]))
                self.assertIn('keep.log', output.getvalue())
                git('mv', 'keep.log', 'renamed.log')
                git('commit', '-qm', 'rename')
                self.assertIn('renamed.log', policy.changed_paths(base, 'HEAD'))
                git('rm', 'renamed.log')
                git('commit', '-qm', 'cleanup')
                with contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0, policy.main(['--base', base]))
            finally:
                os.chdir(original)


if __name__ == '__main__':
    unittest.main()
