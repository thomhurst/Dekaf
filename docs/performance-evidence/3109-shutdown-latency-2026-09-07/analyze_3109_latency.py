"""Inspect original samples without changing the official A-B-A verdict."""
from array import array
import csv
import json
import math
from pathlib import Path
import statistics
import sys

root = Path(__file__).parent
artifacts = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else next((root / 'artifacts-34149484062').iterdir())
destination = root / 'latency-3109-analysis'
destination.mkdir(exist_ok=True)


def quantile(values, q):
    values = sorted(values)
    return values[math.ceil(len(values) * q) - 1] / 1e6


def summary(values):
    return {'samples': len(values), 'p50_ms': quantile(values, .5), 'p95_ms': quantile(values, .95),
            'p99_ms': quantile(values, .99), 'p999_ms': quantile(values, .999),
            'max_ms': max(values) / 1e6, 'over_1ms': sum(x > 1e6 for x in values),
            'over_5ms': sum(x > 5e6 for x in values)}


phases = {}
blocks = []
for phase in ('A1', 'B', 'A2'):
    folder = artifacts / f'loaded-{phase}/shutdown-full-queue'
    values = array('q')
    values.frombytes((folder / 'latency-ticks.bin').read_bytes())
    series = json.loads((folder / 'series.json').read_text())
    metrics = json.loads((folder / 'metrics.json').read_text())
    assert metrics['StopwatchFrequency'] == 1_000_000_000 and len(values) == 10000
    top = sorted(range(len(values)), key=lambda i: values[i], reverse=True)[:12]
    phases[phase] = {'official': summary(values),
                     'diagnostic_first_2000': summary(values[:2000]),
                     'diagnostic_last_8000': summary(values[2000:]),
                     'top_samples': [{'sample': i + 1, 'ms': values[i] / 1e6, 'block': i // 100 + 1} for i in top],
                     'gen2_first_observed': series[0]['Gen2'], 'gen2_last_observed': series[-1]['Gen2'],
                     'allocated_bytes_between_observations': series[-1]['AllocatedBytes'] - series[0]['AllocatedBytes'],
                     'whole_lifecycle_seconds': metrics['ElapsedSeconds']}
    previous = None
    for index, point in enumerate(series):
        row = {'phase': phase, 'block': index + 1, 'sample_end': point['Samples'], **summary(values[index*100:(index+1)*100]),
               'elapsed_seconds': point['ElapsedSeconds'], 'heap_bytes': point['HeapBytes'], 'rss_bytes': point['RssBytes'],
               'gen2': point['Gen2'], 'block_gen2': point['Gen2'] - previous['Gen2'] if previous else None,
               'block_lifecycle_cpu_ms': (point['CpuTicks'] - previous['CpuTicks']) / 10000 if previous else None,
               'block_wall_ms': (point['ElapsedSeconds'] - previous['ElapsedSeconds']) * 1000 if previous else None}
        blocks.append(row)
        previous = point
    print(json.dumps({'phase': phase, **phases[phase]}, indent=2))
(destination / 'analysis.json').write_text(json.dumps(phases, indent=2))
with (destination / 'blocks.csv').open('w', newline='') as stream:
    writer = csv.DictWriter(stream, fieldnames=blocks[0].keys())
    writer.writeheader()
    writer.writerows(blocks)
try:
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
except ImportError:
    pass
else:
    fig, axes = plt.subplots(3, 1, figsize=(11, 8), sharex=True)
    for phase, color in [('A1', '#5470c6'), ('B', '#bf5b17'), ('A2', '#27886a')]:
        selected = [row for row in blocks if row['phase'] == phase]
        x = [row['sample_end'] for row in selected]
        axes[0].plot(x, [row['p50_ms'] for row in selected], label=phase, color=color)
        axes[1].plot(x, [row['max_ms'] for row in selected], label=phase, color=color, alpha=.8)
        axes[2].plot(x, [row['block_wall_ms'] for row in selected], label=phase, color=color)
    for axis in axes:
        axis.grid(alpha=.2)
        axis.axvline(2000, color='gray', linestyle='--', alpha=.5)
    axes[0].set_ylabel('Block p50 (ms)')
    axes[1].set_ylabel('Block maximum (ms)')
    axes[2].set_ylabel('Lifecycle / 100 stops (ms)')
    axes[2].set_xlabel('Shutdown sample number (100-sample blocks)')
    axes[0].legend()
    fig.suptitle('PR #3109: original shutdown latencies over time\nAll 10,000 samples retained per phase; early/late split is diagnostic only')
    fig.tight_layout()
    fig.savefig(destination / 'latency-timeline.png', dpi=160)
