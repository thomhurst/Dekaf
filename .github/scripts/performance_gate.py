"""Select benchmark classes and screen A1/B/A2 BenchmarkDotNet exports for the performance gate.

Why the gate is reliable on hosted runners (see .github/workflows/performance-gate.yml):

- One job per benchmark class runs A1, B and A2 back to back on one VM, so the two baseline
  controls are minutes apart instead of hours.
- Medians are compared, not means, so one GC pause or noisy-neighbour iteration cannot decide.
- Tolerances scale with the size of the operation because nanosecond cases fluctuate the most
  between processes: 20% below 100 ns, 15% below 1 us, 10% from 1 us.
- A REGRESSION must hold against both controls and reproduce on an immediate repeat of the
  regressed cases. Drift between the controls and one-sided losses are notes, never verdicts.
- Allocation regressions need at least one managed object (24 B/op) and more than 1% of the
  baseline, so an amortized per-batch object does not fail a 10 KB/op fixture while a 0 B/op
  hot path still fails on its first new allocation.
- When the baseline had to build its own fixture source, cases from fixture files that differ
  between the revisions are reported but not compared: matching names do not prove equal work.
  A class the PR adds has no baseline at all; the candidate is measured alone and reported.
"""
import argparse
import json
import math
import re
import subprocess
import sys
from pathlib import Path


DEFAULT_ITERATIONS = 15
ALLOCATION_FLOOR_BYTES = 24
ALLOCATION_FLOOR_PERCENT = 1.0
TOLERANCE_TIERS = ((100.0, 20.0), (1000.0, 15.0), (math.inf, 10.0))
BENCHMARKS_ROOT = 'tools/Dekaf.Benchmarks/'
UNIT_FIXTURES = BENCHMARKS_ROOT + 'Benchmarks/Unit/'
NON_STEADY_STATE = re.compile(r'IterationSetup|IterationCleanup|InvocationCount|RunStrategy\.(?:ColdStart|Monitoring)')
CLASS_DECLARATION = re.compile(r'^\s*public\s+(?:sealed\s+)?(?:partial\s+)?class\s+(\w+Benchmarks?)\b', re.M)
COMMENT = re.compile(r'//[^\n]*|/\*.*?\*/', re.S)

# Hot-path sentinels for any product or build-input change. Fixture classes the PR adds or edits
# always run as well, so any other component (or a large parameter sweep such as Crc32CBenchmarks)
# is covered by touching its fixture. Keep mapped classes under about 16 expanded cases so a job
# finishes in roughly 25 minutes.
SENTINELS = ('AccumulatorAppendBenchmarks', 'ConsumerHotPathBenchmarks',
             'FetchResponseParsingBenchmarks', 'ProduceResponseParsingBenchmarks')
COMPONENTS = {
    'src/Dekaf/Consumer/KeyOrderedPartitionDispatcher.cs': ('PartitionMessageKeyBenchmarks',),
    'src/Dekaf/Consumer/PartitionMessageKey.cs': ('PartitionMessageKeyBenchmarks',),
    'src/Dekaf/Admin/': ('AdminDetailedMutationBenchmarks',),
    'src/Dekaf/Producer/': ('AccumulatorAdmissionAppendBenchmarks', 'ProducerFireHotPathBenchmarks',
                            'PartitionerBenchmarks', 'InflightTrackingBenchmarks', 'PartitionQueueAccountingBenchmarks'),
    'src/Dekaf/Consumer/': ('FetchRequestBuildBenchmarks', 'OffsetStoreBenchmarks', 'PartitionedDispatchBenchmarks',
                            'KeyOrderedDispatchBenchmarks', 'PartitionedOffsetTrackingBenchmarks',
                            'ConsumerBatchInterceptorBenchmarks', 'ConsumerFollowerOffsetRetryBenchmarks'),
    'src/Dekaf/ShareConsumer/': ('ShareConsumerParsingBenchmarks', 'ShareFetchResponseDecodingBenchmarks',
                                 'ShareAcknowledgementTrackingBenchmarks'),
    'src/Dekaf/Networking/': ('ResponseFrameReaderBenchmarks', 'PendingRequestTableBenchmarks',
                              'PipelinedResponseAllocationBenchmarks'),
    'src/Dekaf/Protocol/': ('ResponseFrameReaderBenchmarks', 'RecordHeaderMaterializationBenchmarks'),
    'src/Dekaf/Serialization/': ('CachingStringDeserializerBenchmarks',),
}


