"""Validate independent harness/product identities before starting a comparison."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess


SUITES = (
    'micro', 'pool', 'pool-recovery', 'admin', 'admin-pilot', 'admin-calibration', 'admin-profile',
    'dispatch', 'dispatch-adjacent', 'dispatch-loaded-adjacent', 'dispatch-loaded',
    'dispatch-pilot', 'dispatch-record-pilot', 'share-loaded', 'outbox',
    'outbox-loaded', 'outbox-adjacent', 'pool-loaded', 'pool-profile',
)


def command(*args):
    return subprocess.check_output(args, text=True).strip()


def verify(harness, baseline, candidate, pr, suite, repository):
    for name, value in [('harness', harness), ('baseline', baseline), ('candidate', candidate)]:
        if not re.fullmatch('[0-9a-f]{40}', value):
            raise ValueError(f'{name} must be an exact lowercase 40-character SHA')
    if pr <= 0 or suite not in SUITES:
        raise ValueError('A positive PR number and supported suite are required')
    if not re.fullmatch(r'[\w.-]+/[\w.-]+', repository, flags=re.ASCII):
        raise ValueError('Expected owner/repository')
    if command('git', 'rev-parse', 'HEAD') != harness:
        raise ValueError('Checked-out harness differs from requested SHA')
    for sha in (baseline, candidate):
        if command('git', 'rev-parse', '--verify', sha + '^{commit}') != sha:
            raise ValueError('Product commit did not resolve to the requested SHA')
    command('git', 'fetch', 'origin', 'main')
    main = command('git', 'rev-parse', 'FETCH_HEAD')
    if main != baseline:
        raise ValueError('Baseline is not fresh main; rebase the candidate and repin before a new campaign')
    command('git', 'merge-base', '--is-ancestor', baseline, candidate)
    calibration = suite in ('admin-calibration', 'admin-profile')
    if suite == 'admin-profile' and pr != 3138:
        raise ValueError('Administrative profiling currently covers PR 3138 only')
    if calibration:
        if candidate != baseline:
            raise ValueError('Calibration requires identical product SHAs')
    else:
        pull = json.loads(command('gh', 'api', f'repos/{repository}/pulls/{pr}'))
        if pull['state'] != 'open' or pull['head']['sha'] != candidate:
            raise ValueError('Candidate is not the current head of the open PR')
    return {'harness_sha': harness, 'baseline_sha': baseline, 'candidate_sha': candidate,
            'main_at_start': main, 'pr': pr, 'suite': suite, 'calibration': calibration,
            'workflow_sha': os.getenv('GITHUB_SHA'), 'status': 'VERIFIED',
            'scope': 'Identity validation only; never a product performance PASS'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--harness', required=True)
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--pr', type=int, required=True)
    parser.add_argument('--suite', required=True)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        report = verify(args.harness, args.baseline, args.candidate, args.pr, args.suite, args.repository)
    except Exception as error:
        args.output.write_text(json.dumps({'status': 'INVALID', 'error': str(error)}))
        raise
    args.output.write_text(json.dumps(report, indent=2))
