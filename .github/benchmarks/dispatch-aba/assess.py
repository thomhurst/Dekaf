"""Read-only assessment of retained dispatch evidence; never sets a PR gate."""
import argparse
import array
import csv
import json
import math
from pathlib import Path
import re
import statistics
import sys

import run

PHASES = ('A1', 'B', 'A2')
PINS = {
    'baseline': '5df2f0d03607389384b5c1466e17812a9084fac9',
    'candidate': '2a550007ccb091e8f2bf9275a7646277e984dff8',
    'harness': '7614406221b63c0b758f25e0722c8a3bf3ef9d0f',
}


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def compare(values, loss, drift, *, higher_better=False, absolute=False):
    """Point estimates only. A passing screen is not equivalence or acceptance."""
    require(len(values) == 3 and all(math.isfinite(v) and v >= 0 for v in values), 'Invalid metric values')
    a1, candidate, a2 = values
    differences = [candidate - a1, candidate - a2]
    deltas = [(candidate / control - 1) * 100 if control else None for control in (a1, a2)]
    drift_pct = (a2 / a1 - 1) * 100 if a1 else None
    if absolute:
        loss_signal = any(delta > loss for delta in differences)
        control_drift = abs(a2 - a1) > max(drift, a1 * .03)
    else:
        require(a1 > 0 and a2 > 0, 'Relative metric has a zero control')
        loss_signal = any((-delta if higher_better else delta) > loss for delta in deltas)
        control_drift = abs(drift_pct) > drift
    return dict(zip(PHASES, values)) | dict(
        candidate_delta_percent=deltas, candidate_delta_absolute=differences,
        control_drift_percent=drift_pct, control_drift_absolute=a2-a1,
        loss_signal=loss_signal, control_drift_exceeds_tolerance=control_drift)


def verify_inventory(root):
    inventory = read_json(root / 'sha256.json')
    require(bool(inventory), 'Empty artifact inventory')
    for name, expected in inventory.items():
        path = (root / name).resolve()
        require(path.is_relative_to(root.resolve()), f'Inventory path escapes evidence: {name}')
        require(path.is_file(), f'Missing retained file: {name}')
        require(run.digest(path) == expected.lower(), f'Artifact hash mismatch: {name}')
    for label in ('A', 'B'):
        for name in ('Dekaf.dll', 'Dekaf.Abstractions.dll'):
            product = root / f'product-{label}/src/Dekaf/bin/Release/net10.0' / name
            for host in ('Harness', 'Loaded'):
                require(run.digest(product) == run.digest(root / f'{host}-{label}' / name),
                        f'Wrong {host}-{label} binding for {name}')
    return len(inventory)


