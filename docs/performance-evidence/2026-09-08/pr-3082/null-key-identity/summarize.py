"""Fail closed on incomplete local wire-null construction diagnostics."""
import csv
import json
import math
from pathlib import Path
import re

root = Path(__file__).resolve().parent
rows = []
sizes_seen = set()
outputs_seen = set()
for phase in ('A1', 'B', 'A2'):
    for case in ('ConstructAndHash', 'TypedConstruction'):
        path = root / f'{phase}-{case}'
        reports = list((path / 'results').glob('*-report-full.json'))
        if len(reports) != 1:
            raise ValueError(f'Expected one report: {path}')
        cases = json.loads(reports[0].read_text(encoding='utf-8-sig'))['Benchmarks']
        if len(cases) != 1 or cases[0]['Method'] != case:
            raise ValueError(f'Case identity mismatch: {path}')
        benchmark = cases[0]
        stats = benchmark['Statistics']
        if stats['N'] != 25 or not math.isfinite(stats['Mean']):
            raise ValueError(f'Invalid measured sample count: {path}')
        allocated = next(metric['Value'] for metric in benchmark['Metrics']
                         if metric['Descriptor']['Id'] == 'Allocated Memory')
        log = (root / f'{phase}-{case}.log').read_text(encoding='utf-8-sig')
        warmup = re.findall(r'^WorkloadWarmup\s+\d+: (\d+) op, ([\d.]+) ns,', log, re.M)
        elapsed = sum(float(ns) for _, ns in warmup) / 1e9
        direct = re.findall(r'^WARM completed seconds=([\d.]+) operations=(\d+)', log, re.M)
        allocation = re.findall(r'^RAW operations=1000 bytes=(\d+)', log, re.M)
        sizes = re.findall(r'^SIZES .+', log, re.M)
        output = re.findall(r'^CORRECT .+', log, re.M)
        if len(warmup) != 30 or elapsed < 20 or len(direct) != 1 or float(direct[0][0]) < 20:
            raise ValueError(f'Insufficient actual-workload warmup: {path}')
        if len(allocation) != 1 or len(sizes) != 1 or len(output) != 1:
            raise ValueError(f'Missing allocation, layout or correctness probe: {path}')
        sizes_seen.add(sizes[0])
        outputs_seen.add(output[0])
        with (path / 'runtime.csv').open(newline='') as stream:
            runtime = list(csv.DictReader(stream))
        actual = [row for row in runtime if row['workload'].startswith('WorkloadActual')]
        warm = [row for row in runtime if row['workload'].startswith('WorkloadWarmup')]
        if len(actual) != 25 or len(warm) != 30:
            raise ValueError(f'Incomplete runtime coverage: {path}')
        rows.append({
            'phase': phase, 'case': case, 'mean_ns': stats['Mean'],
            'ci_lower_ns': stats['ConfidenceInterval']['Lower'], 'ci_upper_ns': stats['ConfidenceInterval']['Upper'],
            'iteration_mean_min_ns': stats['Min'], 'iteration_mean_max_ns': stats['Max'],
            'samples': stats['N'], 'allocated_bytes': allocated,
            'raw_1000_operation_bytes': int(allocation[0]),
            'direct_warmup_seconds': float(direct[0][0]), 'direct_warmup_operations': int(direct[0][1]),
            'bdn_warmup_seconds': elapsed, 'bdn_warmup_operations': sum(int(ops) for ops, _ in warmup),
            'measured_jit_methods': int(actual[-1]['jit_methods']) - int(warm[-1]['jit_methods']),
            'later_measured_jit_methods': int(actual[-1]['jit_methods']) - int(actual[0]['jit_methods']),
            'measured_jit_ms': float(actual[-1]['jit_ms']) - float(warm[-1]['jit_ms']),
            'max_measured_threads': max(int(row['threads']) for row in actual),
        })
if len(sizes_seen) != 1 or len(outputs_seen) != 1:
    raise ValueError('Product result sizes or normal-path outputs differ')
comparisons = []
for case in ('ConstructAndHash', 'TypedConstruction'):
    a1, b, a2 = [next(row for row in rows if row['phase'] == phase and row['case'] == case)
                 for phase in ('A1', 'B', 'A2')]
    comparisons.append({
        'case': case, 'B_vs_A1_percent': (b['mean_ns'] / a1['mean_ns'] - 1) * 100,
        'B_vs_A2_percent': (b['mean_ns'] / a2['mean_ns'] - 1) * 100,
        'A1_A2_drift_percent': (a2['mean_ns'] / a1['mean_ns'] - 1) * 100,
    })
summary = {'product_acceptance': 'INCONCLUSIVE: local construction diagnostic only; historical distinct-key regression remains blocking',
           'candidate_sha': (root / 'candidate-sha.txt').read_text().strip(),
           'baseline_sha': '405cc88836d813d3dc0b7df726e2d6063a1a00d1',
           'sizes': sizes_seen.pop(), 'output': outputs_seen.pop(), 'rows': rows, 'comparisons': comparisons}
(root / 'summary.json').write_text(json.dumps(summary, indent=2))
with (root / 'summary.csv').open('w', newline='') as stream:
    writer = csv.DictWriter(stream, fieldnames=rows[0].keys())
    writer.writeheader()
    writer.writerows(rows)
print(json.dumps(summary, indent=2))
