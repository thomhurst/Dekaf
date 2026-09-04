"""Report exact-revision screening comparisons without inventing absent metrics."""
import argparse
import json
import math
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from stress_report import cpu_micros_per_message, effective_rate, median_interval_rate


def metrics(result):
    latency = result.get('latency') or {}
    throughput = result.get('throughput') or {}
    return {
        'Throughput (msg/s)': effective_rate(result),
        'Median interval throughput (msg/s)': median_interval_rate(result),
        'Latency p50 (µs)': latency.get('p50Us'),
        'Latency p95 (µs)': latency.get('p95Us'),
        'Latency p99 (µs)': latency.get('p99Us'),
        'Latency max (µs)': latency.get('maxUs'),
        'CPU (µs/msg)': cpu_micros_per_message(result),
        'Allocation (B/msg)': result.get('allocatedBytesPerMessage'),
        'Steady/peak ratio': result.get('steadyStatePeakRatio'),
        'Errors': (throughput.get('totalErrors') or 0) + (throughput.get('totalDeliveryErrors') or 0),
    }


def finite(value):
    return isinstance(value, (int, float)) and math.isfinite(value)


def display(value):
    return f'{value:,.3f}' if finite(value) else 'N/A'


def delta(candidate, baseline):
    if not finite(candidate) or not finite(baseline):
        return 'N/A'
    if baseline == 0:
        return '0.0%' if candidate == 0 else 'from zero'
    return f'{(candidate / baseline - 1) * 100:+.2f}%'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('directory', type=Path)
    parser.add_argument('--summary', type=Path)
    args = parser.parse_args()
    args.directory.mkdir(parents=True, exist_ok=True)
    output = [f"# PR {os.environ.get('PR_NUMBER', '')}: five-minute main/PR/main screening", '',
              f"Main: `{os.environ.get('BASELINE_SHA', '')}`", '',
              f"Candidate: `{os.environ.get('CANDIDATE_SHA', '')}`", '',
              'One VM per PR; client CPUs 6–7, broker CPUs 0–5. Fresh Kafka 4.3.1 broker per segment; '
              '1,000-byte values, six partitions. Five measured minutes per segment; setup excluded. '
              'Producer uses serial ProduceAsync, Acks.Leader and one connection; consumer uses '
              'ForHighThroughput and replays a two-million-message seed. No profiler or Confluent run.', '',
              'These are screening observations, not a formal Pareto PASS. Consumer latency is not '
              'measured by this harness; missing metrics stay N/A. Review maximum latency and '
              'control drift as well as mean rates. No automatic repeat or merge.', '']
    comparisons = {}
    incomplete = False
    for linger in os.environ.get('LINGERS', '5').split():
        directory = args.directory / f'linger-{linger}'
        segment_results = {}
        for segment in ['main-a', 'candidate', 'main-b']:
            paths = list((directory / segment).glob('stress-test-results*.json'))
            if len(paths) != 1:
                incomplete = True
                output += [f'**Missing/ambiguous result: linger {linger}, {segment}.**', '']
                continue
            document = json.loads(paths[0].read_text(encoding='utf-8-sig'))
            if len(document.get('results', [])) != 1:
                raise ValueError(f'Expected exactly one scenario in {paths[0]}')
            result = document['results'][0]
            if result.get('durationMinutes') != 5:
                raise ValueError(f'Unexpected duration in {paths[0]}')
            segment_results[segment] = {'path': str(paths[0]), 'metrics': metrics(result), 'result': result}
        comparisons[linger] = segment_results
        if len(segment_results) != 3:
            continue
        output += [f'## Linger {linger} ms', '',
                   '| Metric | Main A | PR | Main B | PR vs A | PR vs B | Main B vs A |',
                   '|---|---:|---:|---:|---:|---:|---:|']
        a, candidate, b = [segment_results[s]['metrics'] for s in ['main-a', 'candidate', 'main-b']]
        for metric in a:
            values = [a[metric], candidate[metric], b[metric]]
            output.append(f'| {metric} | ' + ' | '.join(map(display, values)) +
                          f' | {delta(values[1], values[0])} | {delta(values[1], values[2])} | {delta(values[2], values[0])} |')
        output.append('')
    report = '\n'.join(output)
    (args.directory / 'comparison.md').write_text(report, encoding='utf-8')
    (args.directory / 'comparison.json').write_text(json.dumps(comparisons, indent=2), encoding='utf-8')
    if args.summary:
        with args.summary.open('a', encoding='utf-8') as handle:
            handle.write(report)
    print(report)
    return 1 if incomplete else 0


if __name__ == '__main__':
    raise SystemExit(main())
