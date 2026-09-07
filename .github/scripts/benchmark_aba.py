"""Run fixed-source A-B-A on one runner; measurement completion is not acceptance."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from xml.sax.saxutils import escape

SUITES = {
    3082: ['*BinaryKeyDispatchBenchmarks*'],
    3083: ['*BatchReadBench*', '*BatchBoundBench*'],
    3085: ['*RelayMetricsBenchmarks*', '*ActualPublisherBenchmarks*'],
    3086: ['*SuccessfulFetch*', '*FollowerError(Prefetch*', '*LeaderError*', '*ResponsePoolControl*'],
    3109: ['*DrainFullQueue*'],
    3116: ['*RecordCount: 1024, HeaderCount: 0*'],
    3117: ['*Mode: KeyRecordsDistinct*', '*Mode: KeyBatchesDistinct*', '*Mode: KeyRecordsPaired*', '*Mode: KeyBatchesPaired*'],
}
EXPECTED_CASES = {3082: 9, 3083: 7, 3085: 8, 3086: 8, 3109: 1, 3116: 7, 3117: 4}

def now():
    return datetime.now(timezone.utc).isoformat()

def run(arguments, log, *, cwd=None, env=None):
    with log.open('w', encoding='utf-8') as stream:
        result = subprocess.run(arguments, cwd=cwd, env=env, stdout=stream, stderr=subprocess.STDOUT)
    if result.returncode:
        raise RuntimeError(f'{arguments[0]} failed ({result.returncode}); see {log}')

def output(arguments, **kwargs):
    return subprocess.check_output(arguments, **kwargs).decode().strip()

def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def reports(folder):
    cases = {}
    for path in sorted(folder.glob('*-report-full.json')):
        for case in json.loads(path.read_text(encoding='utf-8-sig')).get('Benchmarks', []):
            stats = case.get('Statistics') or {}
            mean, count = stats.get('Mean'), stats.get('N')
            if not isinstance(mean, (int, float)) or not math.isfinite(mean) or mean <= 0 or not isinstance(count, int) or count < 1:
                raise ValueError(f'Invalid measurements: {path}')
            key = '|'.join(str(case.get(name, '')) for name in ('Namespace', 'Type', 'Method', 'Parameters'))
            if key in cases:
                raise ValueError(f'Duplicate case: {key}')
            allocated = next((metric['Value'] for metric in case.get('Metrics', [])
                              if metric['Descriptor']['Id'] == 'Allocated Memory'), None)
            if allocated is None or not math.isfinite(allocated) or allocated < 0:
                raise ValueError(f'Missing allocation measurement: {key}')
            cases[key] = {'mean_ns': mean, 'allocated_bytes': allocated, 'statistics': stats,
                          'full_name': case.get('FullName'), 'report': path.name}
    if not cases:
        raise ValueError(f'No measured cases: {folder}')
    return cases

def compare(phases):
    a1, b, a2 = (phases[name] for name in ('A1', 'B', 'A2'))
    if a1.keys() != b.keys() or a1.keys() != a2.keys():
        raise ValueError('Phase case sets differ; this is not a complete paired comparison')
    rows = []
    for key in sorted(a1):
        x, y, z = a1[key], b[key], a2[key]
        rows.append({'case': key, 'A1': x, 'B': y, 'A2': z,
                     'candidate_vs_A1_percent': 100 * (y['mean_ns'] / x['mean_ns'] - 1),
                     'candidate_vs_A2_percent': 100 * (y['mean_ns'] / z['mean_ns'] - 1),
                     'control_drift_percent': 100 * (z['mean_ns'] / x['mean_ns'] - 1)})
    return rows

def execute(args):
    repository = Path.cwd()
    artifacts = Path(args.artifacts).resolve()
    artifacts.mkdir(parents=True, exist_ok=False)
    if not re.fullmatch(r'[0-9a-f]{40}', args.baseline) or not re.fullmatch(r'[0-9a-f]{40}', args.candidate):
        raise ValueError('Exact lowercase 40-character SHAs required')
    for sha in (args.baseline, args.candidate):
        if output(['git', 'rev-parse', f'{sha}^{{commit}}']) != sha:
            raise ValueError('Commit resolution changed')
    subprocess.run(['git', 'merge-base', '--is-ancestor', args.baseline, args.candidate], check=True)
    workspace = Path(os.environ['RUNNER_TEMP']) / f'aba-{args.pr}'
    workspace.mkdir(exist_ok=False)
    allowed_cpus = sorted(os.sched_getaffinity(0))
    cpu = allowed_cpus[0]
    metadata = {'started_utc': now(), 'baseline_sha': args.baseline, 'candidate_sha': args.candidate,
                'workflow_sha': os.environ.get('GITHUB_SHA'), 'run_url': f"{os.environ['GITHUB_SERVER_URL']}/{os.environ['GITHUB_REPOSITORY']}/actions/runs/{os.environ['GITHUB_RUN_ID']}",
                'runner': 'ubuntu-latest', 'image_os': os.environ.get('ImageOS'), 'image_version': os.environ.get('ImageVersion'),
                'cpu_affinity': [cpu], 'original_allowed_cpus': allowed_cpus, 'phase_order': ['A1', 'B', 'A2'],
                'runtime_configuration': {'DOTNET_TieredCompilation': '0'}, 'outliers': 'DontRemove',
                'scope': 'Microbenchmark elapsed time and managed allocations; no full Pareto acceptance.', 'phases': []}
    (artifacts / 'provenance.json').write_text(json.dumps(metadata, indent=2))
    for name, command in {'cpu': ['lscpu'], 'runtime': ['dotnet', '--info'], 'os': ['uname', '-a']}.items():
        run(command, artifacts / f'{name}.txt')
    shutil.copyfile('/etc/os-release', artifacts / 'os-release.txt')
    fixtures = repository / '.github/benchmarks/aba'
    hosts = {}
    environment = dict(os.environ, MSBUILDDISABLENODEREUSE='1', DOTNET_TieredCompilation='0', ABA_PR=str(args.pr))
    for label, sha in (('A', args.baseline), ('B', args.candidate)):
        product = workspace / f'product-{label}'
        run(['git', 'worktree', 'add', '--detach', str(product), sha], artifacts / f'checkout-{label}.log')
        fixture = workspace / f'fixture-{label}'
        fixture.mkdir()
        shutil.copyfile(repository / 'global.json', fixture / 'global.json')
        shutil.copyfile(repository / 'Directory.Packages.props', fixture / 'Directory.Packages.props')
        shutil.copyfile(fixtures / 'Program.cs', fixture / 'Program.cs')
        for file in (fixtures / str(args.pr)).glob('*.cs'):
            shutil.copyfile(file, fixture / file.name)
        project = product / ('src/Dekaf.Outbox/Dekaf.Outbox.csproj' if args.pr == 3085 else 'src/Dekaf/Dekaf.csproj')
        constants = 'ABA_BASELINE' if label == 'A' else 'ABA_CANDIDATE'
        (fixture / 'Dekaf.Benchmarks.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>preview</LangVersion><AssemblyName>Dekaf.Benchmarks</AssemblyName><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally><DefineConstants>$(DefineConstants);{constants}</DefineConstants><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup>
  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /><PackageReference Include="BenchmarkDotNet" /><ProjectReference Include="{escape(str(project))}" /></ItemGroup>
</Project>
''')
        run(['dotnet', 'build', str(fixture / 'Dekaf.Benchmarks.csproj'), '-c', 'Release', '-m:1', '-nr:false', '-p:UseSharedCompilation=false'], artifacts / f'build-{label}.log', cwd=fixture, env=environment)
        hosts[label] = fixture / 'bin/Release/net10.0/Dekaf.Benchmarks.dll'
        manifest = {file.name: sha256(file) for file in hosts[label].parent.glob('*.dll')}
        (artifacts / f'binaries-{label}.json').write_text(json.dumps(manifest, indent=2))
        snapshot = artifacts / f'fixture-{label}'
        snapshot.mkdir()
        for file in fixture.iterdir():
            if file.is_file():
                shutil.copyfile(file, snapshot / file.name)
    # Both builds finish before timing. Each phase uses a fresh prebuilt host;
    # in-process emit does not launch MSBuild during the experiment.
    run(['dotnet', 'build-server', 'shutdown'], artifacts / 'build-server-shutdown.log', env=environment)
    for label in ('A', 'B'):
        run(['taskset', '-c', str(cpu), 'dotnet', str(hosts[label]), '--dry', '--filter', *SUITES[args.pr], '--artifacts', str(artifacts / f'dry-{label}')],
            artifacts / f'dry-{label}.log', cwd=hosts[label].parent, env=dict(environment, ABA_PHASE=f'Dry{label}'))
        if len(reports(artifacts / f'dry-{label}/results')) != EXPECTED_CASES[args.pr]:
            raise ValueError('Dry run did not cover the declared benchmark matrix')
    for phase, label in (('A1', 'A'), ('B', 'B'), ('A2', 'A')):
        recorded = json.loads((artifacts / f'binaries-{label}.json').read_text())
        if any(sha256(hosts[label].parent / name) != digest for name, digest in recorded.items()):
            raise ValueError('Prebuilt measurement inputs changed')
        run(['ps', '-eo', 'pid,ppid,pcpu,pmem,args'], artifacts / f'processes-{phase}.txt')
        entry = {'phase': phase, 'product_sha': args.baseline if label == 'A' else args.candidate, 'started_utc': now()}
        run(['taskset', '-c', str(cpu), 'dotnet', str(hosts[label]), '--filter', *SUITES[args.pr], '--artifacts', str(artifacts / phase)],
            artifacts / f'{phase}.log', cwd=hosts[label].parent, env=dict(environment, ABA_PHASE=phase))
        entry['completed_utc'] = now()
        entry['cases'] = len(reports(artifacts / phase / 'results'))
        metadata['phases'].append(entry)
        (artifacts / 'provenance.json').write_text(json.dumps(metadata, indent=2))
    rows = compare({phase: reports(artifacts / phase / 'results') for phase in ('A1', 'B', 'A2')})
    (artifacts / 'comparison.json').write_text(json.dumps({'measurement_status': 'COMPLETE', 'acceptance': 'NOT_EVALUATED', 'cases': rows}, indent=2))
    lines = [f'# PR #{args.pr}: Ubuntu A–B–A', '', f"A: `{args.baseline}`; B: `{args.candidate}`.", '',
             'Measurement completion is not performance acceptance. No automatic performance-gate override.', '',
             '| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |',
             '|---|---:|---:|---:|---:|---:|---:|---:|']
    for row in rows:
        a, b, c = (row[phase] for phase in ('A1', 'B', 'A2'))
        lines.append(f"| {row['case'].replace('|', ' / ')} | {a['mean_ns']:.3f} | {b['mean_ns']:.3f} | {c['mean_ns']:.3f} | {row['candidate_vs_A1_percent']:+.2f}% | {row['candidate_vs_A2_percent']:+.2f}% | {row['control_drift_percent']:+.2f}% | {a['allocated_bytes']:g} / {b['allocated_bytes']:g} / {c['allocated_bytes']:g} |")
    summary = '\n'.join(lines) + '\n'
    (artifacts / 'comparison.md').write_text(summary)
    with Path(os.environ['GITHUB_STEP_SUMMARY']).open('a') as stream:
        stream.write(summary)
    metadata['completed_utc'] = now()
    (artifacts / 'provenance.json').write_text(json.dumps(metadata, indent=2))

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--pr', type=int, choices=SUITES, required=True)
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--artifacts', required=True)
    try:
        execute(parser.parse_args())
    except (ValueError, RuntimeError, OSError, subprocess.CalledProcessError) as error:
        print(f'A-B-A failed: {error}', file=sys.stderr)
        sys.exit(1)
