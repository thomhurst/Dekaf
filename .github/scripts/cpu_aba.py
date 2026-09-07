"""Scoped #3109 CPU attribution; success does not change a performance gate."""
import argparse
import csv
import json
import os
from pathlib import Path
import shutil
import subprocess
from xml.sax.saxutils import escape
from benchmark_aba import now, output, run, sha256


def validate(folder, samples, warmup):
    metrics = json.loads((folder / 'metrics.json').read_text())
    rows = list(csv.DictReader((folder / 'cpu-stages.csv').open()))
    assert len(rows) == metrics['Samples'] == samples
    assert metrics['CompletedRecords'] == samples * 1024
    assert metrics['Failures'] == metrics['BacklogAtEnd'] == 0
    assert metrics['ActualWarmupSeconds'] >= warmup
    assert all(int(row['Sample']) == index + 1 for index, row in enumerate(rows))
    assert all(int(row['LatencyTicks']) > 0 for row in rows)
    assert all(int(value) >= 0 for row in rows for value in row.values())
    for column, metric in [('SetupCpuTicks', 'SetupCpuNs'), ('StopCpuTicks', 'StopCpuNs'), ('CleanupCpuTicks', 'CleanupCpuNs')]:
        assert abs(sum(int(row[column]) for row in rows) * 100 / samples - metrics[metric]) < .01
    return metrics


def execute(args):
    repository = Path.cwd()
    artifacts = Path(args.artifacts).resolve()
    artifacts.mkdir(parents=True, exist_ok=False)
    workspace = artifacts / 'work'
    workspace.mkdir()
    for sha in (args.baseline, args.candidate):
        assert len(sha) == 40 and output(['git', 'rev-parse', f'{sha}^{{commit}}']) == sha
    subprocess.run(['git', 'merge-base', '--is-ancestor', args.baseline, args.candidate], check=True)
    env = dict(os.environ, DOTNET_TieredCompilation='1', MSBUILDDISABLENODEREUSE='1')
    provenance = dict(baseline_sha=args.baseline, candidate_sha=args.candidate,
        harness_sha=output(['git', 'rev-parse', 'HEAD']), started_utc=now(),
        runner='local-smoke' if args.smoke else 'ubuntu-latest', phase_order=['A1', 'B', 'A2'],
        samples=100 if args.smoke else 30000, warmup_seconds=0 if args.smoke else 30,
        cpu_affinity=None if args.smoke else [2, 3], runtime={'DOTNET_TieredCompilation': '1'},
        run_id=os.environ.get('GITHUB_RUN_ID'), image_version=os.environ.get('ImageVersion'),
        acceptance='NOT_EVALUATED', scope='CPU attribution, not full acceptance')
    (artifacts / 'provenance.json').write_text(json.dumps(provenance, indent=2))
    shutil.copyfile(repository / '.github/benchmarks/aba/CPU-EXPERIMENT.md', artifacts / 'EXPERIMENT.md')
    run(['dotnet', '--info'], artifacts / 'runtime.txt')
    if not args.smoke:
        assert {2, 3}.issubset(os.sched_getaffinity(0))
        run(['lscpu'], artifacts / 'cpu.txt')
        shutil.copyfile('/etc/os-release', artifacts / 'os-release.txt')
    hosts = {}
    for label, sha in [('A', args.baseline), ('B', args.candidate)]:
        product = workspace / f'product-{label}'
        run(['git', 'worktree', 'add', '--detach', str(product), sha], artifacts / f'checkout-{label}.log')
        fixture = workspace / f'fixture-{label}'
        fixture.mkdir()
        for file in (repository / '.github/benchmarks/aba/3109').glob('*.cs'):
            shutil.copyfile(file, fixture / file.name)
        for name in ['global.json', 'Directory.Packages.props']:
            shutil.copyfile(repository / name, fixture / name)
        shutil.copyfile(repository / '.github/benchmarks/aba/Program.cs', fixture / 'Program.cs')
        project = product / 'src/Dekaf/Dekaf.csproj'
        (fixture / 'Dekaf.Benchmarks.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>preview</LangVersion><AssemblyName>Dekaf.Benchmarks</AssemblyName><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally><DefineConstants>ABA_SHUTDOWN</DefineConstants><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup>
  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /><PackageReference Include="BenchmarkDotNet" /><ProjectReference Include="{escape(str(project))}" /></ItemGroup>
</Project>''', encoding='utf-8')
        run(['dotnet', 'build', str(fixture / 'Dekaf.Benchmarks.csproj'), '-c', 'Release', '-m:1', '-nr:false'],
            artifacts / f'build-{label}.log', cwd=fixture, env=env)
        host = fixture / 'bin/Release/net10.0/Dekaf.Benchmarks.dll'
        hosts[label] = host
        (artifacts / f'binaries-{label}.json').write_text(json.dumps(
            {file.name: sha256(file) for file in host.parent.glob('*.dll')}, indent=2))
        snapshot = artifacts / f'fixture-{label}'
        snapshot.mkdir()
        for file in fixture.iterdir():
            if file.is_file():
                shutil.copyfile(file, snapshot / file.name)
    run(['dotnet', 'build-server', 'shutdown'], artifacts / 'build-server-shutdown.log')
    prefix = [] if args.smoke else ['taskset', '-c', '2,3']
    for label, host in hosts.items():
        folder = artifacts / f'dry-{label}'
        run([*prefix, 'dotnet', str(host), '--shutdown-cpu', str(folder), '100', '0'],
            artifacts / f'dry-{label}.log', env=env)
        validate(folder, 100, 0)
    results = {}
    for phase, label in [('A1', 'A'), ('B', 'B'), ('A2', 'A')]:
        host = hosts[label]
        recorded = json.loads((artifacts / f'binaries-{label}.json').read_text())
        assert all(sha256(host.parent / name) == digest for name, digest in recorded.items())
        folder = artifacts / phase
        run([*prefix, 'dotnet', str(host), '--shutdown-cpu', str(folder), str(provenance['samples']), str(provenance['warmup_seconds'])],
            artifacts / f'{phase}.log', env=env)
        results[phase] = validate(folder, provenance['samples'], provenance['warmup_seconds'])
    (artifacts / 'comparison.json').write_text(json.dumps(results, indent=2))
    provenance['completed_utc'] = now()
    (artifacts / 'provenance.json').write_text(json.dumps(provenance, indent=2))
    print(json.dumps(results, indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--pr', type=int, choices=[3109], required=True)
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--artifacts', required=True)
    parser.add_argument('--smoke', action='store_true')
    execute(parser.parse_args())
