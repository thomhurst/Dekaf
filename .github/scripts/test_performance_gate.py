import contextlib
import io
import json
import os
import re
import shutil
import subprocess
import tempfile
import textwrap
import unittest
from pathlib import Path
from unittest import mock

import performance_gate as gate


REPO_ROOT = Path(__file__).resolve().parents[2]
UNIT_FIXTURES = REPO_ROOT / 'tools' / 'Dekaf.Benchmarks' / 'Benchmarks' / 'Unit'
WORKFLOW = REPO_ROOT / '.github' / 'workflows' / 'performance-gate.yml'
SINGLE_INVOCATION = re.compile(r'IterationSetup|IterationCleanup|InvocationCount|RunStrategy\.(ColdStart|Monitoring)')


def benchmark(method='Append', parameters='', mean=100.0, samples=25, allocated=0, warmup_seconds=50.0,
              warmup_iterations=50):
    measurements = [{'IterationMode': 'Workload', 'IterationStage': 'Warmup',
                     'Nanoseconds': warmup_seconds * 1e9 / warmup_iterations} for _ in range(warmup_iterations)]
    measurements += [{'IterationMode': 'Workload', 'IterationStage': 'Actual', 'Nanoseconds': mean * 1000}
                     for _ in range(samples)]
    metrics = [] if allocated is None else [{'Descriptor': {'Id': 'Allocated Memory'}, 'Value': allocated}]
    return {'Namespace': 'Dekaf.Benchmarks.Benchmarks.Unit', 'Type': 'AppendBenchmarks', 'Method': method,
            'Parameters': parameters, 'Statistics': {'Mean': mean, 'StandardDeviation': 1.0, 'N': samples},
            'Metrics': metrics, 'Measurements': measurements}