def micro(root, phase, pattern, batch):
    folder = root / f'{phase}-micro-{pattern}-{batch}'
    files = list((folder / 'results').glob('*-report-full.json'))
    require(len(files) == 1, f'Expected one BDN report: {folder}')
    benchmarks = read_json(files[0])['Benchmarks']
    require(len(benchmarks) == 1, f'Expected one BDN case: {folder}')
    benchmark = benchmarks[0]
    require(benchmark['Parameters'] == f'Pattern={pattern}&BatchSize={batch}', 'Wrong BDN parameters')
    samples = benchmark['Measurements']
    stages = {stage: [s for s in samples if s['IterationMode'] == 'Workload' and s['IterationStage'] == stage]
              for stage in ('Warmup', 'Actual', 'Result')}
    for stage, count in [('Warmup', 30), ('Actual', 25), ('Result', 25)]:
        require(len(stages[stage]) == count, f'Wrong {stage} sample count: {folder}')
        require([s['IterationIndex'] for s in stages[stage]] == list(range(1, count + 1)),
                f'Duplicated or missing {stage} iteration: {folder}')
        require(all(s['Operations'] > 0 and math.isfinite(s['Nanoseconds']) and s['Nanoseconds'] > 0
                    for s in stages[stage]), f'Invalid {stage} sample: {folder}')
    warmup_seconds = sum(s['Nanoseconds'] for s in stages['Warmup']) / 1e9
    require(warmup_seconds >= 20, f'Insufficient elapsed BDN workload warmup: {folder}')
    stats = benchmark['Statistics']
    require(stats['N'] == 25 and len(stats['OriginalValues']) == 25, 'BDN statistics dropped samples')
    normalized = [s['Nanoseconds'] / s['Operations'] for s in stages['Result']]
    require(all(math.isclose(a, b, rel_tol=1e-10) for a, b in zip(normalized, stats['OriginalValues'])),
            'BDN result samples disagree with statistics')
    require(math.isclose(statistics.mean(normalized), stats['Mean'], rel_tol=1e-10), 'BDN mean mismatch')
    log = (root / f'{folder.name}.log').read_text(encoding='utf-8-sig')
    warm = re.search(r'WARM completed seconds=([\d.]+) operations=(\d+)', log)
    require(warm is not None and float(warm[1]) >= 20 and int(warm[2]) > 0, 'Missing direct workload warmup')
    steady = re.findall(r'STEADY_ALLOCATION pattern=(\w+) batchSize=(\d+) largestBatch=(\d+) records=(\d+) bytes=(\d+)', log)
    require(len(steady) == 1, 'Missing or ambiguous steady allocation check')
    recorded_pattern, recorded_batch, largest_batch, records, allocated = steady[0]
    require(recorded_pattern == pattern and int(recorded_batch) == batch and int(records) == 262144 - 256,
            'Wrong steady allocation workload')
    require(phase != 'B' or int(allocated) == 0, 'Candidate steady allocation is nonzero')
    require(pattern != 'PendingPairs' or int(largest_batch) == batch, 'Missing pending batch size coverage')
    with (folder / 'runtime.csv').open(encoding='utf-8-sig', newline='') as stream:
        runtime = list(csv.DictReader(stream))
    actual = [s for s in runtime if s['workload'].startswith('WorkloadActual')]
    warm_rows = [s for s in runtime if s['workload'].startswith('WorkloadWarmup')]
    require(len(actual) == 25 and len(warm_rows) == 30, 'Missing BDN runtime boundary rows')
    numeric = [{k: float(v) for k, v in s.items() if k != 'workload'} for s in actual]
    require(all(math.isfinite(value) and value >= 0 for row in numeric for value in row.values()),
            'Invalid BDN runtime counters')
    require(all(b['timestamp'] > a['timestamp'] for a, b in zip(numeric, numeric[1:])),
            'Unordered BDN runtime samples')
    return dict(mean_ns=stats['Mean'], confidence_interval=stats['ConfidenceInterval'],
                sample_count=25, min_ns=stats['Min'], max_iteration_ns=stats['Max'],
                standard_deviation_ns=stats['StandardDeviation'],
                bytes_per_lifetime=benchmark['Memory']['BytesAllocatedPerOperation'],
                amortized_bytes_per_record=benchmark['Memory']['BytesAllocatedPerOperation'] / 262144,
                steady_allocated_bytes=int(allocated), steady_records=int(records),
                direct_warmup_seconds=float(warm[1]), direct_warmup_records=int(warm[2]),
                bdn_warmup_seconds=warmup_seconds,
                bdn_warmup_lifetimes=sum(s['Operations'] for s in stages['Warmup']),
                actual_lifetimes=sum(s['Operations'] for s in stages['Actual']),
                first_boundary_jit=int(actual[0]['jit_methods'])-int(warm_rows[-1]['jit_methods']),
                interior_boundary_jit=int(actual[-1]['jit_methods'])-int(actual[0]['jit_methods']),
                interior_boundary_jit_ms=float(actual[-1]['jit_ms'])-float(actual[0]['jit_ms']),
                runtime_ranges={k: [min(s[k] for s in numeric), max(s[k] for s in numeric)] for k in numeric[0]})


def verify_latencies(folder, metrics):
    ticks = array.array('q')
    with (folder / 'latency-ticks.bin').open('rb') as stream:
        ticks.fromfile(stream, metrics['Measured'])
        require(stream.read(1) == b'', 'Extra latency samples')
    if sys.byteorder != 'little':
        ticks.byteswap()
    require(len(ticks) > 0 and min(ticks) >= 0, 'Invalid raw latencies')
    ordered = sorted(ticks)
    for key, percentile in [('P50Ns', .5), ('P99Ns', .99), ('MaxNs', 1)]:
        expected = ordered[math.ceil(len(ordered) * percentile) - 1] * 1e9 / metrics['StopwatchFrequency']
        require(math.isclose(expected, metrics[key], rel_tol=1e-12), f'Raw latency mismatch: {key}')
    del ordered, ticks
    series = read_json(folder / 'latency-series.json')
    require(sum(s['completed'] for s in series) == metrics['Measured'], 'Latency series dropped samples')