# ----------------------------------------------------------------------------- selection

def classes_in(source):
    """Public benchmark classes in one fixture file with their steady-state flag, comments ignored.

    A class's region runs from its own attribute block to the next class's attribute block."""
    code = COMMENT.sub('', source)
    matches = list(CLASS_DECLARATION.finditer(code))
    starts = [_attribute_block_start(code, match.start()) for match in matches]
    classes = {}
    for index, match in enumerate(matches):
        end = starts[index + 1] if index + 1 < len(matches) else len(code)
        classes[match.group(1)] = NON_STEADY_STATE.search(code, starts[index], end) is None
    return classes


def _attribute_block_start(code, position):
    """Step back over blank and attribute lines that decorate the declaration at position."""
    while position > 0:
        line_start = code.rfind('\n', 0, position - 1) + 1
        line = code[line_start:position].strip()
        if line and not (line.startswith('[') and line.endswith(']')):
            return position
        position = line_start
    return position


def fixture_classes(root):
    """Every public benchmark class under the unit fixtures."""
    classes = {}
    for path in sorted((Path(root) / UNIT_FIXTURES).glob('*.cs')):
        for name, steady in classes_in(path.read_text(encoding='utf-8-sig')).items():
            classes[name] = {'file': path.name, 'steady_state': steady}
    return classes


def select(changed_files, root='.', fixtures=None):
    """Sentinels for any product change, component classes by directory, and every touched fixture class."""
    fixtures = fixture_classes(root) if fixtures is None else fixtures
    wanted = set()
    for path in changed_files:
        path = path.replace('\\', '/')
        if path.startswith(UNIT_FIXTURES) and path.endswith('.cs'):
            file = Path(root) / path
            if file.exists():
                wanted.update(classes_in(file.read_text(encoding='utf-8-sig')))
        elif path.startswith('src/') and path.endswith(('.cs', '.csproj', '.props', '.targets', '.proto')) \
                or path in ('Directory.Packages.props', 'Directory.Build.props', 'Directory.Build.targets', 'global.json'):
            wanted.update(SENTINELS)
            for prefix, classes in COMPONENTS.items():
                if path.startswith(prefix):
                    wanted.update(classes)
    selected, skipped = [], []
    for name in sorted(wanted):
        fixture = fixtures.get(name)
        if fixture is None:
            skipped.append({'name': name, 'reason': 'no such class under tools/Dekaf.Benchmarks/Benchmarks/Unit'})
        elif not fixture['steady_state']:
            skipped.append({'name': name, 'reason': f'{fixture["file"]} uses per-invocation setup or a cold-start '
                                                    'strategy; not steady-state, so it is not gated'})
        else:
            selected.append({'name': name, 'filter': f'*.Unit.{name}.*'})
    return {'applicable': bool(selected), 'classes': selected, 'skipped': skipped}


def explicit_selection(filters):
    classes, names = [], set()
    for index, item in enumerate(filters):
        name = re.sub(r'[^A-Za-z0-9]+', '-', item).strip('-') or 'filter'
        if name in names:
            name = f'{name}-{index + 1}'
        names.add(name)
        classes.append({'name': name, 'filter': item})
    return {'applicable': bool(classes), 'classes': classes, 'skipped': []}


def changed_files(base, head):
    output = subprocess.check_output(['git', 'diff', '--name-only', f'{base}..{head}'], text=True)
    return [path for path in output.splitlines() if path]


