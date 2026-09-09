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
    def test_diagnostic_deltas_survive_incomplete_startup_assessment(self):
        for fourth in (False, True):
            for condition in ('quiet', 'missing', 'invalid-cpu'):
                with self.subTest(fourth=fourth, condition=condition), tempfile.TemporaryDirectory() as directory:
                    root = Path(directory)
                    arguments = ['--baseline-sha', 'a' * 40, '--candidate-sha', 'b' * 40,
                                 '--output', str(root / 'comparison.json'), '--summary', str(root / 'summary.md'),
                                 '--require-startup-assessment']
                    originals = {}
                    for name, option in [('A', '--baseline-a'), ('B', '--candidate'), ('A2', '--baseline-a2')] + (
                            [('B2', '--candidate-b2')] if fourth else []):
                        data = result(cpu=1.0 if name.startswith('B') else 0.8)
                        data['throughput'].update(warmup_result()['throughput'])
                        if condition == 'missing' and name == ('B2' if fourth else 'B'):
                            del data['throughput']['warmup']
                        if condition == 'invalid-cpu' and name == ('B2' if fourth else 'B'):
                            data['throughput']['runtimeEnd']['cpuSeconds'] = -1
                        phase = root / name
                        phase.mkdir()
                        source = phase / 'stress-test-results.json'
                        source.write_text(json.dumps({'results': [data]}), encoding='utf-8')
                        originals[source] = source.read_bytes()
                        arguments += [option, str(phase)]
                    with contextlib.redirect_stdout(io.StringIO()):
                        code = main(arguments)
                    comparison = json.loads((root / 'comparison.json').read_text(encoding='utf-8'))
                    self.assertEqual(code, 1)
                    self.assertEqual(comparison['verdict'], 'inconclusive')
                    self.assertEqual(comparison['diagnosticMetricVerdict'], 'regression')
                    assessment = comparison['startupAssessment']
                    self.assertEqual(assessment['resultCount'], 4 if fourth else 3)
                    self.assertEqual(assessment['coverageVerdict'], 'VALIDATED' if condition == 'quiet' else 'INCONCLUSIVE')
                    self.assertIn('delivery latency over time', assessment['unassessedMetrics'])
                    cpu = next(row for row in comparison['metrics'] if row['key'] == 'cpu')
                    self.assertEqual(cpu['status'], 'regression')
                    self.assertGreater(cpu['deltaVsBaselineAPercent'], 20)
                    summary = (root / 'summary.md').read_text(encoding='utf-8')
                    self.assertIn('Startup assessment: INCONCLUSIVE', summary)
                    self.assertIn('diagnostic', summary)
                    for source, original in originals.items():
                        self.assertEqual(source.read_bytes(), original)

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