def loaded(root, phase, mode):
    folder = root / phase / f'{phase}-{mode}'
    rate = 1000 if mode.startswith('pending') else 50000
    metrics = run.validate_loaded(folder, run.WARMUP, run.DURATION, rate, acceptance=True)
    require(metrics['Mode'] == mode and metrics['OfferedMessagesPerSecond'] == rate, 'Wrong loaded configuration')
    require(metrics['StopwatchFrequency'] > 0 and metrics['MeasurementEnd'] > metrics['MeasurementStart'],
            'Invalid loaded measurement clock')
    seconds = (metrics['MeasurementEnd']-metrics['MeasurementStart']) / metrics['StopwatchFrequency']
    require(math.isclose(seconds, metrics['MeasuredDurationSeconds'], rel_tol=1e-12), 'Loaded duration mismatch')
    require(math.isclose(metrics['Measured']/seconds, metrics['MessagesPerSecond'], rel_tol=1e-12),
            'Throughput is not completed throughput')
    verify_latencies(folder, metrics)
    rows = read_json(folder / 'series.json')
    require(all(b['Timestamp'] > a['Timestamp'] for a, b in zip(rows, rows[1:])), 'Unordered loaded runtime series')
    actual = [s for s in rows if metrics['MeasurementStart'] <= s['Timestamp'] <= metrics['MeasurementEnd']]
    require(len(actual) >= 2, 'Missing measured runtime series')
    metrics['RuntimeSamples'] = len(actual)
    metrics['MeasuredRuntimeRanges'] = {k: [min(s[k] for s in actual), max(s[k] for s in actual)]
                                       for k in actual[0] if k != 'Timestamp'}
    metrics['MeasuredJitMsDelta'] = metrics['JitMsEnd']-metrics['JitMsStart']
    return metrics


def assess(root):
    provenance = read_json(root / 'provenance.json')
    require(all(provenance.get(k) == v for k, v in PINS.items()), 'Evidence pins differ from this campaign')
    require(provenance.get('phases') == list(PHASES), 'Wrong phase design')
    verified = verify_inventory(root)
    micro_results, loaded_results = {}, {}
    for pattern, batch in run.CASES:
        phases = {phase: micro(root, phase, pattern, batch) for phase in PHASES}
        micro_results[f'{pattern}-{batch}'] = dict(phases=phases,
            mean_ns=compare([phases[p]['mean_ns'] for p in PHASES], 1, 2))
    for mode in run.MODES:
        phases = {phase: loaded(root, phase, mode) for phase in PHASES}
        comparisons = {}
        for metric, tolerance in [('MessagesPerSecond', 3), ('CpuNsPerMessage', 3),
                                  ('P50Ns', 5), ('P99Ns', 5), ('MaxNs', 5), ('AllocatedBytesPerMessage', 1)]:
            comparisons[metric] = compare([phases[p][metric] for p in PHASES], tolerance, tolerance,
                higher_better=metric == 'MessagesPerSecond', absolute=metric == 'AllocatedBytesPerMessage')
        loaded_results[mode] = dict(phases=phases, comparisons=comparisons)
    return dict(provenance=provenance, verified_artifact_files=verified, micro=micro_results, loaded=loaded_results,
                acceptance='INCONCLUSIVE',
                limitations=['Point screens are not statistical equivalence or an acceptance decision.',
                             'Method attribution and startup/runtime trends need review without removing samples.',
                             'Loaded shutdown/error-path performance and long-run stability remain unmeasured.',
                             'BDN iteration maxima and boundary CPU counters are not per-message metrics.'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    require(not args.output.resolve().is_relative_to(args.evidence.resolve()), 'Write assessment outside retained evidence')
    result = assess(args.evidence)
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False))
    print(f"Verified {result['verified_artifact_files']} files; full acceptance remains INCONCLUSIVE.")


if __name__ == '__main__':
    main()