class SelectionTests(unittest.TestCase):
    def test_longest_prefix_selects_the_area_not_the_core_set(self):
        selection = gate.select(['src/Dekaf/Producer/RecordAccumulator.cs'])
        self.assertEqual(['producer'], selection['areas'])
        self.assertIn('*.Unit.AccumulatorAppendBenchmarks.*', selection['filters'])
        self.assertNotIn('*.Unit.ConsumerHotPathBenchmarks.*', selection['filters'])

    def test_unmapped_core_files_and_build_inputs_use_the_core_set(self):
        for path in ('src/Dekaf/KafkaClient.cs', 'src/Dekaf/Internal/Pools.cs', 'global.json', 'Directory.Packages.props'):
            with self.subTest(path=path):
                selection = gate.select([path])
                self.assertTrue(selection['applicable'])
                self.assertEqual(gate.CORE, selection['filters'])

    def test_non_product_and_non_source_files_are_ignored(self):
        for path in ('tests/Dekaf.Tests.Unit/X.cs', 'docs/x.md', 'src/Dekaf/PublicAPI.Unshipped.txt',
                     'tools/Dekaf.Benchmarks/Benchmarks/Unit/X.cs', '.github/workflows/ci.yml'):
            with self.subTest(path=path):
                selection = gate.select([path])
                self.assertFalse(selection['applicable'])
                self.assertEqual([], selection['considered_files'])

    def test_not_applicable_areas_are_explained_without_fixtures(self):
        selection = gate.select(['src/Dekaf.Outbox/Relay.cs', 'src/Dekaf.Compression.Lz4/Codec.cs'])
        self.assertFalse(selection['applicable'])
        self.assertEqual({'outbox', 'compression'}, {item['area'] for item in selection['not_applicable']})
        self.assertTrue(all(item['reason'] for item in selection['not_applicable']))

    def test_multiple_areas_union_filters_without_duplicates(self):
        selection = gate.select(['src/Dekaf/Protocol/Reader.cs', 'src/Dekaf/Consumer/KafkaConsumer.cs'])
        self.assertEqual(['protocol', 'consumer'], selection['areas'])
        self.assertEqual(len(selection['filters']), len(set(selection['filters'])))
        self.assertIn('*.Unit.FetchResponseParsingBenchmarks.*', selection['filters'])

    def test_windows_paths_are_normalized(self):
        self.assertEqual(['networking'], gate.select(['src\\Dekaf\\Networking\\KafkaConnection.cs'])['areas'])

    def test_every_mapped_class_is_a_steady_state_fixture_with_memory_diagnoser(self):
        sources = {path: path.read_text(encoding='utf-8') for path in UNIT_FIXTURES.glob('*.cs')}
        classes = {name for _, _, filters, _ in gate.AREAS for name in (item.split('.')[2] for item in filters)}
        for name in sorted(classes):
            with self.subTest(fixture=name):
                owners = [path for path, text in sources.items() if re.search(rf'\bclass {name}\b', text)]
                self.assertEqual(1, len(owners), f'{name} must be defined once under tools/Dekaf.Benchmarks/Benchmarks/Unit')
                text = sources[owners[0]]
                self.assertIn('MemoryDiagnoser', text, f'{name} needs [MemoryDiagnoser] for 0 B/op evidence')
                self.assertIsNone(SINGLE_INVOCATION.search(text),
                                  f'{owners[0].name} uses a single-invocation fixture and cannot reach the warmup floor')

    def test_case_listing_counts_only_benchmark_lines(self):
        listing = '// Validating benchmarks\nDekaf.Benchmarks.Benchmarks.Unit.A.M\n\nDekaf.Benchmarks.Benchmarks.Unit.A.N(X: 1)\n'
        self.assertEqual(2, gate.count_listed_cases(listing))

    def test_execution_total_includes_parameterized_and_failed_cases(self):
        log = ('// ***** Found 31 benchmark(s) in total *****\n'
               '// Found 4 benchmarks:\n// Found 27 benchmarks:\n'
               '// Benchmark process exited with code 1\n')
        self.assertEqual(31, gate.count_execution_cases(log))
        fatal, _ = gate.validate_phase([benchmark()], gate.count_execution_cases(log), None, 0)
        self.assertIn('expected 31 cases, found 1', fatal)

    def test_execution_count_rejects_missing_or_ambiguous_totals(self):
        line = '// ***** Found 31 benchmark(s) in total *****\n'
        for log in ('// Found 31 benchmarks:\n', line + line):
            with self.subTest(log=log), self.assertRaises(ValueError):
                gate.count_execution_cases(log)

    def test_execution_count_enforces_expanded_case_budget(self):
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / 'dry.log'
            log.write_text(f'// ***** Found {gate.MAX_CASES + 1} benchmark(s) in total *****\n', encoding='utf-8')
            with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(1, gate.main(['count', '--execution-log', str(log)]))
                self.assertEqual(0, gate.main(['count', '--execution-log', str(log), '--max-cases', str(gate.MAX_CASES + 1)]))

    def test_count_command_rejects_empty_and_oversized_selections(self):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            with mock.patch('sys.stdin', io.StringIO('')):
                self.assertEqual(1, gate.main(['count']))
            listing = '\n'.join(f'Dekaf.Benchmarks.Benchmarks.Unit.A.M{i}' for i in range(gate.MAX_CASES + 1))
            with mock.patch('sys.stdin', io.StringIO(listing)):
                self.assertEqual(1, gate.main(['count']))
            with mock.patch('sys.stdin', io.StringIO(listing)):
                self.assertEqual(0, gate.main(['count', '--max-cases', str(gate.MAX_CASES + 1)]))


class ValidationTests(unittest.TestCase):
    def test_complete_phase_has_no_findings(self):
        fatal, warnings = gate.validate_phase([benchmark(), benchmark('Drain')], 2, 25, 20)
        self.assertEqual(([], []), (fatal, warnings))

    def test_incomplete_phases_are_fatal_with_named_cases(self):
        cases = {
            'count': ([benchmark()], 2),
            'iterations': ([benchmark(samples=24)], 1),
            'allocation': ([benchmark(allocated=None)], 1),
            'mean': ([benchmark(mean=0)], 1),
            'duplicate': ([benchmark(), benchmark()], 2),
        }
        for label, (benchmarks, expected) in cases.items():
            with self.subTest(label=label):
                fatal, _ = gate.validate_phase(benchmarks, expected, 25, 20)
                self.assertTrue(fatal, label)
                if label != 'count':
                    self.assertTrue(any('AppendBenchmarks' in issue or 'duplicate' in issue for issue in fatal))

    def test_short_warmup_is_a_warning_not_a_fatal_issue(self):
        fatal, warnings = gate.validate_phase([benchmark(warmup_seconds=16.7)], 1, 25, 20)
        self.assertEqual([], fatal)
        self.assertEqual(1, len(warnings))
        self.assertIn('16.7 s below 20 s', warnings[0])
        self.assertIn('INCONCLUSIVE', warnings[0])

    def test_dry_validation_ignores_iterations_and_warmup(self):
        fatal, warnings = gate.validate_phase([benchmark(samples=1, warmup_seconds=0)], 1, None, 0)
        self.assertEqual(([], []), (fatal, warnings))

    def test_validate_command_writes_sorted_merged_cases_and_exit_code(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            results = root / 'phase' / 'results'
            results.mkdir(parents=True)
            (results / 'Dekaf.Benchmarks-report-full.json').write_text(
                json.dumps({'Benchmarks': [benchmark('Zeta'), benchmark('Alpha', warmup_seconds=10)]}), encoding='utf-8')
            merged, cases = root / 'phase.json', root / 'phase-cases.json'
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                code = gate.main(['validate', '--phase', str(root / 'phase'), '--expected', '2',
                                  '--merged', str(merged), '--cases', str(cases)])
            self.assertEqual(0, code)
            self.assertIn('::warning::phase:', output.getvalue())
            self.assertEqual(['Alpha', 'Zeta'], [item['Method'] for item in json.loads(merged.read_text(encoding='utf-8'))])
            self.assertEqual(2, len(json.loads(cases.read_text(encoding='utf-8'))))
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, gate.main(['validate', '--phase', str(root / 'phase'), '--expected', '3']))