def changed_fixture_types(base, head):
    """Benchmark classes whose fixture file differs between the revisions; None when shared
    benchmark infrastructure differs, so no case can be compared."""
    files = [path for path in changed_files(base, head) if path.startswith(BENCHMARKS_ROOT)]
    if any(not path.startswith(UNIT_FIXTURES) for path in files):
        return None
    types = set()
    for path in files:
        for revision in (base, head):
            try:
                source = subprocess.check_output(['git', 'show', f'{revision}:{path}'], text=True,
                                                 stderr=subprocess.DEVNULL)
            except subprocess.CalledProcessError:
                continue
            types.update(classes_in(source))
    return types


# ----------------------------------------------------------------------------- screening

def tolerance_percent(median_ns):
    return next(percent for upper, percent in TOLERANCE_TIERS if median_ns < upper)


def case_key(benchmark):
    parts = [benchmark.get(field) for field in ('Namespace', 'Type', 'Method', 'Parameters')]
    return ' '.join(str(part) for part in parts if part not in (None, ''))


def allocated(benchmark):
    for metric in benchmark.get('Metrics') or []:
        if (metric.get('Descriptor') or {}).get('Id') == 'Allocated Memory':
            return metric.get('Value')
    return None


def load_reports(directory):
    benchmarks = []
    for path in sorted(Path(directory).rglob('*-report-full.json')):
        benchmarks.extend(json.loads(path.read_text(encoding='utf-8-sig')).get('Benchmarks') or [])
    return benchmarks


def phase_table(benchmarks, iterations, phase, may_be_empty=False):
    """Index one phase by case and reject incomplete measurements."""
    cases, fatal = {}, []
    for benchmark in benchmarks:
        key = case_key(benchmark)
        statistics = benchmark.get('Statistics') or {}
        median, samples, bytes_per_op = statistics.get('Median'), statistics.get('N'), allocated(benchmark)
        if key in cases:
            fatal.append(f'{phase}: duplicate case {key}')
        if not (isinstance(median, (int, float)) and math.isfinite(median) and median > 0):
            fatal.append(f'{phase}: {key} has no finite positive median')
        if iterations is not None and samples != iterations:
            fatal.append(f'{phase}: {key} measured {samples} iterations, expected {iterations}')
        if not isinstance(bytes_per_op, (int, float)) or bytes_per_op < 0:
            fatal.append(f'{phase}: {key} has no Allocated Memory metric (MemoryDiagnoser missing or failed)')
        cases[key] = {'median_ns': median, 'n': samples, 'allocated_bytes': bytes_per_op,
                      'full_name': benchmark.get('FullName'), 'type': benchmark.get('Type'),
                      'short': ' '.join(str(benchmark.get(field)) for field in ('Type', 'Method', 'Parameters')
                                        if benchmark.get(field) not in (None, ''))}
    if not cases and not may_be_empty:
        fatal.append(f'{phase}: no benchmark cases were exported (the filter matched nothing on that revision)')
    return cases, fatal


def percent(value, control):
    return (value / control - 1.0) * 100.0


