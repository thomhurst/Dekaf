import json
import sys
from pathlib import Path

directory = Path(sys.argv[1])
reports = list((directory / 'results').glob('*-report-full-compressed.json'))
if not reports:
    reports = list((directory / 'results').glob('*-report-full.json'))
if not reports:
    raise SystemExit(f'No full BDN JSON report in {directory}')
count = 0
for path in reports:
    for case in json.loads(path.read_text(encoding='utf-8-sig'))['Benchmarks']:
        statistics = case.get('Statistics')
        if not statistics or statistics.get('N', 0) < 3:
            raise SystemExit(f'No valid measurements for {case.get("FullName")}')
        count += 1
if count == 0:
    raise SystemExit(f'No measured benchmark cases in {directory}')
print(f'Validated {count} benchmark cases in {directory.name}')
