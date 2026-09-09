"""Validate retained probe accounting; never grants performance acceptance."""
import json
import math
from pathlib import Path


def validate(path, minimum_seconds):
    data = json.loads(Path(path).read_text(encoding='utf-8-sig'))
    if data['Seconds'] < minimum_seconds or data['Completed'] <= 0:
        raise ValueError(f'{path}: insufficient duration/completions')
    histogram = data['Latencies']
    if not histogram or any(row['Ticks'] < 0 or row['Count'] <= 0 for row in histogram):
        raise ValueError(f'{path}: invalid histogram')
    if any(a['Ticks'] >= b['Ticks'] for a, b in zip(histogram, histogram[1:])):
        raise ValueError(f'{path}: histogram is not sorted and unique')
    if sum(row['Count'] for row in histogram) != data['Completed']:
        raise ValueError(f'{path}: histogram lost completed operations')
    total = {}
    previous = data['Start']
    for interval in data['Intervals']:
        if interval['Start'] != previous or interval['End']['Seconds'] <= previous['Seconds']:
            raise ValueError(f'{path}: broken interval boundary')
        completions = interval['End']['Completed'] - previous['Completed']
        if sum(row['Count'] for row in interval['Latencies']) != completions:
            raise ValueError(f'{path}: interval lost completions')
        for row in interval['Latencies']:
            total[row['Ticks']] = total.get(row['Ticks'], 0) + row['Count']
        previous = interval['End']
    if previous != data['End'] or total != {row['Ticks']: row['Count'] for row in histogram}:
        raise ValueError(f'{path}: aggregate differs from interval data')
    if not math.isclose(data['Seconds'], data['End']['Seconds'] - data['Start']['Seconds']):
        raise ValueError(f'{path}: duration does not match snapshots')
    if data['End']['Completed'] - data['Start']['Completed'] != data['Completed']:
        raise ValueError(f'{path}: completion counter differs from total')
    expected_metrics = {
        'CallsPerSecond': data['Completed'] / data['Seconds'],
        'CpuNsPerCall': (data['End']['CpuTicks'] - data['Start']['CpuTicks']) * 100 / data['Completed'],
        'AllocatedBytesPerCall': (data['End']['AllocatedBytes'] - data['Start']['AllocatedBytes']) / data['Completed'],
    }
    for name, expected in expected_metrics.items():
        if not math.isclose(data[name], expected, rel_tol=1e-10, abs_tol=1e-6):
            raise ValueError(f'{path}: {name} differs from counter boundaries')
    for name, fraction in [('P50Ns', .5), ('P99Ns', .99), ('MaxNs', 1)]:
        rank = math.ceil(data['Completed'] * fraction)
        count = 0
        for row in histogram:
            count += row['Count']
            if count >= rank:
                expected = row['Ticks'] * 1e9 / data['StopwatchFrequency']
                if not math.isclose(data[name], expected, rel_tol=1e-10, abs_tol=1e-6):
                    raise ValueError(f'{path}: {name} differs from histogram')
                break
    for name in ['CallsPerSecond', 'CpuNsPerCall', 'AllocatedBytesPerCall', 'P50Ns', 'P99Ns', 'MaxNs']:
        if not math.isfinite(data[name]) or data[name] < 0:
            raise ValueError(f'{path}: invalid metric {name}')
    return data