def screen_case(key, a1, b, a2):
    tolerance = tolerance_percent(min(a1['median_ns'], a2['median_ns']))
    b_vs_a1, b_vs_a2 = percent(b['median_ns'], a1['median_ns']), percent(b['median_ns'], a2['median_ns'])
    drift = percent(a2['median_ns'], a1['median_ns'])
    control_alloc = max(a1['allocated_bytes'], a2['allocated_bytes'])
    alloc_delta = b['allocated_bytes'] - control_alloc
    alloc_regression = alloc_delta >= ALLOCATION_FLOOR_BYTES and alloc_delta > control_alloc * ALLOCATION_FLOOR_PERCENT / 100
    slower = (b_vs_a1 > tolerance, b_vs_a2 > tolerance)
    notes = []
    if abs(drift) > tolerance:
        notes.append(f'controls drift {drift:+.1f}% on identical code')
    if any(slower) and not all(slower):
        notes.append('slower than one control only; not a demonstrated loss')
    if alloc_regression:
        notes.append(f'allocates {alloc_delta:+.0f} B/op more than both controls')
    elif alloc_delta >= ALLOCATION_FLOOR_BYTES:
        notes.append(f'allocation {alloc_delta:+.0f} B/op is under {ALLOCATION_FLOOR_PERCENT:g}% of the baseline (amortized)')
    if all(slower) or alloc_regression:
        verdict = 'REGRESSION'
    elif b_vs_a1 < -tolerance and b_vs_a2 < -tolerance:
        verdict = 'IMPROVEMENT'
    else:
        verdict = 'PASS'
    return {'case': key, 'short': b['short'], 'full_name': b['full_name'], 'A1': a1, 'B': b, 'A2': a2,
            'tolerance_percent': tolerance, 'b_vs_a1_percent': b_vs_a1, 'b_vs_a2_percent': b_vs_a2,
            'drift_percent': drift, 'alloc_delta_bytes': alloc_delta, 'screen': verdict, 'notes': notes}


def screen(a1, b, a2, iterations=DEFAULT_ITERATIONS, name=None, uncomparable_types=frozenset(),
           baseline_lacks_filter=False):
    """Screen three phases; raises ValueError when the measurement itself is invalid.

    uncomparable_types: benchmark classes whose fixture source differed between the revisions
    (None means every class). Their cases are reported, never compared.
    baseline_lacks_filter: the baseline built its own fixtures and lists no benchmark for the
    filter (the PR adds the class), so A1 and A2 are empty by design and every candidate case
    is reported, not compared. Any other empty control phase is an invalid measurement."""
    x, fatal = phase_table(a1, iterations, 'A1', may_be_empty=baseline_lacks_filter)
    y, more = phase_table(b, iterations, 'B')
    z, last = phase_table(a2, iterations, 'A2', may_be_empty=baseline_lacks_filter)
    if fatal or more or last:
        raise ValueError('; '.join(fatal + more + last))
    if baseline_lacks_filter:
        if x or z:
            raise ValueError('the baseline lists no benchmark for the filter, yet A1/A2 exported cases')
        unchanged = sorted({case['type'] for case in y.values()} - uncomparable_types) if uncomparable_types is not None else []
        if unchanged:
            raise ValueError('the baseline lists no benchmark for the filter, but the fixture files of '
                             f'{unchanged} are unchanged between the revisions')
    if x.keys() != z.keys():
        raise ValueError('A1 and A2 report different case sets for identical code: '
                         f'A1 only {sorted(x.keys() - z.keys())}, A2 only {sorted(z.keys() - x.keys())}')
    common = sorted(x.keys() & y.keys())
    uncomparable = [key for key in common if uncomparable_types is None or y[key]['type'] in uncomparable_types]
    cases = [screen_case(key, x[key], y[key], z[key]) for key in common if key not in uncomparable]
    fixture_changed = [{'case': key, 'short': y[key]['short'], 'A1': x[key], 'B': y[key], 'A2': z[key]}
                       for key in uncomparable]
    candidate_only = [{'case': key, **y[key]} for key in sorted(y.keys() - x.keys())]
    if not cases and not candidate_only and not fixture_changed:
        raise ValueError('no common or candidate-only cases were measured')
    return {'name': name, 'round': 1, 'iterations': iterations, 'cases': cases, 'candidate_only': candidate_only,
            'fixture_changed': fixture_changed, 'baseline_only': sorted(x.keys() - y.keys()),
            'baseline_lacks_filter': baseline_lacks_filter, 'screen': overall(cases)}


def overall(cases):
    if any(case['screen'] == 'REGRESSION' for case in cases):
        return 'REGRESSION'
    return 'PASS' if cases else 'NOT COMPARED'


