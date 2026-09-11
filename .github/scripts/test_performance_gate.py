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
WORKFLOW = REPO_ROOT / '.github' / 'workflows' / 'performance-gate.yml'
BASH = shutil.which('bash')
if os.name == 'nt':
    # Windows' system32/bash.exe launches WSL, not a shell for Windows paths.
    git = shutil.which('git')
    git_bash = Path(git).parent.parent / 'bin/bash.exe' if git else None
    BASH = str(git_bash) if git_bash and git_bash.is_file() else None


def benchmark(method='Append', parameters='', median=100.0, samples=15, allocated=0, type_name='AppendBenchmarks'):
    return {'Namespace': 'Dekaf.Benchmarks.Benchmarks.Unit', 'Type': type_name, 'Method': method,
            'FullName': f'Dekaf.Benchmarks.Benchmarks.Unit.{type_name}.{method}'
                        + (f'({parameters.replace("=", ": ")})' if parameters else ''),
            'Parameters': parameters, 'Statistics': {'Median': median, 'Mean': median, 'N': samples},
            'Metrics': [] if allocated is None else [{'Descriptor': {'Id': 'Allocated Memory'}, 'Value': allocated}]}


def phases(a1=100.0, b=100.0, a2=100.0, alloc=(0, 0, 0), **kwargs):
    return ([benchmark(median=a1, allocated=alloc[0], **kwargs)], [benchmark(median=b, allocated=alloc[1], **kwargs)],
            [benchmark(median=a2, allocated=alloc[2], **kwargs)])


