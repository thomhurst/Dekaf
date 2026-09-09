"""Compare retained code and counters; code differences are not causal CPU proof."""
import hashlib
import json
from pathlib import Path
import re
import sys

root = Path(sys.argv[1])
rows = []
for phase in ('A1', 'B', 'A2'):
    folder = root / phase / (sys.argv[2] if len(sys.argv) > 2 else '')
    measured = json.loads((folder / 'measured.json').read_text())
    compilation = json.loads((folder / 'compilations.json').read_text())
    boundaries = {row['Name']: row['Timestamp'] for row in compilation['phases']}
    events = [event for event in compilation['events'] if boundaries['measured'] <= event['Timestamp'] <= boundaries['finalize']]
    methods = {}
    for block in re.split(r'(?=; Assembly listing for method )', (folder / 'jit.asm').read_text()):
        lines = block.splitlines()
        if not lines or not lines[0].startswith('; Assembly listing') or '(Tier1' not in lines[0]:
            continue
        # JitDisasmDiffable removes absolute addresses. Strip comments and blank lines
        # only, retaining labels, instructions, operands and branch destinations.
        instructions = '\n'.join(line.strip() for line in lines if line.strip() and not line.lstrip().startswith(';'))
        sizes = re.findall(r'; Total bytes of code (\d+)', block)
        methods[lines[0]] = dict(bytes=int(sizes[-1]) if sizes else None,
            instruction_sha256=hashlib.sha256(instructions.encode()).hexdigest())
    blocks = []
    for offset in range(0, len(measured['Intervals']), 30):
        interval = measured['Intervals'][offset:offset + 30]
        start, end = interval[0]['Start'], interval[-1]['End']
        blocks.append(dict(start_seconds=start['Seconds'], end_seconds=end['Seconds'],
            cpu_ns_per_call=(end['CpuTicks'] - start['CpuTicks']) * 100 / (end['Completed'] - start['Completed'])))
    rows.append(dict(phase=phase, metrics={k: measured[k] for k in ['CallsPerSecond', 'CpuNsPerCall', 'AllocatedBytesPerCall', 'P50Ns', 'P99Ns', 'MaxNs']},
        gc=[measured['End'][k] - measured['Start'][k] for k in ['Gen0','Gen1','Gen2']],
        jit_overflow=compilation['overflow'], measured_jit=[dict(type=e['Payload'][4], method=e['Payload'][5]) for e in events],
        cpu_blocks=blocks, methods=methods))
shared = set.intersection(*(set(row['methods']) for row in rows))
changed = [method for method in sorted(shared) if len({row['methods'][method]['instruction_sha256'] for row in rows}) > 1]
result = dict(scope='Diagnostic only; code identity does not prove identical host costs and code differences do not prove CPU causality.',
    rows=rows, shared_tier1_methods=len(shared), changed_instruction_bodies=changed)
(root / 'review-code-and-cpu.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print('Shared Tier1 methods',len(shared),'differing instruction bodies',len(changed))
for row in rows:
    print(row['phase'],json.dumps(row['metrics']), 'GC',row['gc'])
for method in changed:
    if 'AdminClient+<DeleteTopicsAsync>' in method:
        print(method, [row['methods'][method]['bytes'] for row in rows])
