import copy
import json
import re
import unittest
from pathlib import Path

from stress_aba import compare
from stress_warmup import OUTBOX_COUNTERS, assess_results, validate_outbox
from test_stress_aba import result as aggregate_result
from test_stress_warmup import observation, result as runtime_result


def idle_phase(seconds, cpu=0.001, allocation=100):
    samples = []
    for second in range(seconds + 1):
        runtime = observation()
        runtime.update(cpuSeconds=10 + second * cpu, allocatedBytes=1000 + second * allocation)
        counts = dict.fromkeys(OUTBOX_COUNTERS, 0)
        counts['probes'] = second * 8
        samples.append(dict(elapsedSeconds=second, runtime=runtime, operations=counts))
    return dict(requestedSeconds=seconds, elapsedSeconds=seconds,
                runtimeStart=copy.deepcopy(samples[0]['runtime']), runtimeEnd=copy.deepcopy(samples[-1]['runtime']),
                operations=copy.deepcopy(samples[-1]['operations']), samples=samples)


def outbox_result():
    result = aggregate_result(scenario='outbox')
    result['idempotent'] = True
    result['throughput'].update(runtime_result(intervals=180)['throughput'])
    result['durationMinutes'] = 4
    count = result['throughput']['totalMessages']
    result['consumedMessages'] = count
    result.pop('deliveredMessages')
    operations = dict.fromkeys(OUTBOX_COUNTERS, 0)
    operations.update(published=count, reads=100, marks=100, probes=200)
    result['outbox'] = dict(idleWarmup=idle_phase(20), idle=idle_phase(60),
                            activeRequestedSeconds=180, activeWorkloadSeconds=180,
                            committedMessages=count, uniqueConsumedMessages=count,
                            duplicatePublications=0, activeOperations=operations)
    return result


