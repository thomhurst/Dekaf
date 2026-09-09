#!/usr/bin/env python3
"""Exact-product-SHA administrative comparison. Never writes a GitHub gate."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys

CONTROLS = ['legacy:16', 'inventory:16', 'delete:16']
NEW_CASES = ['classic:16', 'mixed:16', 'retry:16', 'malformed:16', 'classic:1']
LIMITS = {'CallsPerSecond': .03, 'CpuNsPerCall': .03, 'P50Ns': .05, 'P99Ns': .05, 'MaxNs': .05}


def run(command, cwd, log=None, env=None):
    if log:
        log.parent.mkdir(parents=True, exist_ok=True)
        with log.open('w', encoding='utf-8') as output:
            subprocess.run(command, cwd=cwd, env=env, stdout=output, stderr=subprocess.STDOUT, check=True)
        return ''
    return subprocess.check_output(command, cwd=cwd, env=env, text=True).strip()


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding='utf-8')


def verify_copied_tree(original, copied):
    for path in original.rglob('*'):
        if path.is_file():
            target = copied / path.relative_to(original)
            if not target.is_file() or hashlib.sha256(path.read_bytes()).digest() != hashlib.sha256(target.read_bytes()).digest():
                raise ValueError(f'Archive copy differs from original: {path}')


def retain_loaded_binaries(manifest, original_root, archive_root):
    rows = json.loads(manifest.read_text(encoding='utf-8-sig'))
    if not rows or not any(Path(row['Path']).name == 'Dekaf.dll' for row in rows):
        raise ValueError(f'{manifest}: loaded product identity missing')
    for row in rows:
        original = Path(row['Path']).resolve()
        relative = original.relative_to(original_root.resolve())
        target = archive_root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, target)
        for path in [original, target]:
            if hashlib.sha256(path.read_bytes()).hexdigest() != row['Sha256']:
                raise ValueError(f'{manifest}: loaded binary hash mismatch: {path}')


def validate_probe(path, minimum_seconds, data=None):
    if data is None:
        data = json.loads(path.read_text(encoding='utf-8-sig'))
    if data['Seconds'] < minimum_seconds or data['Completed'] <= 0:
        raise ValueError(f'{path}: insufficient workload duration or completions')
    histogram = data['Latencies']
    if not histogram or any(x['Ticks'] < 0 or x['Count'] <= 0 for x in histogram):
        raise ValueError(f'{path}: invalid histogram')
    if any(a['Ticks'] >= b['Ticks'] for a, b in zip(histogram, histogram[1:])):
        raise ValueError(f'{path}: histogram not sorted/unique')
    if sum(x['Count'] for x in histogram) != data['Completed']:
        raise ValueError(f'{path}: histogram lost completed calls')
    if sum(sum(x['Count'] for x in row['Latencies']) for row in data['Intervals']) != data['Completed']:
        raise ValueError(f'{path}: interval histograms lost completed calls')
    for field, percentile in [('P50Ns', .5), ('P99Ns', .99), ('MaxNs', 1)]:
        seen = 0
        for bucket in histogram:
            seen += bucket['Count']
            if seen >= math.ceil(data['Completed'] * percentile):
                expected = bucket['Ticks'] * 1e9 / data['StopwatchFrequency']
                if not math.isclose(data[field], expected, rel_tol=1e-10, abs_tol=1e-6):
                    raise ValueError(f'{path}: {field} does not match retained histogram')
                break
    for field in [*LIMITS, 'AllocatedBytesPerCall']:
        if not math.isfinite(data[field]) or data[field] < 0:
            raise ValueError(f'{path}: invalid {field}')
    return data


def validate_primer_segments(primer_path):
    path = primer_path.with_name('segments-' + primer_path.name)
    segments = json.loads(path.read_text(encoding='utf-8-sig'))
    if len(segments) != 128:
        raise ValueError(f'{path}: complete measurement primer requires 128 segments')
    for index, segment in enumerate(segments):
        validate_probe(f'{path}[{index}]', .05, segment)


def validate_bdn_workload_warmup(benchmark, smoke=False):
    warmups = [row for row in benchmark['Measurements']
               if row['IterationMode'] == 'Workload' and row['IterationStage'] == 'Warmup']
    if any(not math.isfinite(row['Nanoseconds']) or row['Nanoseconds'] <= 0 or row['Operations'] <= 0
           for row in warmups):
        raise ValueError('BDN workload warmup contains invalid duration or operation counts')
    seconds = sum(row['Nanoseconds'] for row in warmups) / 1e9
    completed = sum(row['Operations'] for row in warmups)
    if not smoke and (len(warmups) != 50 or seconds < 20):
        raise ValueError('BDN requires fifty workload warmups totaling at least twenty elapsed seconds')
    return dict(seconds=seconds, completed=completed, iterations=len(warmups),
                smoke=smoke, scope='Raw BDN Workload/Warmup measurements; smoke is not acceptance')


def compare(a1, b, a2):
    rows = []
    within_limits = True
    for field, tolerance in LIMITS.items():
        first, candidate, second = a1[field], b[field], a2[field]
        delta1, delta2, drift = candidate / first - 1, candidate / second - 1, second / first - 1
        loss = min(delta1, delta2) < -tolerance if field == 'CallsPerSecond' else max(delta1, delta2) > tolerance
        noisy = abs(drift) > tolerance
        within_limits &= not loss and not noisy
        rows.append(dict(metric=field, A1=first, B=candidate, A2=second,
                         B_vs_A1=delta1, B_vs_A2=delta2, A2_vs_A1=drift,
                         tolerance=tolerance, candidate_exceeds_limit=loss, control_drift_exceeds_limit=noisy))
    field = 'AllocatedBytesPerCall'
    loss = b[field] > min(a1[field], a2[field]) + 1
    noisy = abs(a1[field] - a2[field]) > 1
    within_limits &= not loss and not noisy
    rows.append(dict(metric=field, A1=a1[field], B=b[field], A2=a2[field],
                     B_minus_A1=b[field]-a1[field], B_minus_A2=b[field]-a2[field], A2_minus_A1=a2[field]-a1[field],
                     allowance_bytes=1, candidate_exceeds_limit=loss, control_drift_exceeds_limit=noisy))
    return {'point_estimates_within_declared_limits': within_limits, 'metrics': rows,
            'verdict': 'INCONCLUSIVE: precision, startup and stability require assessment; point estimates alone are not acceptance'}


def execute(args):
    source = Path(__file__).resolve().parent
    repository = Path(run(['git', 'rev-parse', '--show-toplevel'], source))
    for revision in [args.baseline, args.candidate]:
        if not re.fullmatch('[0-9a-f]{40}', revision):
            raise ValueError('Both product revisions must be exact lowercase SHA-1 commits')
    output = Path(args.output).resolve()
    if output.exists():
        raise ValueError('Output directory must be new; never overwrite an earlier experiment')
    output.mkdir(parents=True)
    archive = output / 'archive'
    run(['git', 'fetch', 'origin', 'main'], repository)
    main_now = run(['git', 'rev-parse', 'origin/main'], repository)
    if main_now != args.baseline:
        raise ValueError(f'Fresh main moved to {main_now}; rebase/pin before acceptance')
    run(['git', 'merge-base', '--is-ancestor', args.baseline, args.candidate], repository)
    metadata = dict(A=args.baseline, B=args.candidate, harness=run(['git', 'rev-parse', 'HEAD'], repository),
                    main_at_start=main_now, platform=platform.platform(), machine=platform.machine(),
                    image=os.environ.get('ImageOS'), image_version=os.environ.get('ImageVersion'),
                    github_run=os.environ.get('GITHUB_RUN_ID'), smoke=args.smoke,
                    primer_segments=128, primer_segment_seconds=.05, primer_seconds=1,
                    warmup_seconds=.2 if args.smoke else 120, measured_seconds=.2 if args.smoke else 60,
                    bdn_workload_warmup_iterations=None if args.smoke else 50,
                    bdn_workload_warmup_minimum_seconds=0 if args.smoke else 20,
                    bdn_outlier_mode='DontRemove', bdn_keep_files=True,
                    runtime={'DOTNET_TieredCompilation':'1', 'DOTNET_TieredPGO':'1', 'DOTNET_gcServer':'0'},
                    controls=CONTROLS, candidate_only=NEW_CASES)
    save(archive / 'provenance.json', metadata)
    run(['dotnet', '--info'], repository, archive / 'dotnet-info.txt')
    if sys.platform.startswith('linux'):
        run(['lscpu'], repository, archive / 'hardware.txt')
    copied_sources = [p for p in source.iterdir() if p.suffix in {'.cs', '.csproj', '.py', '.md'}]
    (archive / 'harness').mkdir(parents=True)
    for path in copied_sources:
        shutil.copy2(path, archive / 'harness' / path.name)
    work = output / 'work'
    work.mkdir()
    created = []
    products = {}
    try:
        for name, revision in [('A', args.baseline), ('B', args.candidate)]:
            checkout = work / name
            run(['git', 'worktree', 'add', '--detach', str(checkout), revision], repository)
            created.append(checkout)
            run(['git', 'worktree', 'lock', '--reason', 'PR #3128 owned evidence build', str(checkout)], repository)
            if Path(run(['git','rev-parse','--show-toplevel'], checkout)).resolve() != checkout.resolve():
                raise ValueError('Product worktree path mismatch')
            run(['git', 'archive', '--format=zip', f'--output={archive / (name + "-source.zip")}', revision], repository)
            project = checkout / 'tools' / 'AdminDescriptionEvidence'
            project.mkdir(parents=True, exist_ok=True)
            for path in copied_sources:
                shutil.copy2(path, project / path.name)
            (project / 'Revision.props').write_text(
                f'<Project><PropertyGroup><Candidate>{str(name == "B").lower()}</Candidate></PropertyGroup></Project>', encoding='utf-8')
            run(['dotnet','build',str(project / 'Runner.csproj'),'-c','Release','--disable-build-servers'], checkout, archive / f'build-{name}.log')
            binary = project / 'bin' / 'Release' / 'net10.0' / 'Dekaf.Benchmarks.dll'
            products[name] = (checkout, binary)
            shutil.copytree(binary.parent, archive / 'binaries' / name)
            verify_copied_tree(binary.parent, archive / 'binaries' / name)
            shutil.copytree(project, archive / 'fixtures' / name, ignore=shutil.ignore_patterns('bin', 'obj', '__pycache__'))
        env = os.environ.copy()
        env.update(metadata['runtime'])
        for name, (checkout, binary) in products.items():
            for case in CONTROLS + (NEW_CASES if name == 'B' else []):
                destination = archive / 'validation' / name / case.replace(':','-')
                run(['dotnet',str(binary),'probe',case,str(destination),'.2','.2'], checkout, destination / 'run.log', env)
                validate_probe(destination / 'measured.json', .2)
        if os.environ.get('GITHUB_ACTIONS') == 'true':
            run(['dotnet','build-server','shutdown'], repository, archive / 'build-server-shutdown.log')
        for phase, product in [('A1','A'), ('B','B'), ('A2','A')]:
            checkout, binary = products[product]
            cases = CONTROLS + (NEW_CASES if product == 'B' else [])
            for case in cases:
                destination = archive / phase / case.replace(':','-')
                run(['dotnet',str(binary),'probe',case,str(destination),str(metadata['warmup_seconds']),str(metadata['measured_seconds'])],
                    checkout, destination / 'probe.log', env)
                validate_probe(destination / 'warmup.json', metadata['warmup_seconds'])
                validate_probe(destination / 'primer.json', metadata['primer_seconds'])
                validate_primer_segments(destination / 'primer.json')
                validate_probe(destination / 'measured.json', metadata['measured_seconds'])
                retain_loaded_binaries(destination / 'binaries.json', binary.parent, archive / 'binaries' / product)
            env['ADMIN_EVIDENCE_CASES'] = ','.join(cases)
            bdn = ['dotnet',str(binary),'--filter','*AdminEvidenceBenchmark*','--exporters','fulljson',
                   '--artifacts',str(archive / phase / 'bdn'), '--keepFiles']
            bdn += ['--smoke-bdn'] if args.smoke else []
            try:
                run(bdn, binary.parent, archive / phase / 'bdn.log', env)
            finally:
                # Keep the generated project and loaded worker binaries before A2 or cleanup.
                workers = [p.parent for p in binary.parent.rglob('BenchmarkDotNet.Autogenerated.csproj')]
                for index, worker in enumerate(workers):
                    target = archive / phase / 'bdn-workers' / str(index)
                    shutil.copytree(worker, target)
                    verify_copied_tree(worker, target)
            if not workers:
                raise ValueError(f'{phase}: generated BDN worker outputs were not retained')
            reports = list((archive / phase / 'bdn' / 'results').glob('*-full.json'))
            benchmarks = [benchmark for path in reports for benchmark in json.loads(path.read_text(encoding='utf-8-sig')).get('Benchmarks', [])]
            if len(benchmarks) != len(cases) or any(not b.get('Statistics') for b in benchmarks):
                raise ValueError(f'{phase}: BDN did not measure every requested fixture')
            if sorted(b['Parameters'] for b in benchmarks) != sorted('Case=' + case for case in cases):
                raise ValueError(f'{phase}: BDN measured fixture identities differ from requested cases')
            save(archive / phase / 'bdn-workload-warmup.json',
                 {b['Parameters']: validate_bdn_workload_warmup(b, args.smoke) for b in benchmarks})
        summary = {}
        for case in CONTROLS:
            inputs = [json.loads((archive / phase / case.replace(':','-') / 'measured.json').read_text(encoding='utf-8-sig')) for phase in ['A1','B','A2']]
            summary[case] = compare(*inputs)
        save(archive / 'control-assessment.json', summary)
    finally:
        try:
            metadata['main_at_end'] = run(['git','ls-remote','origin','refs/heads/main'], repository).split()[0]
        except (subprocess.SubprocessError, IndexError) as error:
            # A network failure must not mask the experiment failure or prevent archival.
            metadata['main_at_end_error'] = str(error)
        save(archive / 'provenance.json', metadata)
        inventory = [{'path':str(p.relative_to(archive)), 'bytes':p.stat().st_size,
                      'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
                     for p in sorted(archive.rglob('*')) if p.is_file() and p.name != 'inventory.json']
        save(archive / 'inventory.json', inventory)
        for checkout in reversed(created):
            if not checkout.resolve().is_relative_to(work.resolve()):
                raise ValueError('Refusing worktree cleanup outside the owned experiment directory')
            run(['git','worktree','unlock',str(checkout)], repository)
            run(['git','worktree','remove','--force',str(checkout)], repository)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--smoke', action='store_true')
    execute(parser.parse_args())
