"""Summarize retained host counters without changing client sample inclusion."""
import json
from pathlib import Path
import sys

root = Path(sys.argv[1])
resource = root / 'runner-resources'
evidence = root / 'evidence'
plan = json.loads((evidence / 'plan.json').read_text())
records = [json.loads(line) for line in (resource / 'series.jsonl').read_text().splitlines()]
assert json.loads((resource / 'completion.json').read_text())['exit_code'] == 0
assert all(b['monotonic_ns'] > a['monotonic_ns'] for a, b in zip(records, records[1:]))

def cpu(row, name):
    return next(list(map(int, line.split()[1:])) for line in row['stat'].splitlines() if line.split()[0] == name)

def key_value(text):
    return {line.split()[0].rstrip(':'): int(line.split()[1]) for line in text.splitlines()}

def pressure(row, name, category):
    line = next((line for line in row['pressure/' + name].splitlines() if line.startswith(category + ' ')), None)
    return int(line.rsplit('total=', 1)[1]) if line else None

summaries = []
for phase in ['A1', 'B', 'A2', 'candidate-only']:
    for case in plan['candidate_only'] if phase == 'candidate-only' else plan['controls']:
        folder = evidence / phase / case.replace(':', '-')
        log = json.loads((folder / 'compilations.json').read_text())
        boundaries = {row['Name']: row['Timestamp'] * 1e9 / log['frequency'] for row in log['phases']}
        measured = [row for row in records if boundaries['measured'] <= row['monotonic_ns'] <= boundaries['finalize']]
        assert len(measured) >= 2, (phase, case, boundaries)
        first, last = measured[0], measured[-1]
        seconds = (last['monotonic_ns'] - first['monotonic_ns']) / 1e9
        cpu_rows = {}
        for name in ['cpu'] + ['cpu' + str(value) for value in plan['cpu_affinity']]:
            a, b = cpu(first, name), cpu(last, name)
            changes = [y - x for x, y in zip(a, b)]
            total = sum(changes[:8])
            cpu_rows[name] = dict(busy_fraction=1 - (changes[3] + changes[4]) / total,
                                 steal_fraction=changes[7] / total)
        memory = [key_value(row['meminfo']) for row in measured]
        vm_first, vm_last = key_value(first['vmstat']), key_value(last['vmstat'])
        summary = dict(phase=phase, case=case, samples=len(measured), observed_seconds=seconds,
            max_sample_gap_seconds=max((b['monotonic_ns']-a['monotonic_ns'])/1e9 for a,b in zip(measured,measured[1:])),
            cpu=cpu_rows, min_memory_available_bytes=min(row['MemAvailable'] for row in memory)*1024,
            min_swap_free_bytes=min(row['SwapFree'] for row in memory)*1024,
            swap_pages_in=vm_last['pswpin']-vm_first['pswpin'], swap_pages_out=vm_last['pswpout']-vm_first['pswpout'],
            pressure_fractions={name: {kind: (pressure(last,name,kind)-pressure(first,name,kind))/(seconds*1e6)
                for kind in ['some','full'] if pressure(first,name,kind) is not None} for name in ['cpu','memory','io']})
        summaries.append(summary)
        print(json.dumps(summary))
(evidence / 'runner-resource-review.json').write_text(json.dumps(summaries, indent=2), encoding='utf-8')
