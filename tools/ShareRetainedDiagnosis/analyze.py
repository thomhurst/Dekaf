"""Read-only normalized instruction comparison; no performance acceptance inference."""
import difflib
import json
from pathlib import Path
import re

root = Path.cwd()
work = root / '.artifacts/retained-codegen'
layouts = {phase: json.loads((work / f'layout-{phase}.json').read_text()) for phase in ('A1', 'B', 'A2')}
normalized = {}
methods = {}
for phase in layouts:
    raw = (work / f'disasm-{phase}.log').read_text()
    blocks = re.split(r'(?=; Assembly listing for method )', raw)
    methods[phase] = []
    output = []
    for block in blocks:
        if not block.strip():
            continue
        header = block.splitlines()[0]
        if 'Traverse' not in header:
            raise ValueError(f'Unexpected disassembly: {header}')
        sizes = re.findall(r'; Total bytes of code (\d+)', block)
        if len(sizes) != 1:
            raise ValueError(f'Missing code size: {phase} {header}')
        methods[phase].append(dict(method=header, bytes=int(sizes[0])))
        output.append(header)
        for line in block.splitlines()[1:]:
            line = line.strip()
            if not line or line.startswith(';'):
                continue
            # Only long hexadecimal addresses are normalized. Opcodes, labels, field
            # offsets, short immediates and symbolic call identities remain intact.
            output.append(re.sub(r'0x[0-9A-Fa-f]{10,16}\b', '<address>', line))
    if not methods[phase]:
        raise ValueError('No traversal method was captured')
    normalized[phase] = '\n'.join(output) + '\n'
    (work / f'normalized-{phase}.txt').write_text(normalized[phase], encoding='utf-8')
comparisons = {}
for left, right in [('A1', 'B'), ('B', 'A2'), ('A1', 'A2')]:
    diff = ''.join(difflib.unified_diff(normalized[left].splitlines(True), normalized[right].splitlines(True), fromfile=left, tofile=right))
    (work / f'normalized-{left}-{right}.diff').write_text(diff, encoding='utf-8')
    comparisons[left + '-' + right] = dict(instructions_equal=not diff, fields_equal=layouts[left]['Fields'] == layouts[right]['Fields'], checksum_equal=layouts[left]['Checksum'] == layouts[right]['Checksum'])
result = dict(methods=methods, comparisons=comparisons, layout={phase:{k:v for k,v in layout.items() if k!='RelativeAddresses'} for phase,layout in layouts.items()}, normalization='Long hexadecimal addresses only; comments omitted except method identity. Raw outputs retained. No timing equivalence inferred.')
(work / 'analysis.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