def confirm(previous, repeat):
    """Keep a first-round REGRESSION only when the repeat of that case regressed as well."""
    repeated = {case['case']: case for case in repeat['cases']}
    cases = []
    for first in previous['cases']:
        again = repeated.get(first['case'])
        if first['screen'] != 'REGRESSION':
            cases.append(first)
            continue
        if again is None:
            raise ValueError(f'the repeat did not measure the regressed case {first["case"]}')
        summary = (f'first {first["b_vs_a1_percent"]:+.1f}%/{first["b_vs_a2_percent"]:+.1f}%, '
                   f'repeat {again["b_vs_a1_percent"]:+.1f}%/{again["b_vs_a2_percent"]:+.1f}%')
        merged = dict(again, first_round=first)
        if again['screen'] == 'REGRESSION':
            merged['notes'] = again['notes'] + [f'reproduced on repeat ({summary})']
        else:
            merged.update(screen='PASS', notes=again['notes'] + [f'not reproduced on repeat ({summary})'])
        cases.append(merged)
    return dict(previous, cases=cases, round=2, screen=overall(cases))


def regressed_filters(comparison):
    return [case['full_name'] for case in comparison['cases'] if case['screen'] == 'REGRESSION']


def _ns(value):
    if value >= 1_000_000:
        return f'{value / 1_000_000:,.2f} ms'
    if value >= 1_000:
        return f'{value / 1_000:,.2f} us'
    return f'{value:,.1f} ns'


def markdown(comparison):
    rows = ['| Case | A1 | B | A2 | B vs A1 | B vs A2 | A1 to A2 | Tolerance | Allocated B/op A1 / B / A2 | Screen | Notes |',
            '| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- |']
    for case in comparison['cases']:
        rows.append('| {short} | {a1} | {b} | {a2} | {d1:+.1f}% | {d2:+.1f}% | {drift:+.1f}% | {tol:g}% | {alloc} | {screen} | {notes} |'.format(
            short=case['short'].replace('|', '/'), a1=_ns(case['A1']['median_ns']), b=_ns(case['B']['median_ns']),
            a2=_ns(case['A2']['median_ns']), d1=case['b_vs_a1_percent'], d2=case['b_vs_a2_percent'],
            drift=case['drift_percent'], tol=case['tolerance_percent'],
            alloc=' / '.join(f'{case[phase]["allocated_bytes"]:,.0f}' for phase in ('A1', 'B', 'A2')),
            screen=case['screen'], notes='; '.join(case['notes'])))
    for item in comparison.get('fixture_changed', []):
        rows.append('| {short} | {a1} | {b} | {a2} | | | | | {alloc} | NOT COMPARED | fixture source differs between the revisions |'.format(
            short=item['short'].replace('|', '/'), a1=_ns(item['A1']['median_ns']), b=_ns(item['B']['median_ns']),
            a2=_ns(item['A2']['median_ns']),
            alloc=' / '.join(f'{item[phase]["allocated_bytes"]:,.0f}' for phase in ('A1', 'B', 'A2'))))
    if comparison['candidate_only']:
        why = ' (the baseline has no benchmark for this filter; the PR adds the class)' if comparison.get('baseline_lacks_filter') else ''
        rows += ['', f'Candidate-only cases, not compared{why}: ' + ', '.join(
            f'{item["short"]} {_ns(item["median_ns"])}, {item["allocated_bytes"]:,.0f} B/op' for item in comparison['candidate_only'])]
    if comparison['baseline_only']:
        rows += ['', 'Baseline-only cases (not compared): ' + ', '.join(item.split(' ', 1)[1] for item in comparison['baseline_only'])]
    repeat = ' after repeating the regressed cases' if comparison.get('round') == 2 else ''
    rows += ['', f'Screen: **{comparison["screen"]}**{repeat}. Medians of {comparison["iterations"]} iterations per phase. '
                 'REGRESSION: slower than both controls beyond the tolerance for that case size, or at least '
                 f'{ALLOCATION_FLOOR_BYTES} B/op and {ALLOCATION_FLOOR_PERCENT:g}% more allocation than both controls, '
                 'reproduced on an immediate repeat. Control drift and one-sided differences are notes.']
    return '\n'.join(rows)