class WorkflowTests(unittest.TestCase):
    @unittest.skipUnless(os.name != 'nt' and shutil.which('bash') and shutil.which('jq'), 'requires bash and jq')
    def test_manual_pins_reject_stale_closed_and_unrelated_candidates(self):
        workflow = WORKFLOW.read_text(encoding='utf-8')
        script = textwrap.dedent(workflow.split('          git fetch --no-tags origin main\n', 1)[1]
                                .split('          echo "base=$base"', 1)[0])
        commands = '''
git() {
  case "$1 $2" in
    'rev-parse origin/main') echo "$TEST_MAIN" ;;
    'rev-parse HEAD') echo "$TEST_CHECKOUT" ;;
    'merge-base --is-ancestor') [[ "$TEST_ANCESTOR" == true ]] ;;
    'fetch --no-tags') return 0 ;;
    *) return 1 ;;
  esac
}
gh() { printf '%s' "$TEST_PR_JSON"; }
'''
        baseline, candidate = 'a' * 40, 'b' * 40
        valid = dict(BASE_INPUT=baseline, HEAD_INPUT=candidate, PR_INPUT='42',
                     TEST_MAIN=baseline, TEST_CHECKOUT=baseline, TEST_ANCESTOR='true',
                     TEST_PR_JSON=json.dumps({'state': 'open', 'head': {'sha': candidate}}))
        cases = [{}, {'BASE_INPUT': 'c' * 40}, {'HEAD_INPUT': 'c' * 40},
                 {'TEST_CHECKOUT': 'c' * 40}, {'TEST_ANCESTOR': 'false'}, {'PR_INPUT': 'invalid'},
                 {'BASE_INPUT': ''}, {'HEAD_INPUT': 'branch'},
                 {'TEST_PR_JSON': json.dumps({'state': 'closed', 'head': {'sha': candidate}})}]
        with tempfile.TemporaryDirectory() as directory:
            for changes in cases:
                with self.subTest(changes=changes):
                    env = dict(os.environ, GATE=directory, GITHUB_EVENT_NAME='workflow_dispatch',
                               GITHUB_REPOSITORY='owner/repository', **(valid | changes))
                    result = subprocess.run(['bash', '-euo', 'pipefail', '-c', commands + script],
                                            env=env, capture_output=True, text=True)
                    self.assertEqual(not changes, result.returncode == 0, result.stderr)

    def test_gate_workflow_measures_a1_b_a2_on_one_hosted_vm_with_the_shared_screen(self):
        workflow = WORKFLOW.read_text(encoding='utf-8')
        self.assertIn('pull_request:', workflow)
        self.assertIn('runs-on: ubuntu-latest', workflow)
        self.assertIn('for phase in A1 B A2', workflow)
        for setting in ('--warmupCount 50', '--iterationCount 25', '--iterationTime 1000', '--launchCount 1',
                        '--outliers DontRemove', '--exporters fulljson'):
            self.assertIn(setting, workflow, setting)
        self.assertIn('performance_gate.py select', workflow)
        self.assertIn('performance_gate.py validate', workflow)
        self.assertIn('--argjson alloc_floor 8', workflow)
        self.assertIn('--argjson min_warmup 20', workflow)
        self.assertIn('compare_bdn_reports.jq', workflow)
        self.assertIn('dotnet build-server shutdown', workflow)
        self.assertNotIn('runs-on: ubicloud', workflow)


if __name__ == '__main__':
    unittest.main()
