"""Task-scoped outbox comparison including renewal stores; never promotes the PR performance gate."""
import argparse
import csv
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess

MODES = ('sync-off', 'pending-off', 'sync-on', 'pending-on',
         'renewal-sync-off', 'renewal-pending-off', 'renewal-sync-on', 'renewal-pending-on')
PHASES = (('A1', 'A'), ('B', 'B'), ('A2', 'A'))


def now():
    return datetime.now(timezone.utc).isoformat()


def run(arguments, log, **kwargs):
    with Path(log).open('w', encoding='utf-8') as stream:
        subprocess.run(arguments, stdout=stream, stderr=subprocess.STDOUT, check=True, **kwargs)


def output(arguments):
    return subprocess.check_output(arguments, text=True).strip()


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def inventory(folder):
    return {str(file.relative_to(folder)): digest(file)
            for file in sorted(folder.rglob('*')) if file.is_file()}


def read_case(folder, log):
    cases = []
    for report in (folder / 'results').glob('*-report-full.json'):
        cases.extend(json.loads(report.read_text(encoding='utf-8-sig')).get('Benchmarks', []))
    if len(cases) != 1:
        raise ValueError(f'Expected exactly one case in {folder}, got {len(cases)}')
    case = cases[0]
    statistics = case.get('Statistics') or {}
    if statistics.get('N') != 25 or not math.isfinite(statistics.get('Mean', math.nan)):
        raise ValueError(f'Expected 25 valid measured samples: {folder}')
    allocated = next((metric['Value'] for metric in case.get('Metrics', [])
                      if metric['Descriptor']['Id'] == 'Allocated Memory'), None)
    if allocated is None or not math.isfinite(allocated) or allocated < 0:
        raise ValueError(f'Missing MemoryDiagnoser result: {folder}')
    text = log.read_text(encoding='utf-8-sig')
    warmups = re.findall(r'^WorkloadWarmup\s+\d+: (\d+) op, ([\d.]+) ns,', text, re.M)
    elapsed = sum(float(ns) for _, ns in warmups) / 1e9
    if len(warmups) != 30 or elapsed < 20:
        raise ValueError(f'Insufficient elapsed workload warmup: {folder}: {elapsed}s/{len(warmups)} iterations')
    raw = re.findall(r'^RAW cycles=1000 bytes=(\d+)', text, re.M)
    if len(raw) != 1:
        raise ValueError(f'Missing exact allocation probe: {folder}')
    with (folder / 'runtime.csv').open(newline='') as stream:
        runtime = list(csv.DictReader(stream))
    actual = [row for row in runtime if row['workload'].startswith('WorkloadActual')]
    warm = [row for row in runtime if row['workload'].startswith('WorkloadWarmup')]
    if len(actual) != 25 or len(warm) != 30:
        raise ValueError(f'Runtime series does not cover every workload interval: {folder}')
    return {'mean_ns': statistics['Mean'], 'allocated_bytes': allocated,
            'statistics': statistics, 'raw_1000_cycle_bytes': int(raw[0]),
            'warmup_seconds': elapsed, 'warmup_cycles': sum(int(ops) for ops, _ in warmups),
            'measured_jit_methods': int(actual[-1]['jit_methods']) - int(warm[-1]['jit_methods']),
            'measured_jit_ms': float(actual[-1]['jit_ms']) - float(warm[-1]['jit_ms']),
            'runtime_samples': len(runtime)}


def comparison(phases):
    if set(phases) != {'A1', 'B', 'A2'} or any(set(value) != set(MODES) for value in phases.values()):
        raise ValueError('Incomplete phase/case matrix')
    rows = []
    for mode in MODES:
        a1, b, a2 = (phases[phase][mode] for phase in ('A1', 'B', 'A2'))
        rows.append({'mode': mode, 'A1': a1, 'B': b, 'A2': a2,
                     'candidate_vs_A1_percent': (b['mean_ns'] / a1['mean_ns'] - 1) * 100,
                     'candidate_vs_A2_percent': (b['mean_ns'] / a2['mean_ns'] - 1) * 100,
                     'control_drift_percent': (a2['mean_ns'] / a1['mean_ns'] - 1) * 100})
    return rows