# ----------------------------------------------------------------------------- entry point

def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest='command', required=True)
    selecting = commands.add_parser('select', help='Select benchmark classes for the changes between two commits')
    selecting.add_argument('--base', required=True)
    selecting.add_argument('--head', required=True)
    selecting.add_argument('--filters', default='', help='Explicit space-separated BDN globs that replace path selection')
    selecting.add_argument('--root', type=Path, default=Path('.'), help='Checkout of the candidate commit')
    selecting.add_argument('--output', type=Path, required=True)
    selecting.add_argument('--github-output', type=Path, help='Append applicable= and matrix= for the workflow')
    screening = commands.add_parser('screen', help='Screen A1/B/A2 exports; exit 1 on REGRESSION, 2 on an invalid measurement')
    screening.add_argument('--a1', type=Path, required=True)
    screening.add_argument('--b', type=Path, required=True)
    screening.add_argument('--a2', type=Path, required=True)
    screening.add_argument('--iterations', type=int, default=DEFAULT_ITERATIONS)
    screening.add_argument('--name')
    screening.add_argument('--previous', type=Path, help='First-round comparison that this repeat confirms')
    screening.add_argument('--fixture-diff', nargs=2, metavar=('BASE', 'HEAD'),
                           help='The baseline ran its own fixture source; do not compare classes whose fixture differs')
    screening.add_argument('--baseline-lacks-filter', action='store_true',
                           help='The baseline lists no benchmark for the filter (the PR adds the class); '
                                'A1 and A2 are empty and the candidate is reported, not compared')
    screening.add_argument('--output', type=Path, required=True)
    screening.add_argument('--markdown', type=Path)
    screening.add_argument('--regressed', type=Path, help='Write one BenchmarkDotNet filter per regressed case')
    args = parser.parse_args(argv)

    if args.command == 'select':
        selection = explicit_selection(args.filters.split()) if args.filters.split() \
            else select(changed_files(args.base, args.head), args.root)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(selection, indent=2) + '\n', encoding='utf-8')
        if args.github_output:
            with args.github_output.open('a', encoding='utf-8') as handle:
                handle.write(f'applicable={"true" if selection["applicable"] else "false"}\n')
                handle.write(f'matrix={json.dumps({"include": selection["classes"]})}\n')
        print(json.dumps(selection, indent=2))
        return 0

    try:
        uncomparable = changed_fixture_types(*args.fixture_diff) if args.fixture_diff else frozenset()
        comparison = screen(load_reports(args.a1), load_reports(args.b), load_reports(args.a2), args.iterations,
                            args.name, uncomparable, args.baseline_lacks_filter)
        if args.previous:
            comparison = confirm(json.loads(args.previous.read_text(encoding='utf-8')), comparison)
    except ValueError as error:
        print(f'::error::Invalid measurement: {error}', file=sys.stderr)
        return 2
    args.output.write_text(json.dumps(comparison, indent=2) + '\n', encoding='utf-8')
    if args.markdown:
        args.markdown.write_text(markdown(comparison) + '\n', encoding='utf-8')
    if args.regressed:
        args.regressed.write_text(''.join(f'{item}\n' for item in regressed_filters(comparison)), encoding='utf-8')
    if comparison['fixture_changed']:
        print(f'::warning::{args.name or "screen"}: {len(comparison["fixture_changed"])} case(s) not compared because '
              'their fixture source differs between the revisions; see the table.')
    if comparison['baseline_lacks_filter']:
        print(f'::warning::{args.name or "screen"}: {len(comparison["candidate_only"])} candidate-only case(s) not compared '
              'because the baseline has no benchmark for this filter; run the class locally against main if that comparison matters.')
    print(f'{args.name or "screen"}: {len(comparison["cases"])} compared cases, {comparison["screen"]}')
    return 1 if comparison['screen'] == 'REGRESSION' else 0


if __name__ == '__main__':
    raise SystemExit(main())
