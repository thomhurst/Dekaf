import copy
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import performance_shards as shards
from outbox_loaded_aba import execution_plan
from verify_performance_pins import SUITES


ROOT = Path(__file__).resolve().parents[2]
DISPATCH = ROOT / '.github/benchmarks/dispatch-aba'
sys.path.insert(0, str(DISPATCH))
try:
    spec = importlib.util.spec_from_file_location('dispatch_shard_schedule', DISPATCH / 'run_adjacent.py')
    adjacent = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(adjacent)
finally:
    sys.path.pop(0)


class ShardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def campaign(self, suite):
        return shards.campaign(dict(status='VERIFIED', suite=suite, pr=3085,
                                    harness_sha='a' * 40, baseline_sha='b' * 40, candidate_sha='c' * 40))

    def evidence(self, plan, shard):
        pins = plan['pins']
        dispatch = pins['suite'].startswith('dispatch')
        if dispatch:
            rows = list(adjacent.schedule(pins['suite'] == 'dispatch-loaded-adjacent', shard))
            provenance = dict(harness=pins['harness_sha'], baseline=pins['baseline_sha'],
                              candidate=pins['candidate_sha'], shard=shard,
                              loaded_warmup_seconds=361, loaded_duration_seconds=180)
        else:
            rows = [dict(phase=phase, label=label, smoke=smoke, case=f'{store}-{listener}')
                    for phase, label, smoke, store, listener in execution_plan(pins['suite'] == 'outbox-adjacent', shard)]
            provenance = dict(harness=pins['harness_sha'], A=pins['baseline_sha'], B=pins['candidate_sha'],
                              shard=shard, primer_seconds=20, measured_seconds=180,
                              warmup_seconds=480 if pins['suite'] == 'outbox-adjacent' else 180)
        for index, row in enumerate(rows):
            row.update(started_utc=f'2026-09-09T12:{index:02}:00+00:00',
                       completed_utc=f'2026-09-09T12:{index:02}:30+00:00')
        self.write('provenance.json' if dispatch else 'plan.json', provenance)
        self.write('capture-order.json', rows)
        return rows

    def write(self, name, value):
        (self.root / name).write_text(json.dumps(value))

    def test_all_dispatch_groups_preserve_exact_full_matrix(self):
        for suite, loaded_only, count in [('dispatch-adjacent', False, 6), ('dispatch-loaded-adjacent', True, 4)]:
            groups = shards.workloads(suite)
            self.assertEqual(len(groups), count)
            combined = [row for shard in groups for row in adjacent.schedule(loaded_only, shard)]
            key = lambda row: (row['kind'], row['name'], row['phase'])
            self.assertCountEqual([key(row) for row in combined], [key(row) for row in adjacent.schedule(loaded_only)])
            for shard, expected in groups.items():
                self.assertEqual([f"{row['kind']}/{row['name']}" for row in adjacent.schedule(loaded_only, shard)
                                  if row['phase'] == 'A1'], expected)

    def test_all_outbox_groups_preserve_both_validation_and_triplet_phases(self):
        for suite, adjacent_controls in [('outbox-loaded', False), ('outbox-adjacent', True)]:
            groups = shards.workloads(suite)
            self.assertEqual(len(groups), 4)
            combined = [row for shard in groups for row in execution_plan(adjacent_controls, shard)]
            self.assertCountEqual(combined, list(execution_plan(adjacent_controls)))
            for shard in groups:
                self.assertEqual([row[0] for row in execution_plan(adjacent_controls, shard)], ['DryA', 'DryB', 'A1', 'B', 'A2'])

    def test_other_suites_remain_single_jobs(self):
        for suite in SUITES:
            if suite not in ('dispatch-adjacent', 'dispatch-loaded-adjacent', 'outbox-loaded', 'outbox-adjacent'):
                self.assertEqual(shards.workloads(suite), {'all': []})

    def test_unknown_and_incompatible_driver_selections_fail(self):
        for shard in ('typo', 'micro', 'shutdown'):
            with self.assertRaises(ValueError):
                list(adjacent.schedule(True, shard))
        with self.assertRaises(ValueError):
            list(execution_plan(shard='typo'))

    def test_each_complete_suite_aggregates_without_granting_performance_acceptance(self):
        for suite in ('dispatch-adjacent', 'dispatch-loaded-adjacent', 'outbox-loaded', 'outbox-adjacent'):
            plan = self.campaign(suite)
            receipts = []
            for shard in plan['workloads']:
                self.evidence(plan, shard)
                receipts.append(shards.complete(plan, shard, self.root))
            result = shards.aggregate(plan, receipts, 'success')
            self.assertEqual(result['collection'], 'COMPLETE')
            self.assertEqual(result['acceptance'], 'NOT_EVALUATED')

    def test_missing_duplicate_extra_and_reordered_captures_fail(self):
        plan = self.campaign('outbox-loaded')
        rows = self.evidence(plan, 'legacy-off')
        variants = [rows[:-1], rows + [rows[-1]], rows[:2] + [rows[3], rows[2], rows[4]],
                    rows[:2] + [dict(rows[2], case='extra')] + rows[3:],
                    [dict(rows[0], completed_utc=rows[0]['started_utc'])] + rows[1:],
                    rows[:2] + [dict(rows[2], label='B')] + rows[3:],
                    rows[:2] + [dict(rows[2], smoke=True)] + rows[3:]]
        for variant in variants:
            with self.subTest(variant=variant), self.assertRaises(ValueError):
                self.write('capture-order.json', variant)
                shards.complete(plan, 'legacy-off', self.root)

    def test_wrong_capture_identities_settings_or_shard_fail(self):
        plan = self.campaign('outbox-loaded')
        self.evidence(plan, 'legacy-off')
        provenance = json.loads((self.root / 'plan.json').read_text())
        for key, value in [('A', 'd' * 40), ('B', 'd' * 40), ('harness', 'd' * 40),
                           ('shard', 'renewal-on'), ('warmup_seconds', 30)]:
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.write('plan.json', {**provenance, key: value})
                shards.complete(plan, 'legacy-off', self.root)

    def test_failed_missing_duplicate_and_foreign_shards_cannot_complete_campaign(self):
        plan = self.campaign('outbox-loaded')
        receipts = []
        for shard in plan['workloads']:
            self.evidence(plan, shard)
            receipts.append(shards.complete(plan, shard, self.root))
        for status in ('failure', 'cancelled', 'skipped'):
            with self.subTest(status=status), self.assertRaises(ValueError):
                shards.aggregate(plan, receipts, status)
        for invalid in (receipts[:-1], receipts + [receipts[0]], []):
            with self.assertRaises(ValueError):
                shards.aggregate(plan, invalid, 'success')
        for mutation in ('pins', 'workloads', 'collection', 'settings'):
            invalid = copy.deepcopy(receipts)
            if mutation == 'pins':
                invalid[0]['campaign']['pins']['candidate_sha'] = 'd' * 40
            elif mutation == 'workloads':
                invalid[0]['workloads'] = []
            elif mutation == 'settings':
                invalid[0]['settings']['warmup_seconds'] = 1
            else:
                invalid[0]['collection'] = 'INCOMPLETE'
            with self.assertRaises(ValueError):
                shards.aggregate(plan, invalid, 'success')

    def test_queued_jobs_use_original_verified_pins_and_allow_failed_job_reruns(self):
        with patch.dict(os.environ, GITHUB_RUN_ID='123', GITHUB_RUN_ATTEMPT='1'):
            plan = self.campaign('outbox-loaded')
            with patch.dict(os.environ, GITHUB_RUN_ATTEMPT='2'):
                result = shards.validate_campaign(plan, 'a' * 40, 'b' * 40, 'c' * 40, 3085, 'outbox-loaded')
                self.assertEqual(result, plan['pins'])
            with patch.dict(os.environ, GITHUB_RUN_ID='124'), self.assertRaises(ValueError):
                shards.validate_campaign(plan, 'a' * 40, 'b' * 40, 'c' * 40, 3085, 'outbox-loaded')

    def test_campaign_rejects_mismatched_pins_and_coverage(self):
        plan = self.campaign('outbox-loaded')
        for field in ('harness_sha', 'baseline_sha', 'candidate_sha', 'pr', 'suite', 'status'):
            wrong = copy.deepcopy(plan)
            wrong['pins'][field] = 'wrong'
            with self.subTest(field=field), self.assertRaises(ValueError):
                shards.validate_campaign(wrong, 'a' * 40, 'b' * 40, 'c' * 40, 3085, 'outbox-loaded')
        wrong = copy.deepcopy(plan)
        wrong['workloads'].pop('legacy-off')
        with self.assertRaises(ValueError):
            shards.validate_campaign(wrong, 'a' * 40, 'b' * 40, 'c' * 40, 3085, 'outbox-loaded')


if __name__ == '__main__':
    unittest.main()