class StressOutboxTests(unittest.TestCase):
    def test_complete_idle_and_active_evidence_passes_without_mutation(self):
        result = outbox_result()
        before = copy.deepcopy(result)
        self.assertEqual('VALIDATED', assess_results({'B': result})['verdict'])
        comparison = compare(result, result, result)
        self.assertEqual('pass', comparison['verdict'])
        self.assertEqual(2, len([item for item in comparison['metrics'] if item['key'].startswith('idle')]))
        self.assertEqual(before, result)

    def test_idle_spike_is_included_in_aggregate_regression(self):
        baseline, candidate = outbox_result(), outbox_result()
        idle = candidate['outbox']['idle']
        # One spike in the middle; first/last trend windows still have equal rates.
        for sample in idle['samples'][30:]:
            sample['runtime']['cpuSeconds'] += 0.6
        idle['runtimeEnd']['cpuSeconds'] += 0.6
        idle['cpuMillisecondsPerSecond'] = 0  # Cached values cannot hide the spike.
        self.assertEqual('VALIDATED', assess_results({'B': candidate})['verdict'])
        comparison = compare(baseline, candidate, baseline)
        metric = next(item for item in comparison['metrics'] if item['key'] == 'idleCpu')
        self.assertEqual('regression', metric['status'])
        self.assertAlmostEqual(11, metric['candidate'])

    def test_idle_allocation_uses_the_same_adverse_tolerance(self):
        baseline, candidate = outbox_result(), outbox_result()
        candidate['outbox']['idle'] = idle_phase(60, allocation=104)
        comparison = compare(baseline, candidate, baseline)
        self.assertEqual('regression', next(item for item in comparison['metrics'] if item['key'] == 'idleAlloc')['status'])

    def test_missing_or_inconsistent_evidence_is_rejected(self):
        mutations = {
            'missing outbox': lambda r: r.pop('outbox'),
            'malformed outbox': lambda r: r.update(outbox=[1]),
            'wrong scenario': lambda r: r.update(scenario='producer'),
            'missing latency': lambda r: r.pop('latency'),
            'non-idempotent publisher': lambda r: r.update(idempotent=False),
            'missing idle': lambda r: r['outbox'].pop('idle'),
            'short idle': lambda r: r['outbox']['idle'].update(requestedSeconds=61),
            'drain replaces active work': lambda r: r['outbox'].update(activeWorkloadSeconds=1),
            'long idle replaces active work': lambda r: r['outbox'].update(activeWorkloadSeconds=179),
            'wrong total duration': lambda r: r.update(durationMinutes=5),
            'nonfinite time': lambda r: r['outbox']['idle'].update(elapsedSeconds=float('nan')),
            'missing sample': lambda r: r['outbox']['idle']['samples'].__delitem__(slice(10, 13)),
            'thread pool change': lambda r: r['outbox']['idle']['samples'][30]['runtime'].update(threadPoolThreads=5),
            'counter mismatch': lambda r: r['outbox']['idle']['operations'].update(probes=1),
            'published while idle': lambda r: r['outbox']['idle']['samples'][30]['operations'].update(published=1),
            'idle error': lambda r: r['outbox']['idle']['samples'][30]['operations'].update(errors=1),
            'missing delivery': lambda r: r['outbox'].update(uniqueConsumedMessages=1),
            'not marked': lambda r: r['outbox']['activeOperations'].update(published=1),
            'active store error': lambda r: r['outbox']['activeOperations'].update(errors=1),
            'missing consumed count': lambda r: r.pop('consumedMessages'),
            'fractional duplicates': lambda r: r['outbox'].update(duplicatePublications=0.5),
            'unequal warmups': lambda r: r['outbox']['idleWarmup'].update(requestedSeconds=19),
        }
        for label, mutate in mutations.items():
            with self.subTest(label=label):
                result = outbox_result()
                mutate(result)
                with self.assertRaises(ValueError):
                    validate_outbox(result)
                with self.assertRaises(ValueError):
                    compare(outbox_result(), result, outbox_result())
                self.assertEqual('INCONCLUSIVE', assess_results({'B': result})['verdict'])

    def test_duplicate_publication_does_not_count_as_missing_unique_delivery(self):
        result = outbox_result()
        result['outbox']['duplicatePublications'] = 3
        self.assertEqual('pass', compare(result, result, result)['verdict'])

    def test_latency_sample_floor_remains_required(self):
        result = outbox_result()
        result['latency']['count'] = 9999
        with self.assertRaisesRegex(ValueError, 'at least 10000'):
            compare(result, result, result)

    def test_idle_trends_and_minimum_coverage_are_gated(self):
        result = outbox_result()
        idle = result['outbox']['idle']
        for sample in idle['samples'][40:]:
            sample['runtime']['allocatedBytes'] += (sample['elapsedSeconds'] - 40) * 100
        idle['runtimeEnd'] = copy.deepcopy(idle['samples'][-1]['runtime'])
        report = assess_results({'B': result})
        self.assertEqual('INCONCLUSIVE', report['verdict'])
        self.assertTrue(any('idle allocations' in error for error in report['errors']))
        result['outbox']['idle'] = idle_phase(15)
        result['outbox']['activeRequestedSeconds'] = 225
        result['outbox']['activeWorkloadSeconds'] = 225
        result['throughput'].update(runtime_result(intervals=225)['throughput'])
        report = assess_results({'B': result})
        self.assertTrue(any('at least 30 intervals' in error for error in report['errors']))

    def test_workflow_lane_is_manual_only_with_no_comparison_variants(self):
        workflow = (Path(__file__).resolve().parents[1] / 'workflows/stress-tests.yml').read_text(encoding='utf-8')
        lane = json.loads(re.search(r'\{"lane": "outbox-1b"[^\n]+\}', workflow).group())
        self.assertTrue(lane['manual_only'])
        self.assertEqual('dekaf', lane['client'])
        self.assertEqual(1, lane['brokers'])
        for key in ('run_3conn', 'run_adaptive', 'paired_samples'):
            self.assertNotIn(key, lane)
        self.assertIn('or .lane == $lane', workflow)


if __name__ == '__main__':
    unittest.main()
