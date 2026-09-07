import csv
import json
from pathlib import Path
import statistics
import sys

root = Path(sys.argv[1])
summary = {}
for phase in ['A1', 'B', 'A2']:
    folder = root / phase
    metrics = json.loads((folder / 'metrics.json').read_text())
    series = json.loads((folder / 'series.json').read_text())
    rows = list(csv.DictReader((folder / 'cpu-stages.csv').open()))
    blocks = {}
    for column in ['SetupCpuTicks', 'StopCpuTicks', 'CleanupCpuTicks', 'SetupKernelTicks', 'StopKernelTicks']:
        means = [statistics.mean(int(row[column]) / 10 for row in rows[i:i + 1000]) for i in range(0, len(rows), 1000)]
        blocks[column] = {'mean_us': statistics.mean(means), 'block_sd_us': statistics.stdev(means),
                         'min_us': min(means), 'max_us': max(means), 'blocks_us': means}
    summary[phase] = {'metrics': metrics, 'blocks': blocks,
        'runtime': {key: series[-1][key] - series[0][key] for key in ['JitMethods', 'JitMilliseconds', 'Gen0', 'Gen1', 'Gen2']},
        'threads': sorted(set(s['Threads'] for s in series)),
        'heap_min_max': [min(s['HeapBytes'] for s in series), max(s['HeapBytes'] for s in series)]}

ratios = {}
for metric in ['LifecycleCpuNs', 'SetupCpuNs', 'StopCpuNs', 'CleanupCpuNs', 'LifecycleAllocatedBytes',
               'LifecycleOperationsPerSecond', 'P50Ns', 'P99Ns', 'MaxNs']:
    a, b, c = [summary[p]['metrics'][metric] for p in ['A1', 'B', 'A2']]
    ratios[metric] = {'A1': a, 'B': b, 'A2': c, 'B_A1_percent': 100 * (b / a - 1),
                      'B_A2_percent': 100 * (b / c - 1), 'A2_A1_drift_percent': 100 * (c / a - 1)}
summary['ratios'] = ratios
(root / 'cpu-analysis.json').write_text(json.dumps(summary, indent=2))
print(json.dumps({'ratios': ratios, 'runtime': {p: summary[p]['runtime'] | {'threads': summary[p]['threads']} for p in ['A1', 'B', 'A2']}}, indent=2))