def execute(args):
    repository = Path.cwd()
    artifacts = Path(args.artifacts).resolve()
    artifacts.mkdir(parents=True, exist_ok=False)
    for sha in (args.baseline, args.candidate):
        if not re.fullmatch('[0-9a-f]{40}', sha) or output(['git', 'rev-parse', f'{sha}^{{commit}}']) != sha:
            raise ValueError('Exact lowercase commit SHAs required')
    subprocess.run(['git', 'merge-base', '--is-ancestor', args.baseline, args.candidate], check=True)
    remote_main = output(['git', 'ls-remote', 'origin', 'refs/heads/main']).split()[0]
    if remote_main != args.baseline:
        raise ValueError('Main moved before this new campaign; repin/rebase before measuring')
    remote_head = output(['git', 'ls-remote', 'origin', 'refs/heads/issue-3041-outbox-metrics']).split()[0]
    if remote_head != args.candidate:
        raise ValueError('The PR head changed before this campaign')
    workspace = Path(os.environ['RUNNER_TEMP']) / f'outbox-cycle-{os.environ["GITHUB_RUN_ID"]}'
    workspace.mkdir(exist_ok=False)
    cpu = min(os.sched_getaffinity(0))
    environment = dict(os.environ, DOTNET_TieredCompilation='0', ABA_CPU=str(cpu), MSBUILDDISABLENODEREUSE='1')
    provenance = {'baseline_sha': args.baseline, 'candidate_sha': args.candidate,
                  'harness_sha': output(['git', 'rev-parse', 'HEAD']), 'started_utc': now(),
                  'run_url': f"https://github.com/{os.environ['GITHUB_REPOSITORY']}/actions/runs/{os.environ['GITHUB_RUN_ID']}",
                  'runner': 'ubuntu-latest', 'image_os': os.environ.get('ImageOS'),
                  'image_version': os.environ.get('ImageVersion'), 'cpu_affinity': cpu,
                  'tiered_compilation': False, 'phase_order': ['A1', 'B', 'A2'],
                  'case_order': list(MODES), 'warmup_iterations': 30, 'iteration_target_ms': 1000,
                  'measured_iterations': 25, 'outliers': 'DontRemove',
                  'scope': 'Four fixture-inclusive relay cycle microbenchmarks; full performance acceptance remains INCONCLUSIVE.',
                  'phases': []}
    provenance_path = artifacts / 'provenance.json'
    provenance_path.write_text(json.dumps(provenance, indent=2))
    for name, command in {'cpu': ['lscpu'], 'runtime': ['dotnet', '--info'], 'os': ['uname', '-a']}.items():
        run(command, artifacts / f'{name}.txt')
    fixtures = repository / '.github/benchmarks/outbox-cycle-aba'
    shutil.copytree(fixtures, artifacts / 'fixture-sources', ignore=shutil.ignore_patterns('bin', 'obj'))
    hosts = {}
    manifests = {}
    for label, sha in (('A', args.baseline), ('B', args.candidate)):
        product = workspace / f'product-{label}'
        run(['git', 'worktree', 'add', '--detach', str(product), sha], artifacts / f'checkout-{label}.log')
        fixture = workspace / f'fixture-{label}'
        shutil.copytree(fixtures, fixture, ignore=shutil.ignore_patterns('bin', 'obj'))
        for name in ('global.json', 'Directory.Packages.props'):
            shutil.copyfile(repository / name, fixture / name)
        run(['dotnet', 'build', str(fixture / 'Harness.csproj'), '-c', 'Release', '-m:1', '-nr:false',
             f'-p:ProductProject={product}/src/Dekaf.Outbox/Dekaf.Outbox.csproj', f'-p:FixtureSource={fixture}'],
            artifacts / f'build-{label}.log', cwd=fixture, env=environment)
        hosts[label] = fixture / 'bin/Release/net10.0/Harness.dll'
        manifests[label] = inventory(hosts[label].parent)
        shutil.copytree(hosts[label].parent, artifacts / f'loaded-binaries-{label}')
        if inventory(artifacts / f'loaded-binaries-{label}') != manifests[label]:
            raise ValueError('Loaded binary archive differs from measurement inputs')
        (artifacts / f'loaded-binaries-{label}.json').write_text(json.dumps(manifests[label], indent=2))
    # This dedicated hosted VM belongs solely to the experiment. Both products
    # and hosts are already built; InProcessEmit launches no further build.
    run(['dotnet', 'build-server', 'shutdown'], artifacts / 'build-server-shutdown.log', env=environment)
    for label in ('A', 'B'):
        run(['taskset', '-c', str(cpu), 'dotnet', str(hosts[label]), 'validate'],
            artifacts / f'validate-{label}.log', cwd=hosts[label].parent, env=environment)
    phases = {}
    for phase, label in PHASES:
        if inventory(hosts[label].parent) != manifests[label]:
            raise ValueError('Prebuilt measurement inputs changed')
        run(['ps', '-eo', 'pid,ppid,pcpu,pmem,args'], artifacts / f'processes-{phase}.txt')
        entry = {'phase': phase, 'product_sha': args.baseline if label == 'A' else args.candidate,
                 'started_utc': now(), 'cases': []}
        phases[phase] = {}
        for mode in MODES:
            folder = artifacts / phase / mode
            log = artifacts / f'{phase}-{mode}.log'
            print(f'{phase} {mode}: starting fresh process', flush=True)
            run(['taskset', '-c', str(cpu), 'dotnet', str(hosts[label]), mode, str(folder)],
                log, cwd=hosts[label].parent, env=environment)
            phases[phase][mode] = read_case(folder, log)
            entry['cases'].append(mode)
        entry['completed_utc'] = now()
        provenance['phases'].append(entry)
        provenance_path.write_text(json.dumps(provenance, indent=2))
    rows = comparison(phases)
    (artifacts / 'comparison.json').write_text(json.dumps({'performance_acceptance': 'INCONCLUSIVE', 'cases': rows}, indent=2))
    lines = ['# Outbox cycle A1/B/A2 diagnostic', '',
             'Complete microbenchmark evidence does not establish whole-PR performance acceptance.', '',
             '| Mode | A1 ns | B ns | A2 ns | B/A1 | B/A2 | Control drift | Allocated B/cycle A1 / B / A2 |',
             '|---|---:|---:|---:|---:|---:|---:|---:|']
    for row in rows:
        a1, b, a2 = (row[phase] for phase in ('A1', 'B', 'A2'))
        lines.append(f"| {row['mode']} | {a1['mean_ns']:.3f} | {b['mean_ns']:.3f} | {a2['mean_ns']:.3f} | "
                     f"{row['candidate_vs_A1_percent']:+.2f}% | {row['candidate_vs_A2_percent']:+.2f}% | "
                     f"{row['control_drift_percent']:+.2f}% | {a1['allocated_bytes']:g} / {b['allocated_bytes']:g} / {a2['allocated_bytes']:g} |")
    summary = '\n'.join(lines) + '\n'
    (artifacts / 'comparison.md').write_text(summary)
    with Path(os.environ['GITHUB_STEP_SUMMARY']).open('a') as stream:
        stream.write(summary)
    provenance['completed_utc'] = now()
    provenance['main_sha_at_end'] = output(['git', 'ls-remote', 'origin', 'refs/heads/main']).split()[0]
    provenance['artifact_sha256'] = inventory(artifacts)
    provenance['artifact_sha256'].pop('provenance.json', None)
    provenance_path.write_text(json.dumps(provenance, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--artifacts', required=True)
    execute(parser.parse_args())
