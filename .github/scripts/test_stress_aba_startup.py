import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

from stress_aba import main
from test_stress_aba import result
from test_stress_warmup import result as warmup_result


class StressAbaStartupTests(unittest.TestCase):
    def run_comparison(self, fourth, condition):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            arguments = ['--baseline-sha', 'a' * 40, '--candidate-sha', 'b' * 40,
                         '--output', str(root / 'comparison.json'), '--summary', str(root / 'summary.md'),
                         '--require-startup-assessment']
            originals = {}
            for name, option in [('A', '--baseline-a'), ('B', '--candidate'), ('A2', '--baseline-a2')] + (
                    [('B2', '--candidate-b2')] if fourth else []):
                data = result(cpu=1.0 if name.startswith('B') else 0.8)
                data['throughput'].update(warmup_result()['throughput'])
                target = name == ('B2' if fourth else 'B')
                if condition == 'missing' and target:
                    del data['throughput']['warmup']
                if condition == 'invalid-cpu' and target:
                    data['throughput']['runtimeEnd']['cpuSeconds'] = -1
                if condition == 'drifting' and target:
                    for index, sample in enumerate(data['throughput']['intervalSamples']):
                        sample['runtime']['cpuSeconds'] = 10 + index * index
                phase = root / name
                phase.mkdir()
                source = phase / 'stress-test-results.json'
                source.write_text(json.dumps({'results': [data]}), encoding='utf-8')
                originals[source] = source.read_bytes()
                arguments += [option, str(phase)]
            with contextlib.redirect_stdout(io.StringIO()):
                code = main(arguments)
            comparison = json.loads((root / 'comparison.json').read_text(encoding='utf-8'))
            summary = (root / 'summary.md').read_text(encoding='utf-8')
            for source, original in originals.items():
                self.assertEqual(source.read_bytes(), original)
            return code, comparison, summary

    def test_validated_startup_assessment_keeps_the_metric_verdict(self):
        for fourth in (False, True):
            with self.subTest(fourth=fourth):
                code, comparison, summary = self.run_comparison(fourth, 'quiet')
                self.assertEqual(code, 1)
                self.assertEqual(comparison['verdict'], 'regression')
                self.assertNotIn('diagnosticMetricVerdict', comparison)
                assessment = comparison['startupAssessment']
                self.assertEqual(assessment['verdict'], 'VALIDATED')
                self.assertEqual(assessment['resultCount'], 4 if fourth else 3)
                self.assertEqual(assessment['coverageVerdict'], 'VALIDATED')
                self.assertEqual(assessment['trendVerdict'], 'VALIDATED')
                self.assertIn('Startup assessment: VALIDATED', summary)
                cpu = next(row for row in comparison['metrics'] if row['key'] == 'cpu')
                self.assertEqual(cpu['status'], 'regression')
                self.assertGreater(cpu['deltaVsBaselineAPercent'], 20)

    def test_diagnostic_deltas_survive_incomplete_startup_assessment(self):
        for fourth in (False, True):
            for condition in ('missing', 'invalid-cpu', 'drifting'):
                with self.subTest(fourth=fourth, condition=condition):
                    code, comparison, summary = self.run_comparison(fourth, condition)
                    self.assertEqual(code, 1)
                    self.assertEqual(comparison['verdict'], 'inconclusive')
                    self.assertEqual(comparison['diagnosticMetricVerdict'], 'regression')
                    assessment = comparison['startupAssessment']
                    self.assertEqual(assessment['verdict'], 'INCONCLUSIVE')
                    self.assertEqual(assessment['coverageVerdict'], 'VALIDATED' if condition == 'drifting' else 'INCONCLUSIVE')
                    if condition == 'drifting':
                        self.assertEqual(assessment['trendVerdict'], 'INCONCLUSIVE')
                        self.assertTrue(any('CPU per completed message drifted' in error for error in assessment['errors']))
                    self.assertIn('delivery latency over time', assessment['unassessedMetrics'])
                    cpu = next(row for row in comparison['metrics'] if row['key'] == 'cpu')
                    self.assertEqual(cpu['status'], 'regression')
                    self.assertIn('Startup assessment: INCONCLUSIVE', summary)
                    self.assertIn('diagnostic', summary)

    def test_workflow_compares_after_failed_warmup_without_relaxing_acceptance(self):
        workflow = (Path(__file__).resolve().parents[1] / 'workflows/stress-tests.yml').read_text(encoding='utf-8')
        step = workflow.split('- name: Compare exact baseline A-B-A', 1)[1].split('- name: Upload Results', 1)[0]
        self.assertNotIn("steps.warmup.outcome == 'success'", step)
        self.assertIn("if: always() && matrix.baseline_sha != ''", step)
        self.assertIn('--require-startup-assessment', step)
        validation = workflow.split('- name: Validate exact baseline warmup', 1)[1].split('- name: Record comparison binaries', 1)[0]
        self.assertNotIn('continue-on-error', validation)


if __name__ == '__main__':
    unittest.main()
