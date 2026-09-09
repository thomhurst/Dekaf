"""Plan complete-workload jobs and verify coverage, never performance acceptance."""
import argparse
from collections import Counter
from datetime import datetime
import json
import os
from pathlib import Path


DISPATCH_MODES = ('sync-records', 'sync-batches', 'pending-records', 'pending-batches')
OUTBOX_CASES = ('legacy-off', 'legacy-on', 'renewal-off', 'renewal-on')


def workloads(suite):
    if suite in ('dispatch-adjacent', 'dispatch-loaded-adjacent'):
        groups = {mode: [f'loaded/{mode}'] for mode in DISPATCH_MODES}
        if suite == 'dispatch-adjacent':
            groups['micro'] = [f'micro/{pattern}-{batch}'
                               for pattern in ('Repeated', 'Distinct', 'PendingPairs') for batch in (1, 16)]
            groups['shutdown'] = [f'shutdown/batch-{batch}-keys-{keys}'
                                  for batch in (1, 16) for keys in (1, 2)]
        return groups
    if suite in ('outbox-loaded', 'outbox-adjacent'):
        return {case: [case] for case in OUTBOX_CASES}
    return {'all': []}


def campaign(pins):
    if pins.get('status') != 'VERIFIED':
        raise ValueError('Campaign requires verified pins')
    return dict(pins=pins, run_id=os.environ.get('GITHUB_RUN_ID'), workloads=workloads(pins['suite']))


def settings(suite):
    if suite.startswith('dispatch'):
        return dict(loaded_warmup_seconds=361, loaded_duration_seconds=180)
    return dict(primer_seconds=20, warmup_seconds=480 if suite == 'outbox-adjacent' else 180,
                measured_seconds=180)


def validate_campaign(plan, harness, baseline, candidate, pr, suite):
    pins = plan['pins']
    expected = dict(harness_sha=harness, baseline_sha=baseline, candidate_sha=candidate,
                    pr=pr, suite=suite, status='VERIFIED')
    if any(pins.get(key) != value for key, value in expected.items()):
        raise ValueError('Campaign pins differ from requested comparison')
    if plan['workloads'] != workloads(suite):
        raise ValueError('Campaign workload manifest differs from this harness')
    if plan['run_id'] != os.environ.get('GITHUB_RUN_ID'):
        raise ValueError('Campaign belongs to another workflow run')
    return pins


def complete(plan, shard, evidence):
    expected = plan['workloads'][shard]
    if not expected:
        raise ValueError('Unsharded suites do not use coverage receipts')
    pins = plan['pins']
    dispatch = pins['suite'].startswith('dispatch')
    provenance = json.loads((evidence / ('provenance.json' if dispatch else 'plan.json')).read_text())
    actual = (provenance['harness'], provenance['baseline' if dispatch else 'A'],
              provenance['candidate' if dispatch else 'B'])
    if actual != (pins['harness_sha'], pins['baseline_sha'], pins['candidate_sha']):
        raise ValueError('Captured product/harness identities differ from campaign')
    if provenance.get('shard') != shard:
        raise ValueError('Captured shard differs from campaign')
    declared_settings = settings(pins['suite'])
    if any(provenance.get(key) != value for key, value in declared_settings.items()):
        raise ValueError('Capture settings differ from the declared suite')
    captures = json.loads((evidence / 'capture-order.json').read_text())
    found = Counter()
    per_case = {}
    measurement_started = False
    for capture in captures:
        case = f"{capture['kind']}/{capture['name']}" if dispatch else capture['case']
        phase = capture['phase']
        smoke = phase in ('DryA', 'DryB')
        if capture['smoke'] != smoke or capture['label'] != ('B' if phase in ('DryB', 'B') else 'A'):
            raise ValueError('Incorrect phase/product mapping')
        if smoke and measurement_started:
            raise ValueError('Validation must precede measurements in each job')
        measurement_started |= not smoke
        if datetime.fromisoformat(capture['completed_utc']) <= datetime.fromisoformat(capture['started_utc']):
            raise ValueError('Capture did not complete with valid timestamps')
        found[(case, phase)] += 1
        per_case.setdefault(case, []).append(phase)
    phases = ['DryA', 'DryB', 'A1', 'B', 'A2']
    required = Counter((case, phase) for case in expected for phase in phases)
    if found != required or any(value != phases for value in per_case.values()):
        raise ValueError('Missing, duplicated, extra or reordered workload phases')
    return dict(campaign=plan, shard=shard, workloads=expected, settings=declared_settings, collection='COMPLETE',
                acceptance='NOT_EVALUATED')


def aggregate(plan, receipts, comparison_result):
    if comparison_result != 'success':
        raise ValueError('One or more comparison jobs failed, were cancelled or skipped')
    expected = plan['workloads']
    found = Counter()
    for receipt in receipts:
        shard = receipt['shard']
        if receipt['campaign'] != plan:
            raise ValueError('Shard belongs to a different campaign')
        if shard not in expected or receipt['workloads'] != expected[shard] or receipt['collection'] != 'COMPLETE':
            raise ValueError('Shard did not complete its declared workload coverage')
        if receipt.get('settings') != settings(plan['pins']['suite']):
            raise ValueError('Shard settings differ from the declared suite')
        found[shard] += 1
    if found != Counter({shard: 1 for shard in expected}):
        raise ValueError('Missing, duplicated or unexpected shards')
    return dict(collection='COMPLETE', acceptance='NOT_EVALUATED', campaign=plan,
                shards=list(expected), note='All declared workloads collected; review protected metrics for acceptance.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('plan', 'verify', 'complete', 'aggregate'))
    parser.add_argument('--campaign', type=Path, default=Path('performance-campaign.json'))
    parser.add_argument('--pins', type=Path, default=Path('performance-pins.json'))
    parser.add_argument('--shard')
    parser.add_argument('--evidence', type=Path, default=Path('evidence'))
    parser.add_argument('--receipts', type=Path, default=Path('receipts'))
    parser.add_argument('--comparison-result')
    args = parser.parse_args()
    if args.command == 'plan':
        plan = campaign(json.loads(args.pins.read_text()))
        args.campaign.write_text(json.dumps(plan, indent=2))
        print('matrix=' + json.dumps({'include': [{'shard': shard} for shard in plan['workloads']]}))
        print('sharded=' + str(list(plan['workloads']) != ['all']).lower())
        return
    plan = json.loads(args.campaign.read_text())
    if args.command == 'verify':
        import subprocess
        if subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip() != os.environ['HARNESS_SHA']:
            raise ValueError('Checked-out harness differs from campaign')
        pins = validate_campaign(plan, os.environ['HARNESS_SHA'], os.environ['BASELINE_SHA'],
                                 os.environ['CANDIDATE_SHA'], int(os.environ['PR']), os.environ['SUITE'])
        args.pins.write_text(json.dumps(pins, indent=2))
    elif args.command == 'complete':
        receipt = complete(plan, args.shard, args.evidence)
        Path('shard-completion.json').write_text(json.dumps(receipt, indent=2))
    else:
        receipts = [json.loads(path.read_text()) for path in args.receipts.rglob('shard-completion.json')]
        result = aggregate(plan, receipts, args.comparison_result)
        Path('campaign-completion.json').write_text(json.dumps(result, indent=2))
        print(result['note'])


if __name__ == '__main__':
    main()