class SelectionTests(unittest.TestCase):
    def test_config_mutations_keep_direct_fixture_coverage(self):
        for path in ('src/Dekaf/Admin/AdminClient.cs', 'src/Dekaf/Admin/AdminClient.DetailedConfigMutations.cs',
                     'src/Dekaf/Admin/AdminClient.DetailedMutations.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('AdminDetailedConfigBenchmarks', selected)

    fixtures = {'AccumulatorAppendBenchmarks': {'file': 'a.cs', 'steady_state': True},
                'ConsumerHotPathBenchmarks': {'file': 'b.cs', 'steady_state': True},
                'FetchResponseParsingBenchmarks': {'file': 'c.cs', 'steady_state': True},
                'ProduceResponseParsingBenchmarks': {'file': 'd.cs', 'steady_state': True},
                'PartitionerBenchmarks': {'file': 'e.cs', 'steady_state': True},
                'CompressionBenchmarks': {'file': 'f.cs', 'steady_state': False}}

    def names(self, selection):
        return [item['name'] for item in selection['classes']]

    def test_partition_message_key_selects_direct_fixture(self):
        path = 'src/Dekaf/Consumer/PartitionMessageKey.cs'
        selected = self.names(gate.select([path], root=REPO_ROOT))
        self.assertIn('PartitionMessageKeyBenchmarks', selected)

    def test_key_ordered_dispatcher_selects_message_key_fixture(self):
        path = 'src/Dekaf/Consumer/KeyOrderedPartitionDispatcher.cs'
        selected = self.names(gate.select([path], root=REPO_ROOT))
        self.assertIn('PartitionMessageKeyBenchmarks', selected)

    def test_detailed_mutation_paths_select_direct_fixture(self):
        for path in ('src/Dekaf/Admin/AdminClient.cs', 'src/Dekaf/Admin/AdminClient.DetailedTopicMutations.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('AdminDetailedMutationBenchmarks', selected)

    def test_quota_mutations_select_direct_fixture(self):
        for path in ('src/Dekaf/Admin/AdminClient.DetailedClientQuotaMutations.cs', 'src/Dekaf/Admin/AdminClient.DetailedMutations.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('AdminDetailedClientQuotaBenchmarks', selected)

    def test_inmemory_quota_paths_select_direct_fixture(self):
        for path in ('src/Dekaf.Testing/InMemoryAdminClient.DetailedClientQuotaMutations.cs',
                     'src/Dekaf.Testing/InMemoryAdminClient.DetailedMutations.cs',
                     'src/Dekaf.Testing/InMemoryAdminClient.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('InMemoryDetailedClientQuotaBenchmarks', selected)

    def test_member_removal_paths_select_direct_fixture(self):
        for path in ('src/Dekaf/Admin/AdminClient.MemberRemoval.cs', 'src/Dekaf/Admin/AdminClient.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('AdminMemberRemovalBenchmarks', selected)

    def test_security_mutation_paths_select_direct_fixture(self):
        for path in ('src/Dekaf/Admin/AdminClient.DetailedSecurityMutations.cs', 'src/Dekaf/Admin/AdminClient.DetailedMutations.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('AdminDetailedSecurityBenchmarks', selected)

    def test_consumer_group_mutation_paths_select_direct_fixture(self):
        for path in ('src/Dekaf/Admin/AdminClient.DetailedConsumerGroupMutations.cs', 'src/Dekaf/Admin/AdminClient.DetailedMutations.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('AdminDetailedConsumerGroupMutationBenchmarks', selected)

    def test_member_registration_paths_select_direct_fixture(self):
        for path in ('src/Dekaf.Testing/InMemoryKafkaCluster.cs', 'src/Dekaf.Testing/InMemoryKafkaCluster.MemberRemoval.cs', 'src/Dekaf.Testing/InMemoryConsumer.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('InMemoryMemberRegistrationBenchmarks', selected)

    def test_detailed_share_mutations_select_coordinator_fixture(self):
        for path in ('src/Dekaf/Admin/AdminClient.DetailedMutations.cs', 'src/Dekaf/Admin/AdminClient.DetailedShareGroupOffsets.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('AdminDetailedShareGroupOffsetBenchmarks', selected)
    def test_share_consumer_selects_polling_and_batch_fixtures(self):
        path = 'src/Dekaf/ShareConsumer/KafkaShareConsumer.cs'
        selected = self.names(gate.select([path], root=REPO_ROOT))
        for fixture in ('ShareConsumerPollBenchmarks', 'ShareConsumerPollBufferBenchmarks', 'ShareConsumerUnsubscribeBenchmarks', 'ShareConsumerSparsePollBenchmarks', 'ShareConsumerBorrowedParsingBenchmarks',
                        'ShareBatchAcknowledgementBenchmarks', 'ShareBatchScalingBenchmarks',
                        'ShareBatchRenewalPollBenchmarks', 'ShareBatchPendingStateBenchmarks',
                        'ShareBatchChunkedRenewalBenchmarks'):
            self.assertIn(fixture, selected)

    def test_acknowledged_offsets_selects_indexing_fixture(self):
        path = 'src/Dekaf/ShareConsumer/ShareAcknowledgedOffsets.cs'
        selected = self.names(gate.select([path], root=REPO_ROOT))
        self.assertIn('ShareAcknowledgedOffsetsBenchmarks', selected)

    def test_share_owner_paths_select_multi_batch_fixture(self):
        for path in ('src/Dekaf/ShareConsumer/ShareRecordBatchOwner.cs', 'src/Dekaf/ShareConsumer/KafkaShareConsumer.cs'):
            with self.subTest(path=path):
                selected = self.names(gate.select([path], root=REPO_ROOT))
                self.assertIn('ShareConsumerMultiBatchBenchmarks', selected)

    def test_any_product_change_runs_the_sentinels(self):
        selection = gate.select(['src/Dekaf/Admin/AdminClient.cs'], fixtures=self.fixtures)
        self.assertEqual(sorted(gate.SENTINELS), self.names(selection))
        self.assertEqual(['*.Unit.AccumulatorAppendBenchmarks.*'], [selection['classes'][0]['filter']])

    def test_component_directories_add_their_classes(self):
        selection = gate.select(['src\\Dekaf\\Producer\\RecordAccumulator.cs'], fixtures=self.fixtures)
        self.assertIn('PartitionerBenchmarks', self.names(selection))
        self.assertIn('AccumulatorAppendBenchmarks', self.names(selection))

    def test_non_product_files_select_nothing(self):
        selection = gate.select(['docs/guide.md', 'tests/Dekaf.Tests.Unit/X.cs', 'src/Dekaf/README.md'], fixtures=self.fixtures)
        self.assertFalse(selection['applicable'])
        self.assertEqual([], selection['classes'])

    def test_touched_fixture_classes_run_even_without_product_changes(self):
        with tempfile.TemporaryDirectory() as root:
            unit = Path(root) / gate.UNIT_FIXTURES
            unit.mkdir(parents=True)
            (unit / 'NewBenchmarks.cs').write_text(
                '[MemoryDiagnoser]\npublic class NewBenchmarks\n{\n}\n\npublic sealed class OtherBenchmark { }\n', encoding='utf-8')
            (unit / 'ColdBenchmarks.cs').write_text(
                'public class ColdBenchmarks\n{\n    [IterationSetup]\n    public void Setup() { }\n}\n', encoding='utf-8')
            selection = gate.select([gate.UNIT_FIXTURES + 'NewBenchmarks.cs', gate.UNIT_FIXTURES + 'ColdBenchmarks.cs',
                                     gate.UNIT_FIXTURES + 'Deleted.cs'], root=root)
        self.assertEqual(['NewBenchmarks', 'OtherBenchmark'], self.names(selection))
        self.assertEqual(['ColdBenchmarks'], [item['name'] for item in selection['skipped']])
        self.assertIn('not steady-state', selection['skipped'][0]['reason'])

    def test_steady_state_is_judged_per_class_and_ignores_comments(self):
        source = ('// No [IterationSetup]: each benchmark clears its buffer itself.\n'
                  '/* InvocationCount would be wrong here */\n'
                  '[MemoryDiagnoser]\npublic class SteadyBenchmarks\n{\n    [Benchmark] public void Run() { }\n}\n\n'
                  '[Config(typeof(Cold))]\npublic class ColdBenchmarks\n{\n    [IterationSetup] public void Setup() { }\n'
                  '    private sealed class Cold : ManualConfig { }\n}\n\n'
                  'public sealed class AlsoSteadyBenchmarks\n{\n}\n')
        self.assertEqual({'SteadyBenchmarks': True, 'ColdBenchmarks': False, 'AlsoSteadyBenchmarks': True},
                         gate.classes_in(source))
        decorated = ('[MemoryDiagnoser]\npublic class FirstBenchmarks\n{\n}\n\n'
                     '[SimpleJob(RunStrategy.Monitoring)] // sampled\n[MemoryDiagnoser]\n'
                     'public sealed class SecondBenchmarks\n{\n}\n')
        self.assertEqual({'FirstBenchmarks': True, 'SecondBenchmarks': False}, gate.classes_in(decorated))

    def test_real_fixture_files_with_mixed_classes_keep_their_steady_siblings(self):
        fixtures = gate.fixture_classes(REPO_ROOT)
        self.assertTrue(fixtures['CompressionBenchmarks']['steady_state'])
        self.assertTrue(fixtures['SchemaRegistryRuleExecutorBenchmarks']['steady_state'])
        self.assertFalse(fixtures['SchemaRegistryCelFreshContextBenchmarks']['steady_state'])

    def test_unknown_and_non_steady_state_classes_are_skipped_with_a_reason(self):
        selection = gate.select(['src/Dekaf/Foo.cs'], fixtures={'CompressionBenchmarks': {'file': 'f.cs', 'steady_state': False}})
        self.assertFalse(selection['applicable'])
        self.assertEqual(sorted(gate.SENTINELS), sorted(item['name'] for item in selection['skipped']))

    def test_every_mapped_class_exists_and_is_steady_state(self):
        fixtures = gate.fixture_classes(REPO_ROOT)
        mapped = set(gate.SENTINELS).union(*gate.COMPONENTS.values())
        for name in sorted(mapped):
            with self.subTest(name=name):
                self.assertIn(name, fixtures)
                self.assertTrue(fixtures[name]['steady_state'])
                source = (REPO_ROOT / gate.UNIT_FIXTURES / fixtures[name]['file']).read_text(encoding='utf-8-sig')
                self.assertIn('MemoryDiagnoser', source)

    def test_explicit_filters_replace_path_selection_with_unique_names(self):
        selection = gate.explicit_selection(['*.Unit.Crc32CBenchmarks.*', '*Share*', '*.Unit.Crc32CBenchmarks'])
        self.assertEqual(['Unit-Crc32CBenchmarks', 'Share', 'Unit-Crc32CBenchmarks-3'], self.names(selection))
        self.assertEqual('*Share*', selection['classes'][1]['filter'])

    def test_changed_fixture_types_come_from_both_revisions_or_everything_when_infrastructure_changed(self):
        with mock.patch.object(gate.subprocess, 'check_output', side_effect=[
                'src/Dekaf/X.cs\ntools/Dekaf.Benchmarks/Benchmarks/Unit/A.cs\n',
                'public class OldBenchmarks { }\n', 'public class NewBenchmarks { }\n']):
            self.assertEqual({'OldBenchmarks', 'NewBenchmarks'}, gate.changed_fixture_types('base', 'head'))
        with mock.patch.object(gate.subprocess, 'check_output', return_value='tools/Dekaf.Benchmarks/Infrastructure/H.cs\n'):
            self.assertIsNone(gate.changed_fixture_types('base', 'head'))
        with mock.patch.object(gate.subprocess, 'check_output', return_value='src/Dekaf/X.cs\n'):
            self.assertEqual(set(), gate.changed_fixture_types('base', 'head'))


class ScreenTests(unittest.TestCase):
    def screen(self, *args, **kwargs):
        return gate.screen(*phases(*args, **kwargs))['cases'][0]

    def test_tolerance_widens_for_small_operations(self):
        self.assertEqual(20, gate.tolerance_percent(50))
        self.assertEqual(15, gate.tolerance_percent(100))
        self.assertEqual(15, gate.tolerance_percent(999))
        self.assertEqual(10, gate.tolerance_percent(1000))
        self.assertEqual(10, gate.tolerance_percent(5e7))

    def test_identical_phases_pass(self):
        case = self.screen()
        self.assertEqual('PASS', case['screen'])
        self.assertEqual([], case['notes'])

    def test_slower_than_both_controls_beyond_tolerance_regresses(self):
        self.assertEqual('REGRESSION', self.screen(a1=100, b=116, a2=100)['screen'])
        self.assertEqual('PASS', self.screen(a1=100, b=114, a2=100)['screen'])
        self.assertEqual('REGRESSION', self.screen(a1=50, b=61, a2=50)['screen'])
        self.assertEqual('PASS', self.screen(a1=50, b=59, a2=50)['screen'])
        self.assertEqual('REGRESSION', self.screen(a1=5000, b=5501, a2=5000)['screen'])

    def test_slower_than_one_control_only_is_a_note_not_a_verdict(self):
        case = self.screen(a1=100, b=120, a2=118)
        self.assertEqual('PASS', case['screen'])
        self.assertIn('slower than one control only; not a demonstrated loss', case['notes'])
        self.assertTrue(any(note.startswith('controls drift') for note in case['notes']))

    def test_faster_than_both_controls_is_an_improvement(self):
        self.assertEqual('IMPROVEMENT', self.screen(a1=100, b=80, a2=100)['screen'])

    def test_allocation_regression_needs_one_object_and_one_percent(self):
        self.assertEqual('REGRESSION', self.screen(alloc=(0, 24, 0))['screen'])
        self.assertEqual('PASS', self.screen(alloc=(0, 23, 0))['screen'])
        self.assertEqual('REGRESSION', self.screen(alloc=(59, 185, 59))['screen'])
        amortized = self.screen(alloc=(12528, 12568, 12528))
        self.assertEqual('PASS', amortized['screen'])
        self.assertIn('amortized', amortized['notes'][0])
        self.assertEqual('PASS', self.screen(alloc=(2064, 2064, 2100))['screen'])

    def test_invalid_measurements_are_errors_not_verdicts(self):
        good = benchmark()
        with self.assertRaisesRegex(ValueError, 'measured 3 iterations'):
            gate.screen([good], [benchmark(samples=3)], [good])
        with self.assertRaisesRegex(ValueError, 'Allocated Memory'):
            gate.screen([good], [benchmark(allocated=None)], [good])
        with self.assertRaisesRegex(ValueError, 'different case sets'):
            gate.screen([good], [good], [benchmark(method='Other')])
        with self.assertRaisesRegex(ValueError, 'no benchmark cases'):
            gate.screen([], [good], [good])

    def test_candidate_only_and_baseline_only_cases_are_listed_not_compared(self):
        extra = benchmark(method='New', median=5, allocated=0)
        result = gate.screen([benchmark()], [benchmark(), extra], [benchmark()])
        self.assertEqual('PASS', result['screen'])
        self.assertEqual(['Dekaf.Benchmarks.Benchmarks.Unit AppendBenchmarks New'], [item['case'] for item in result['candidate_only']])
        result = gate.screen([benchmark(), extra], [benchmark()], [benchmark(), extra])
        self.assertEqual(1, len(result['baseline_only']))
        self.assertIn('Candidate-only', gate.markdown(gate.screen([benchmark()], [benchmark(), extra], [benchmark()])))

    def test_cases_from_changed_fixture_files_are_reported_not_compared(self):
        a1, b, a2 = phases(a1=100, b=200, a2=100)
        result = gate.screen(a1, b, a2, uncomparable_types={'AppendBenchmarks'})
        self.assertEqual(('NOT COMPARED', [], 1), (result['screen'], result['cases'], len(result['fixture_changed'])))
        self.assertIn('| NOT COMPARED | fixture source differs', gate.markdown(result))
        self.assertEqual('NOT COMPARED', gate.screen(a1, b, a2, uncomparable_types=None)['screen'])
        self.assertEqual('REGRESSION', gate.screen(a1, b, a2, uncomparable_types={'OtherBenchmarks'})['screen'])
        mixed = gate.screen(a1 + [benchmark(type_name='FreshBenchmarks')], b + [benchmark(type_name='FreshBenchmarks')],
                            a2 + [benchmark(type_name='FreshBenchmarks')], uncomparable_types={'AppendBenchmarks'})
        self.assertEqual(('PASS', 1, 1), (mixed['screen'], len(mixed['cases']), len(mixed['fixture_changed'])))

    def test_class_the_pr_adds_is_measured_on_the_candidate_alone_and_not_compared(self):
        fresh = [benchmark(type_name='FreshBenchmarks', median=80, allocated=32)]
        for changed in ({'FreshBenchmarks'}, None):
            result = gate.screen([], fresh, [], uncomparable_types=changed, baseline_lacks_filter=True)
            self.assertEqual(('NOT COMPARED', [], 1, [], True),
                             (result['screen'], result['cases'], len(result['candidate_only']), result['baseline_only'],
                              result['baseline_lacks_filter']))
        self.assertIn('the baseline has no benchmark for this filter', gate.markdown(result))
        with self.assertRaisesRegex(ValueError, 'B: no benchmark cases'):
            gate.screen([], [], [], uncomparable_types={'FreshBenchmarks'}, baseline_lacks_filter=True)
        with self.assertRaisesRegex(ValueError, 'yet A1/A2 exported cases'):
            gate.screen(fresh, fresh, fresh, uncomparable_types={'FreshBenchmarks'}, baseline_lacks_filter=True)
        with self.assertRaisesRegex(ValueError, r"fixture files of \['FreshBenchmarks'\] are unchanged"):
            gate.screen([], fresh, [], uncomparable_types={'OtherBenchmarks'}, baseline_lacks_filter=True)
        with self.assertRaisesRegex(ValueError, 'A1: no benchmark cases'):
            gate.screen([], fresh, [], uncomparable_types={'FreshBenchmarks'})

    def test_repeat_must_reproduce_a_regression(self):
        first = gate.screen(*phases(a1=100, b=130, a2=100))
        self.assertEqual('REGRESSION', first['screen'])
        self.assertEqual(['Dekaf.Benchmarks.Benchmarks.Unit.AppendBenchmarks.Append'], gate.regressed_filters(first))
        confirmed = gate.confirm(first, gate.screen(*phases(a1=100, b=125, a2=102)))
        self.assertEqual('REGRESSION', confirmed['screen'])
        self.assertEqual(2, confirmed['round'])
        self.assertIn('reproduced on repeat', confirmed['cases'][0]['notes'][-1])
        cleared = gate.confirm(first, gate.screen(*phases(a1=100, b=104, a2=99)))
        self.assertEqual('PASS', cleared['screen'])
        self.assertIn('not reproduced on repeat (first +30.0%/+30.0%, repeat +4.0%/+5.1%)', cleared['cases'][0]['notes'][-1])
        self.assertEqual('REGRESSION', cleared['cases'][0]['first_round']['screen'])
        with self.assertRaisesRegex(ValueError, 'did not measure'):
            gate.confirm(first, gate.screen(*phases(method='Other')))

    def test_passing_cases_are_not_repeated(self):
        first = gate.screen([benchmark(), benchmark(method='Slow')], [benchmark(), benchmark(method='Slow', median=140)],
                            [benchmark(), benchmark(method='Slow')])
        self.assertEqual(['Dekaf.Benchmarks.Benchmarks.Unit.AppendBenchmarks.Slow'], gate.regressed_filters(first))
        final = gate.confirm(first, gate.screen([benchmark(method='Slow')], [benchmark(method='Slow', median=101)],
                                                [benchmark(method='Slow')]))
        self.assertEqual(['PASS', 'PASS'], [case['screen'] for case in final['cases']])

    def test_markdown_reports_every_case_with_medians_and_verdict(self):
        text = gate.markdown(gate.screen(*phases(a1=100, b=116, a2=100)))
        self.assertIn('| AppendBenchmarks Append | 100.0 ns | 116.0 ns | 100.0 ns | +16.0% | +16.0% | +0.0% | 15% | 0 / 0 / 0 | REGRESSION |', text)
        self.assertIn('Screen: **REGRESSION**', text)
        self.assertIn('1.20 us', gate.markdown(gate.screen(*phases(a1=1200, b=1200, a2=1200))))


class CommandTests(unittest.TestCase):
    def write_phase(self, root, name, benchmarks):
        directory = Path(root) / name / 'results'
        directory.mkdir(parents=True)
        (directory / 'X-report-full.json').write_text(json.dumps({'Benchmarks': benchmarks}), encoding='utf-8')
        return str(Path(root) / name)

    def test_screen_command_exit_codes_and_outputs(self):
        with tempfile.TemporaryDirectory() as root:
            same = self.write_phase(root, 'A1', [benchmark()])
            slow = self.write_phase(root, 'B', [benchmark(median=140)])
            again = self.write_phase(root, 'A2', [benchmark()])
            broken = self.write_phase(root, 'broken', [benchmark(samples=2)])
            out, md, regressed = (Path(root) / name for name in ('c.json', 'c.md', 'r.txt'))
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, gate.main(['screen', '--a1', same, '--b', same, '--a2', again, '--output', str(out)]))
                self.assertEqual(1, gate.main(['screen', '--a1', same, '--b', slow, '--a2', again, '--name', 'Append',
                                               '--output', str(out), '--markdown', str(md), '--regressed', str(regressed)]))
                with contextlib.redirect_stderr(io.StringIO()) as errors:
                    self.assertEqual(2, gate.main(['screen', '--a1', same, '--b', broken, '--a2', again, '--output', str(out)]))
            self.assertIn('::error::Invalid measurement', errors.getvalue())
            self.assertEqual('Dekaf.Benchmarks.Benchmarks.Unit.AppendBenchmarks.Append\n', regressed.read_text(encoding='utf-8'))
            self.assertIn('REGRESSION', md.read_text(encoding='utf-8'))
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, gate.main(['screen', '--a1', same, '--b', same, '--a2', again, '--previous', str(out),
                                               '--output', str(Path(root) / 'final.json')]))
            final = json.loads((Path(root) / 'final.json').read_text(encoding='utf-8'))
            self.assertEqual(('PASS', 2), (final['screen'], final['round']))
            with mock.patch.object(gate, 'changed_fixture_types', return_value={'AppendBenchmarks'}), \
                    contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(0, gate.main(['screen', '--a1', same, '--b', slow, '--a2', again, '--fixture-diff', 'x', 'y',
                                               '--output', str(out)]))
            self.assertIn('::warning::', output.getvalue())
            self.assertEqual('NOT COMPARED', json.loads(out.read_text(encoding='utf-8'))['screen'])
            # A class the PR adds: the workflow skips A1/A2 (their directories stay empty) and says so.
            empty = str(Path(root) / 'empty')
            with mock.patch.object(gate, 'changed_fixture_types', return_value={'AppendBenchmarks'}),                     contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(0, gate.main(['screen', '--a1', empty, '--b', slow, '--a2', empty, '--fixture-diff', 'x', 'y',
                                               '--baseline-lacks-filter', '--output', str(out), '--markdown', str(md)]))
            self.assertIn('the baseline has no benchmark for this filter', output.getvalue())
            final = json.loads(out.read_text(encoding='utf-8'))
            self.assertEqual(('NOT COMPARED', 1, True), (final['screen'], len(final['candidate_only']), final['baseline_lacks_filter']))
            self.assertIn('Candidate-only cases, not compared (the baseline has no benchmark', md.read_text(encoding='utf-8'))


class WorkflowTests(unittest.TestCase):
    @unittest.skipUnless(BASH, 'workflow execution requires bash')
    def test_initialization_finishes_before_parallel_builds(self):
        result, events, _ = self.run_build_step()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(['prepare-A', 'prepare-B'], events[:2])
        self.assertIn('build-B-background', events)
        self.assertIn('build-A-foreground', events)

    @unittest.skipUnless(BASH, 'workflow execution requires bash')
    def test_preparation_failure_stops_before_builds(self):
        for role in ('A', 'B'):
            with self.subTest(role=role):
                result, events, _ = self.run_build_step(fail_prepare=role)
                self.assertNotEqual(0, result.returncode)
                self.assertFalse(any(event.startswith('build-') for event in events), events)

    @unittest.skipUnless(BASH, 'workflow execution requires bash')
    def test_baseline_fallback_prepares_again_and_observes_candidate_failure(self):
        for fail_candidate in (False, True):
            with self.subTest(fail_candidate=fail_candidate):
                result, events, fallback = self.run_build_step(fallback=True, fail_candidate=fail_candidate)
                self.assertEqual(2, events.count('prepare-A'), events)
                self.assertEqual(2, events.count('build-A-foreground'), events)
                self.assertTrue(fallback)
                self.assertEqual(not fail_candidate, result.returncode == 0, result.stdout + result.stderr)
                if fail_candidate:
                    self.assertIn('The candidate fixtures failed to build', result.stdout)

    def run_build_step(self, *, fail_prepare='', fallback=False, fail_candidate=False):
        workflow = WORKFLOW.read_text(encoding='utf-8')
        start = re.search(r'^          (?:prepare|build)\(\) \{', workflow, re.MULTILINE).start()
        end = workflow.index('\n      - name: Measure A1', start)
        commands = textwrap.dedent(workflow[start:end])
        # Model a first-use CLI that rejects initialization in a background job.
        # BASH_SUBSHELL distinguishes the concurrent build from foreground setup
        # deterministically, without sleeps or depending on scheduler timing.
        stub = r'''
            dotnet() {
              local role=${PWD##*/gate-}
              role=${role%%/*}
              case "$1" in
                new)
                  if (( BASH_SUBSHELL > 1 )); then
                    echo 'Concurrent CLI first-use initialization' >&2
                    return 71
                  fi
                  echo "prepare-$role" >> "$EVENTS"
                  [[ "$FAIL_PREPARE" != "$role" ]] || return 61
                  touch GateBenchmarkExecution.sln
                  ;;
                sln) ;;
                build)
                  local execution=foreground
                  if (( BASH_SUBSHELL > 1 )); then execution=background; fi
                  echo "build-$role-$execution" >> "$EVENTS"
                  [[ -f GateBenchmarkExecution.sln ]] || return 62
                  if [[ "$role" == B && "$FAIL_CANDIDATE" == 1 ]]; then return 63; fi
                  if [[ "$role" == A && "$FALLBACK" == 1 && ! -f "$RUNNER_TEMP/attempted-A" ]]; then
                    touch "$RUNNER_TEMP/attempted-A"
                    return 64
                  fi
                  ;;
                run) echo Dekaf.Benchmarks.Benchmarks.Unit.ExampleBenchmarks.Example ;;
                build-server) ;;
                *) return 65 ;;
              esac
            }
            git() {
              if [[ "$1" == clean ]]; then
                rm -f tools/Dekaf.Benchmarks/GateBenchmarkExecution.sln
              fi
            }
        '''
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for role in ('A', 'B'):
                (root / f'gate-{role}/tools/Dekaf.Benchmarks').mkdir(parents=True)
            (root / 'results').mkdir()
            events = root / 'events'
            events.touch()
            script = root / 'build.sh'
            script.write_text('set -euo pipefail\n' + textwrap.dedent(stub) + commands, encoding='utf-8')
            environment = dict(os.environ, RUNNER_TEMP=root.as_posix(), GATE=(root / 'results').as_posix(),
                               GITHUB_STEP_SUMMARY=(root / 'summary').as_posix(), EVENTS=events.as_posix(),
                               FAIL_PREPARE=fail_prepare, FALLBACK=str(int(fallback)),
                               FAIL_CANDIDATE=str(int(fail_candidate)), HEAD_SHA='candidate', FILTER='*')
            result = subprocess.run([BASH, script.as_posix()], cwd=root, env=environment,
                                    text=True, capture_output=True, timeout=30)
            return result, events.read_text().splitlines(), (root / 'results/baseline-fixtures').exists()

    def test_gate_workflow_runs_one_same_vm_job_per_class_and_repeats_regressions(self):
        workflow = WORKFLOW.read_text(encoding='utf-8')
        measure = workflow.split('\n  measure:\n', 1)[1].split('\n  gate:\n', 1)[0]
        self.assertIn("types: [opened, synchronize, reopened, ready_for_review]", workflow)
        self.assertIn("- 'tools/Dekaf.Benchmarks/**'", workflow)
        self.assertNotIn('ubicloud', workflow)
        self.assertNotIn('jq ', workflow)
        self.assertIn('runs-on: ubuntu-latest', measure)
        self.assertIn('fail-fast: false', measure)
        self.assertIn('matrix: ${{ fromJSON(needs.plan.outputs.matrix) }}', measure)
        self.assertIn('cp -R "$RUNNER_TEMP/gate-B/tools/Dekaf.Benchmarks" "$RUNNER_TEMP/gate-A/tools/Dekaf.Benchmarks"', measure)
        self.assertIn('phase A1 A "$FILTER"; phase B B "$FILTER"; phase A2 A "$FILTER"', measure)
        self.assertIn('phase repeat-A1 A "${filters[@]}"; phase repeat-B B "${filters[@]}"; phase repeat-A2 A "${filters[@]}"', measure)
        self.assertIn('--previous "$GATE/comparison.json"', measure)
        self.assertIn('FIXTURE_DIFF=(--fixture-diff "$BASE_SHA" "$HEAD_SHA")', measure)
        self.assertIn('--list flat --filter "$FILTER"', measure)
        self.assertIn('touch "$GATE/baseline-lacks-filter"', measure)
        self.assertIn('FIXTURE_DIFF+=(--baseline-lacks-filter)', measure)
        self.assertIn('if ! wait "$candidate_build"; then', measure)
        self.assertIn('git merge-base --is-ancestor "$base" "$head"', workflow)
        settings = re.search(r"BDN_SETTINGS: '([^']+)'", workflow).group(1)
        for setting in ('--warmupCount 10', '--iterationCount 15', '--iterationTime 500', '--launchCount 1',
                        '--outliers DontRemove', '--exporters fulljson'):
            self.assertIn(setting, settings, setting)
        self.assertIn("ITERATIONS: '15'", workflow)
        self.assertEqual(15, gate.DEFAULT_ITERATIONS)
        self.assertIn('timeout-minutes: 75', measure)
        self.assertIn('build B &', measure)
        self.assertIn('needs: [plan, measure]', workflow)
        self.assertIn('"$MEASURE_RESULT" != success', workflow)


if __name__ == '__main__':
    unittest.main()
