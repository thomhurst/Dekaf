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
            'FullName': f'Dekaf.Benchmarks.Benchmarks.Unit.AppendBenchmarks.{method}'
                        + (f'({parameters.replace("=", ": ")})' if parameters else ''),
            'Parameters': parameters, 'Statistics': {'Mean': mean, 'StandardDeviation': 1.0, 'N': samples},
            'Metrics': metrics, 'Measurements': measurements}


class SelectionTests(unittest.TestCase):
    def test_admin_changes_cover_consumer_assignment_decoding(self):
        selection = gate.select(['src/Dekaf/Admin/AdminClient.cs'])
        self.assertIn('*.Unit.ConsumerGroupAssignmentBenchmarks.*', selection['filters'])
        self.assertIn('*.Unit.ControlPlaneProtocolBenchmarks.*', selection['filters'])

    def test_outbox_save_coverage_does_not_claim_relay_or_store_workloads(self):
        for name in ('OutboxCommitObserver.cs', 'OutboxNotificationOptionsExtensions.cs'):
            selection = gate.select([f'src/Dekaf.Outbox.EntityFrameworkCore/{name}'])
            self.assertEqual(gate.unit('OutboxSaveChangesBenchmarks'), selection['filters'])
        for path in ('src/Dekaf.Outbox/OutboxRelayService.cs',
                     'src/Dekaf.Outbox.EntityFrameworkCore/EfCoreOutboxStore.cs'):
            self.assertNotIn('*.Unit.OutboxSaveChangesBenchmarks.*', gate.select([path])['filters'])

    def test_share_fetch_response_selects_its_actual_decoder(self):
        selection = gate.select(['src/Dekaf/Protocol/Messages/ShareFetchResponse.cs'])
        self.assertEqual(gate.unit('ShareFetchResponseDecodingBenchmarks'), selection['filters'])
        self.assertIn('*.Unit.PipelinedResponseAllocationBenchmarks.*',
                      gate.select(['src/Dekaf/Networking/KafkaConnection.cs'])['filters'])
        self.assertIn('*.Unit.Crc32CBenchmarks.*',
                      gate.select(['src/Dekaf/Protocol/UnknownResponse.cs'])['filters'])

    def test_binary_key_paths_retain_distinct_and_collision_workloads(self):
        for path in ('PartitionMessageKey.cs', 'KeyOrderedPartitionDispatcher.cs'):
            selection = gate.select([f'src/Dekaf/Consumer/{path}'])
            for fixture in gate.unit('BinaryKeyDispatchBenchmarks', 'DistinctBinaryKeyDispatchBenchmarks',
                                     'SharedSuffixBinaryKeyDispatchBenchmarks'):
                self.assertIn(fixture, selection['filters'])
        self.assertIn('*.Unit.KeyOrderedDispatchBenchmarks.*', selection['filters'])

    def test_added_record_bound_retains_reads_but_does_not_select_unchanged_writes(self):
        addition = ('+++ b/src/Dekaf/Protocol/Records/RecordBatch.cs\n'
                    '+    // Bound encoded records.\n+    internal int RecordCountUpperBound\n'
                    '+    {\n+        get => _recordCount;\n+    }\n')
        path = 'src/Dekaf/Protocol/Records/RecordBatch.cs'
        with mock.patch.object(gate.subprocess, 'check_output', return_value=addition):
            selection = gate.select([path], 'base', 'head')
        self.assertIn('*.Unit.ProtocolBenchmarks.Read*RecordBatch*', selection['filters'])
        self.assertIn('*.Unit.ParsedRecordSlabLifecycleBenchmarks.*', selection['filters'])
        self.assertNotIn('*.Unit.ProtocolBenchmarks.*RecordBatch*', selection['filters'])
        for diff in (addition + '-    WriteOld();\n+    WriteNew();\n',
                     addition + '+    private int _newField;\n',
                     addition.replace('get => _recordCount;', 'get;'),
                     addition.replace('internal int', 'internal static int')):
            with self.subTest(diff=diff), mock.patch.object(gate.subprocess, 'check_output', return_value=diff):
                selection = gate.select([path], 'base', 'head')
            self.assertIn('*.Unit.ProtocolBenchmarks.*RecordBatch*', selection['filters'])

    def test_pending_fetch_only_changes_keep_actual_lifecycle_coverage(self):
        source = ('using System;\ninternal sealed class PendingFetchData : IDisposable\n{\n'
                  '    private int _count = 1;\n}\n'
                  'public sealed class KafkaConsumer\n{\n    public void StoreOffset() { }\n}\n')
        changed = source.replace('_count = 1', '_count = 2')
        with mock.patch.object(gate.subprocess, 'check_output', side_effect=[source, changed]):
            selection = gate.select(['src/Dekaf/Consumer/KafkaConsumer.cs'], 'base', 'head')
        self.assertEqual(gate.unit('ConsumerHotPathBenchmarks', 'ParsedRecordSlabLifecycleBenchmarks'),
                         selection['filters'])
        self.assertEqual(1, len(selection['scoped_files']))

        # Any change outside the known class, including a using, offset-store method,
        # new top-level type or an unfamiliar declaration, retains the broad fallback.
        for candidate in (changed.replace('StoreOffset()', 'StoreOffsets()'),
                          changed.replace('using System;', 'using System.Text;'),
                          changed + 'internal sealed class Other { }\n',
                          changed.replace('internal sealed class PendingFetchData',
                                          'internal partial class PendingFetchData')):
            with self.subTest(candidate=candidate), mock.patch.object(
                    gate.subprocess, 'check_output', side_effect=[source, candidate]):
                selection = gate.select(['src/Dekaf/Consumer/KafkaConsumer.cs'], 'base', 'head')
            self.assertIn('*.Unit.OffsetStoreBenchmarks.*', selection['filters'])
            self.assertIn('*.Unit.ConsumeResultOffsetStoreBenchmarks.*', selection['filters'])
            self.assertIn('*.Unit.FetchRequestBuildBenchmarks.*', selection['filters'])
            self.assertEqual([], selection['scoped_files'])

    def test_kafka_consumer_retains_all_offset_store_api_coverage(self):
        selection = gate.select(['src/Dekaf/Consumer/KafkaConsumer.cs'])
        self.assertEqual(gate.unit('ConsumerHotPathBenchmarks', 'FetchResponseParsingBenchmarks',
                                   'FetchRequestBuildBenchmarks', 'OffsetStoreBenchmarks',
                                   'ConsumeResultOffsetStoreBenchmarks'), selection['filters'])
        self.assertNotIn('*.Unit.PartitionedDispatchBenchmarks.*', selection['filters'])

    def test_partition_completion_retains_dispatch_and_tracking_coverage(self):
        selection = gate.select(['src/Dekaf/Consumer/PartitionedProcessing.cs',
                                 'src/Dekaf/Consumer/CompletedOffsetRanges.cs'])
        self.assertEqual(gate.unit('PartitionedDispatchBenchmarks', 'KeyOrderedDispatchBenchmarks',
                                   'PartitionedOffsetTrackingBenchmarks'), selection['filters'])

    def test_record_batch_selects_real_read_write_and_lazy_lifecycle(self):
        selection = gate.select(['src/Dekaf/Protocol/Records/RecordBatch.cs'])
        self.assertIn('*.Unit.ProtocolBenchmarks.*RecordBatch*', selection['filters'])
        self.assertIn('*.Unit.ParsedRecordSlabLifecycleBenchmarks.*', selection['filters'])
        self.assertNotIn('*.Unit.Crc32CBenchmarks.*', selection['filters'])
        fallback = gate.select(['src/Dekaf/Protocol/UnknownReader.cs'])
        self.assertIn('*.Unit.Crc32CBenchmarks.*', fallback['filters'])
        self.assertIn('*.Unit.KeyOrderedDispatchBenchmarks.*',
                      gate.select(['src/Dekaf/Consumer/UnknownConsumer.cs'])['filters'])

    def test_only_friend_assembly_edits_can_skip_the_build_input_screen(self):
        original = Path.cwd()
        with tempfile.TemporaryDirectory() as directory:
            try:
                os.chdir(directory)
                def git(*args):
                    return subprocess.check_output(['git', *args], stderr=subprocess.DEVNULL).decode().strip()
                git('init', '-q')
                git('config', 'user.email', 'gate@example.test')
                git('config', 'user.name', 'Gate test')
                project = Path('src/Dekaf/Dekaf.csproj')
                project.parent.mkdir(parents=True)
                before = '<Project>\n<ItemGroup>\n<InternalsVisibleTo Include="Dekaf.Tests.Unit" />\n</ItemGroup>\n</Project>\n'
                project.write_text(before)
                git('add', '.')
                git('commit', '-qm', 'baseline')
                baseline = git('rev-parse', 'HEAD')
                for addition, expected in (
                    ('<InternalsVisibleTo Include="Dekaf.Tests.Aot" />', []),
                    ('<PackageReference Include="Another.Package" />', [project.as_posix()]),
                    ('<InternalsVisibleTo Include="Dekaf.Tests.Aot" />\n<Optimize>false</Optimize>', [project.as_posix()]),
                    ('<InternalsVisibleTo Include="Unknown.Assembly" />', [project.as_posix()]),
                ):
                    with self.subTest(addition=addition):
                        project.write_text(before.replace('</ItemGroup>', addition + '\n</ItemGroup>'))
                        git('add', '.')
                        git('commit', '-qm', 'change')
                        self.assertEqual(expected, gate.changed_files(baseline, 'HEAD'))
            finally:
                os.chdir(original)

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
                code = '\n'.join(line for line in text.splitlines() if not line.lstrip().startswith('//'))
                self.assertIsNone(SINGLE_INVOCATION.search(code),
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

    def test_discovery_count_derives_budget_from_both_batch_limits(self):
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / 'dry.log'
            # Changing either limit must affect discovery without another workflow edit.
            for per_batch, batches in ((3, 2), (4, 2), (4, 3)):
                with self.subTest(per_batch=per_batch, batches=batches), \
                        mock.patch.object(gate, 'MAX_CASES', per_batch), \
                        mock.patch.object(gate, 'MAX_BATCHES', batches):
                    for count in (per_batch + 1, per_batch * batches, per_batch * batches + 1):
                        log.write_text(f'// ***** Found {count} benchmark(s) in total *****\n')
                        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                            self.assertEqual(int(count > per_batch * batches),
                                             gate.main(['count', '--execution-log', str(log), '--discovery']))
                            self.assertEqual(1, gate.main(['count', '--execution-log', str(log)]))

    def test_count_command_rejects_empty_and_oversized_selections(self):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            with mock.patch('sys.stdin', io.StringIO('')):
                self.assertEqual(1, gate.main(['count']))
            listing = '\n'.join(f'Dekaf.Benchmarks.Benchmarks.Unit.A.M{i}' for i in range(gate.MAX_CASES + 1))
            with mock.patch('sys.stdin', io.StringIO(listing)):
                self.assertEqual(1, gate.main(['count']))
            with mock.patch('sys.stdin', io.StringIO(listing)):
                self.assertEqual(0, gate.main(['count', '--max-cases', str(gate.MAX_CASES + 1)]))


class PlanningTests(unittest.TestCase):
    def test_every_requested_glob_must_match_on_each_revision(self):
        listing = 'Dekaf.Benchmarks.Benchmarks.Unit.A.Read\n'
        gate.check_listing(listing, ['*.Unit.A.*'], 'A')
        with self.assertRaisesRegex(ValueError, r'A:.*Missing'):
            gate.check_listing(listing, ['*.Unit.A.*', '*.Unit.Missing.*'], 'A')
        with self.assertRaisesRegex(ValueError, r'B:.*compatible fixtures'):
            gate.check_listing('', ['*.Unit.A.*'], 'B')

    def test_listing_uses_bdn_globs_not_shell_character_classes(self):
        listing = 'Dekaf.Benchmarks.Benchmarks.Unit.A.Read[Int32]\n'
        gate.check_listing(listing, ['*.unit.a.read[Int32]'], 'A')
        with self.assertRaises(ValueError):
            gate.check_listing(listing, ['*.Unit.A.Read[I]*'], 'A')

    def test_expanded_179_case_selection_is_partitioned_without_loss_or_overlap(self):
        cases = [benchmark('Read', f'Size={size}') for size in range(179)]
        plan = gate.plan_batches(cases, list(reversed(cases)))
        self.assertEqual(179, plan['case_count'])
        self.assertEqual([48, 48, 48, 35], [len(batch['cases']) for batch in plan['batches']])
        self.assertEqual(sorted(gate._case_key(item) for item in cases),
                         [key for batch in plan['batches'] for key in batch['cases']])
        self.assertEqual({item['FullName'] for item in cases},
                         {name for batch in plan['batches'] for name in batch['filters']})
        self.assertEqual(plan, gate.plan_batches(list(reversed(cases)), cases))

    def test_plan_rejects_missing_or_different_cases_even_when_counts_match(self):
        for baseline, candidate in (([], []), ([benchmark()], [benchmark('Other')]),
                                     ([benchmark(parameters='Size=1')], [benchmark(parameters='Size=2')]),
                                     ([benchmark()], [benchmark(), benchmark()])):
            with self.subTest(baseline=baseline, candidate=candidate), self.assertRaises(ValueError):
                gate.plan_batches(baseline, candidate)

    def test_filter_identity_must_be_literal_and_unambiguous(self):
        for name in (None, '', 'SomethingElse.Read', 'Dekaf.Benchmarks.A.Read(X: *)',
                     'Dekaf.Benchmarks.A.Read(X: ?)', 'Dekaf.Benchmarks.A.Read\n--job Dry'):
            item = benchmark() | {'FullName': name}
            with self.subTest(name=name), self.assertRaises(ValueError):
                gate.plan_batches([item], [item])
        cases = [benchmark('Read'), benchmark('read')]
        with self.assertRaisesRegex(ValueError, 'ambiguous'):
            gate.plan_batches(cases, cases)
        with self.assertRaisesRegex(ValueError, 'FullName'):
            gate.plan_batches([benchmark()], [benchmark() | {'FullName': 'Dekaf.Benchmarks.Other.Read'}])

    def test_total_work_and_batch_size_remain_bounded(self):
        cases = [benchmark(f'Method{i}') for i in range(gate.MAX_CASES * gate.MAX_BATCHES + 1)]
        with self.assertRaisesRegex(ValueError, 'narrower filters'):
            gate.plan_batches(cases, cases)
        for size in (0, -1, gate.MAX_CASES + 1):
            with self.subTest(size=size), self.assertRaises(ValueError):
                gate.plan_batches([benchmark()], [benchmark()], size)

    def test_changed_full_name_diagnostic_identifies_both_filters(self):
        baseline = benchmark()
        candidate = baseline | {'FullName': baseline['FullName'].replace('.Append', '.append')}
        with self.assertRaises(ValueError) as failure:
            gate.plan_batches([baseline], [candidate])
        diagnostic = str(failure.exception)
        self.assertIn(gate._case_key(baseline), diagnostic)
        self.assertIn(f'A={baseline["FullName"]!r}', diagnostic)
        self.assertIn(f'B={candidate["FullName"]!r}', diagnostic)

    def test_summary_requires_every_planned_case_and_preserves_verdicts(self):
        plan = gate.plan_batches([benchmark(), benchmark('Read')], [benchmark(), benchmark('Read')], 1)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for batch in plan['batches']:
                target = root / f'batch-{batch["id"]}' / 'comparison.json'
                target.parent.mkdir()
                target.write_text(json.dumps({'screen': 'PASS', 'baseline_only': [], 'candidate_only': [],
                                              'cases': [{'case': key} for key in batch['cases']]}))
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual('PASS', gate.summarize_batches(plan, root))
                target = root / 'batch-2' / 'comparison.json'
                original = json.loads(target.read_text())
                for screen in ('INCONCLUSIVE', 'REGRESSION'):
                    target.write_text(json.dumps(original | {'screen': screen}))
                    self.assertEqual(screen, gate.summarize_batches(plan, root))
                for changes in ({'cases': []}, {'cases': [{'case': 'unexpected'}]},
                                {'cases': original['cases'] * 2}, {'candidate_only': ['new']},
                                {'baseline_only': ['old']}, {'screen': 'UNKNOWN'}):
                    target.write_text(json.dumps(original | changes))
                    with self.subTest(changes=changes), self.assertRaises(ValueError):
                        gate.summarize_batches(plan, root)
                target.unlink()
                with self.assertRaises(FileNotFoundError):
                    gate.summarize_batches(plan, root)

    def test_plan_command_uses_original_exports(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for role in ('A', 'B'):
                (root / role).mkdir()
                (root / role / 'original-report-full.json').write_text(json.dumps(
                    {'Benchmarks': [benchmark('Read', 'Size=128'), benchmark('Read', 'Size=512')]}))
            output = root / 'plan.json'
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, gate.main(['plan', '--baseline', str(root / 'A'),
                                               '--candidate', str(root / 'B'), '--output', str(output),
                                               '--max-cases', '1']))
            plan = json.loads(output.read_text())
            self.assertEqual(2, len(plan['batches']))
            self.assertEqual('Dekaf.Benchmarks.Benchmarks.Unit.AppendBenchmarks.Read(Size: 128)',
                             plan['batches'][0]['filters'][0])


class ValidationTests(unittest.TestCase):
    def test_exact_case_validation_catches_same_count_substitutions(self):
        expected = [gate._case_key(benchmark('Read', 'Size=128'))]
        self.assertEqual([], gate.check_case_set([benchmark('Read', 'Size=128')], expected))
        self.assertTrue(gate.check_case_set([benchmark('Read', 'Size=512')], expected))
        self.assertTrue(gate.check_case_set([benchmark('Other', 'Size=128')], expected))
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'case-report-full.json').write_text(json.dumps({'Benchmarks': [benchmark('Other')]}))
            (root / 'expected.json').write_text(json.dumps(expected))
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, gate.main(['validate', '--phase', str(root), '--expected', '1',
                                               '--expected-cases', str(root / 'expected.json')]))

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
        measurement_job = workflow.split('\n  measure:\n', 1)[1].split('\n  gate:\n', 1)[0]
        self.assertIn('pull_request:', workflow)
        self.assertIn('runs-on: ubuntu-latest', workflow)
        self.assertIn('for phase in A1 B A2', measurement_job)
        self.assertIn('runs-on: ubuntu-latest', measurement_job)
        self.assertIn('fail-fast: false', measurement_job)
        self.assertIn('max-parallel: 2', measurement_job)
        self.assertIn("if: needs.plan.result == 'success' && needs.plan.outputs.applicable == 'true' "
                      "&& needs.plan.outputs.matrix != ''", measurement_job)
        self.assertIn('--execution-log "$GATE/dry-$role.log" --discovery', workflow)
        self.assertNotIn('--max-cases 192', workflow)
        self.assertIn('--expected-cases "$GATE/expected-cases.json"', measurement_job)
        self.assertIn('needs: [plan, measure]', workflow)
        self.assertIn('defaults:\n  run:\n    shell: bash', workflow)
        self.assertIn('performance_gate.py summarize --plan plan/plan.json', workflow)
        self.assertIn('"$MEASURE_RESULT" != success', workflow)
        for setting in ('--warmupCount 50', '--iterationCount 25', '--iterationTime 1000', '--launchCount 1',
                        '--outliers DontRemove', '--exporters fulljson'):
            self.assertIn(setting, measurement_job, setting)
        self.assertIn('performance_gate.py select', workflow)
        self.assertIn('performance_gate.py validate', workflow)
        self.assertIn('--argjson alloc_floor 8', workflow)
        self.assertIn('--argjson min_warmup 20', workflow)
        self.assertIn('compare_bdn_reports.jq', workflow)
        self.assertIn('dotnet build-server shutdown', workflow)
        self.assertNotIn('runs-on: ubicloud', workflow)


if __name__ == '__main__':
    unittest.main()
