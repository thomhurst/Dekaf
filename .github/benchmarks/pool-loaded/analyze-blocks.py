"""Validate retained block distributions; this does not decide performance acceptance."""
import argparse
import hashlib
import json
import math
from pathlib import Path


def integer(value, minimum=0):
    if type(value) is not int or value < minimum:
        raise ValueError(f'Expected integer >= {minimum}: {value!r}')
    return value


def finite(value, minimum=0):
    if type(value) not in (int, float) or not math.isfinite(value) or value < minimum:
        raise ValueError(f'Expected finite number >= {minimum}: {value!r}')
    return value


def percentile(buckets, count, percent):
    if count == 0:
        return None
    rank = max(1, math.floor(count * percent / 100.0))
    cumulative = 0
    for index, frequency in sorted(buckets.items()):
        cumulative += frequency
        if cumulative >= rank:
            return dict(rank=rank, lowerUs=index * 10, upperExclusiveUs=(index + 1) * 10,
                        midpointUs=(index + 0.5) * 10)
    raise ValueError('Percentile rank exceeds retained observations')


def analyze(data):
    if data['Failure'] is not None or data['IntervalsStableAfterDrain'] is not True or data['IntervalCaptureComplete'] is not True:
        raise ValueError('Undrained, failed, or incomplete collection')
    completed = integer(data['Consumed'], 1)
    if integer(data['Sent'], 1) != completed or integer(data['Acknowledged'], 1) != completed:
        raise ValueError('Completion counts disagree')
    configured = integer(data['ConfiguredSeconds'], 1)
    elapsed = finite(data['Seconds'])
    if not elapsed >= finite(data['OfferedSeconds']) >= configured:
        raise ValueError('Invalid phase duration')
    allocated = integer(data['AllocatedBytes'])
    cpu = finite(data['CpuMs'])
    allocation_start, allocation_end = (integer(data[name]) for name in ('AllocatedBytesStart', 'AllocatedBytesEnd'))
    cpu_start, cpu_end = (finite(data[name]) for name in ('CpuMillisecondsStart', 'CpuMillisecondsEnd'))
    if allocation_end < allocation_start or allocated != allocation_end - allocation_start:
        raise ValueError('Invalid allocation accounting boundaries')
    if cpu_end < cpu_start or not math.isclose(cpu, cpu_end - cpu_start, rel_tol=1e-12):
        raise ValueError('Invalid CPU accounting boundaries')
    for key, expected in [('BytesPerCompleted', allocated / completed),
                          ('CpuUsPerCompleted', cpu * 1000 / completed),
                          ('CompletedPerSecond', completed / elapsed)]:
        if not math.isclose(finite(data[key]), expected, rel_tol=1e-12):
            raise ValueError('Invalid protected metric denominator: ' + key)
    result = dict(completed=completed, seconds=elapsed, configuredSeconds=configured,
                  offeredSeconds=data['OfferedSeconds'], distributions={})
    for boundary in ['Delivery', 'Completion']:
        raw = data[boundary + 'Blocks']
        intervals = data[boundary + 'Intervals']
        global_latency = data[boundary + 'Latency']
        capacity = configured + 31
        for key, expected in [('BlockSeconds', 10), ('BucketWidthUs', 10), ('BucketCount', 500_000),
                              ('CapacitySeconds', capacity), ('OutsideCapacityCount', 0)]:
            if integer(raw[key]) != expected:
                raise ValueError('Unexpected histogram configuration or capacity overflow: ' + key)
        if integer(intervals['IntervalSeconds']) != 1 or len(intervals['Intervals']) != capacity:
            raise ValueError('Unexpected interval configuration')
        frequency = integer(intervals['TicksPerSecond'], 1)
        if any(integer(intervals['OutsideCapacity'][key]) != 0 for key in ['Count', 'MinTicks', 'MaxTicks']):
            raise ValueError('Interval capacity overflow')
        if integer(global_latency['Count'], 1) != completed or integer(global_latency['OverflowCount']) != 0:
            raise ValueError('Global histogram count mismatch or latency overflow')
        observations = intervals['Intervals']
        for second, observation in enumerate(observations):
            count = integer(observation['Count'])
            minimum = integer(observation['MinTicks'])
            maximum = integer(observation['MaxTicks'])
            if minimum > maximum or (count == 0 and (minimum != 0 or maximum != 0)):
                raise ValueError('Invalid exact extrema')
            if count and second > math.floor(elapsed):
                raise ValueError('Observation after phase ended')
        if sum(item['Count'] for item in observations) != completed:
            raise ValueError('Interval count mismatch')
        minimum = min(item['MinTicks'] for item in observations if item['Count'])
        maximum = max(item['MaxTicks'] for item in observations)
        if minimum * 1_000_000.0 / frequency != finite(global_latency['MinUs']):
            raise ValueError('Global minimum mismatch')
        if maximum * 1_000_000.0 / frequency != finite(global_latency['MaxUs']):
            raise ValueError('Global maximum mismatch')
        blocks = raw['Blocks']
        if len(blocks) != (capacity - 1) // 10 + 1:
            raise ValueError('Missing or extra time block')
        aggregate = {}
        summaries = []
        for number, block in enumerate(blocks):
            start = number * 10
            if integer(block['StartSecond']) != start or integer(block['LatencyOverflowCount']) != 0:
                raise ValueError('Incorrect time block or latency overflow')
            buckets = {}
            previous = -1
            for bucket in block['Buckets']:
                index = integer(bucket['Index'])
                count = integer(bucket['Count'], 1)
                if index <= previous or index >= 500_000:
                    raise ValueError('Unordered, repeated, or out-of-range latency bucket')
                buckets[index] = count
                aggregate[index] = aggregate.get(index, 0) + count
                previous = index
            count = sum(buckets.values())
            exact = observations[start:min(start + 10, capacity)]
            if count != integer(block['Count']) or count != sum(item['Count'] for item in exact):
                raise ValueError('Block count differs from buckets or completion intervals')
            if count:
                minimum = min(item['MinTicks'] for item in exact if item['Count']) * 1_000_000.0 / frequency
                maximum = max(item['MaxTicks'] for item in exact) * 1_000_000.0 / frequency
                if not min(buckets) * 10 <= minimum < (min(buckets) + 1) * 10:
                    raise ValueError('Block minimum outside its first occupied bucket')
                if not max(buckets) * 10 <= maximum < (max(buckets) + 1) * 10:
                    raise ValueError('Block maximum outside its last occupied bucket')
            summaries.append(dict(startSecond=start, observedSeconds=max(0, min(start + 10, elapsed) - start),
                                  count=count, p50=percentile(buckets, count, 50), p99=percentile(buckets, count, 99),
                                  maxTicks=max((item['MaxTicks'] for item in exact), default=0)))
        if sum(aggregate.values()) != completed:
            raise ValueError('Aggregate count mismatch')
        for percent in [50, 95, 99]:
            if percentile(aggregate, completed, percent)['midpointUs'] != finite(global_latency[f'P{percent}Us']):
                raise ValueError('Reconstructed global percentile mismatch')
        result['distributions'][boundary] = summaries
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('inputs', type=Path, nargs='+')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = dict(status='INVALID_COLLECTION', scope=__doc__, results=[])
    try:
        for path in args.inputs:
            raw = path.read_bytes()
            report['results'].append(dict(path=str(path.resolve()), sha256=hashlib.sha256(raw).hexdigest(),
                                          analysis=analyze(json.loads(raw))))
        report['status'] = 'COLLECTION_VALIDATED'
    except (OSError, KeyError, TypeError, ValueError, OverflowError) as error:
        report['error'] = str(error)
    serialized = json.dumps(report, indent=2, allow_nan=False) + '\n'
    with args.output.open('x', encoding='utf-8') as output:
        output.write(serialized)
    print(report['status'])
    return 0 if report['status'] == 'COLLECTION_VALIDATED' else 1


if __name__ == '__main__':
    raise SystemExit(main())
