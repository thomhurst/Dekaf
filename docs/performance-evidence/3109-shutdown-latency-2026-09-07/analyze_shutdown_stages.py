import csv
import json
import math
from pathlib import Path
import sys

root = Path(__file__).parent
run_id = sys.argv[1]
artifacts = next((root / f'artifacts-{run_id}').iterdir())
destination = root / 'latency-3109-analysis'


def quantile(values, q):
    values = sorted(values)
    return values[math.ceil(len(values) * q) - 1] / 1e6


analysis = {}
for variant in ('startup', 'warmed'):
    for phase in ('A1', 'B', 'A2'):
        folder = artifacts / f'loaded-{phase}/shutdown-{variant}'
        metrics = json.loads((folder / 'metrics.json').read_text())
        series = json.loads((folder / 'series.json').read_text())
        warmup_series = json.loads((folder / 'warmup-series.json').read_text())
        with (folder / 'stages.csv').open() as stream:
            rows = [{key: int(value) for key, value in row.items()} for row in csv.DictReader(stream)]
        values = [row['End'] - row['Start'] for row in rows]
        scale = 1e9 / metrics['StopwatchFrequency']
        assert scale == 1
        stages = {'stop_setup': ('Release', 'Start'), 'handler_scheduling': ('Resume', 'Release'),
                  'handler_drain': ('HandlerEnd', 'Resume'), 'completion_return': ('End', 'HandlerEnd')}
        top = []
        for row in sorted(rows, key=lambda row: row['End'] - row['Start'], reverse=True)[:8]:
            parts = {name: (row[end] - row[start]) / 1e6 for name, (end, start) in stages.items()}
            block = (row['Sample'] - 1) // 100 + 1
            top.append({'sample': row['Sample'], 'total_ms': (row['End'] - row['Start']) / 1e6,
                        'dominant_stage': max(parts, key=parts.get), **parts,
                        'gc_during_bracket': row['Gen0After'] > row['Gen0Before'],
                        'gen2_during_bracket': row['Gen2After'] > row['Gen2Before'],
                        'threads_before': row['ThreadsBefore'], 'threads_after': row['ThreadsAfter'],
                        'block_jit_methods': series[block]['JitMethods'] - series[block-1]['JitMethods'],
                        'block_jit_ms': series[block]['JitMilliseconds'] - series[block-1]['JitMilliseconds']})
        slow = [row for row in rows if row['End'] - row['Start'] > 1e6]
        result = {
            'metrics': metrics, 'over_1ms': len(slow), 'over_1ms_after_2000': sum(row['Sample'] > 2000 for row in slow),
            'gc_bracket_count': sum(row['Gen0After'] > row['Gen0Before'] for row in rows),
            'slow_with_gc_bracket': sum(row['Gen0After'] > row['Gen0Before'] for row in slow),
            'jit_methods_during_measurement': series[-1]['JitMethods'] - series[0]['JitMethods'],
            'jit_ms_during_measurement': series[-1]['JitMilliseconds'] - series[0]['JitMilliseconds'],
            'threads_start': series[0]['ThreadPoolThreads'], 'threads_end': series[-1]['ThreadPoolThreads'],
            'gen2_during_measurement': series[-1]['Gen2'] - series[0]['Gen2'],
            'jit_ms_last_100_warmup': warmup_series[-1]['JitMilliseconds'] - warmup_series[-2]['JitMilliseconds'] if len(warmup_series) > 1 else None,
            'stage_p99_ms': {name: quantile([row[end] - row[start] for row in rows], .99) for name, (end, start) in stages.items()},
            'top_samples': top,
        }
        analysis[f'{variant}-{phase}'] = result
        print(json.dumps({'variant': variant, 'phase': phase, 'p50_ms': metrics['P50Ns']/1e6,
                          'p99_ms': metrics['P99Ns']/1e6, 'max_ms': metrics['MaxNs']/1e6,
                          'cpu_us_per_lifecycle': metrics['LifecycleCpuNsPerOperation']/1000,
                          'alloc_bytes_per_lifecycle': metrics['LifecycleAllocatedBytesPerOperation'],
                          **{key: result[key] for key in ('over_1ms', 'over_1ms_after_2000', 'slow_with_gc_bracket',
                              'jit_methods_during_measurement', 'jit_ms_during_measurement', 'threads_start', 'threads_end')},
                          'max_sample': top[0]}, indent=2))
(destination / f'stages-analysis-{run_id}.json').write_text(json.dumps(analysis, indent=2))
