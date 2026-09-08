#!/usr/bin/env python3
"""Candidate-only startup diagnosis. Never grants performance acceptance."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys

from run_comparison import run, save, retain_loaded_binaries, validate_probe, validate_primer_segments, verify_copied_tree


def execute(args):
    source = Path(__file__).resolve().parent
    repository = Path(run(['git', 'rev-parse', '--show-toplevel'], source))
    if any(not re.fullmatch('[0-9a-f]{40}', revision) for revision in [args.baseline, args.candidate]):
        raise ValueError('Product pins must be exact lowercase SHA-1 commits')
    output = Path(args.output).resolve()
    if output.exists():
        raise ValueError('Output directory must be new')
    output.mkdir(parents=True)
    archive = output / 'archive'
    run(['git', 'fetch', 'origin', 'main'], repository)
    main = run(['git', 'rev-parse', 'origin/main'], repository)
    if main != args.baseline:
        raise ValueError(f'Fresh main moved to {main}; repin before diagnosis')
    run(['git', 'merge-base', '--is-ancestor', args.baseline, args.candidate], repository)
    metadata = dict(kind='candidate-only startup diagnostic; not A/B/A acceptance',
                    A=args.baseline, B=args.candidate, harness=run(['git', 'rev-parse', 'HEAD'], repository),
                    main_at_start=main, platform=platform.platform(), machine=platform.machine(),
                    image=os.environ.get('ImageOS'), image_version=os.environ.get('ImageVersion'),
                    github_run=os.environ.get('GITHUB_RUN_ID'), smoke=args.smoke,
                    case='retry:16', phases=['untraced1', 'traced', 'untraced2'],
                    primer_segments=128, primer_segment_seconds=.05, primer_seconds=1,
                    warmup_seconds=.2 if args.smoke else 120, measured_seconds=.2 if args.smoke else 20,
                    trace_tool_version='9.0.652701',
                    runtime={'DOTNET_TieredCompilation':'1', 'DOTNET_TieredPGO':'1', 'DOTNET_gcServer':'0'})
    save(archive / 'provenance.json', metadata)
    run(['dotnet', '--info'], repository, archive / 'dotnet-info.txt')
    if sys.platform.startswith('linux'):
        run(['lscpu'], repository, archive / 'hardware.txt')
    checkout = output / 'work' / 'B'
    created = False
    try:
        run(['git', 'archive', '--format=zip', f'--output={archive / "harness-source.zip"}', metadata['harness']], repository)
        run(['git', 'archive', '--format=zip', f'--output={archive / "B-source.zip"}', args.candidate], repository)
        run(['git', 'worktree', 'add', '--detach', str(checkout), args.candidate], repository)
        created = True
        run(['git', 'worktree', 'lock', '--reason', 'PR #3128 owned startup diagnostic', str(checkout)], repository)
        if Path(run(['git', 'rev-parse', '--show-toplevel'], checkout)).resolve() != checkout.resolve():
            raise ValueError('Product worktree path mismatch')
        project = checkout / 'tools' / 'AdminDescriptionEvidence'
        project.mkdir(parents=True)
        for path in source.iterdir():
            if path.suffix in {'.cs', '.csproj', '.py', '.md'}:
                shutil.copy2(path, project / path.name)
        (project / 'Revision.props').write_text('<Project><PropertyGroup><Candidate>true</Candidate></PropertyGroup></Project>', encoding='utf-8')
        shutil.copytree(project, archive / 'fixture')
        run(['dotnet', 'build', str(project / 'Runner.csproj'), '-c', 'Release', '--disable-build-servers'], checkout, archive / 'build-B.log')
        binary = project / 'bin' / 'Release' / 'net10.0' / 'Dekaf.Benchmarks.dll'
        shutil.copytree(binary.parent, archive / 'binaries' / 'B')
        verify_copied_tree(binary.parent, archive / 'binaries' / 'B')
        inspector_source = repository / 'tools' / 'AdminJitTraceInspector'
        shutil.copytree(inspector_source, archive / 'inspector-source', ignore=shutil.ignore_patterns('bin', 'obj'))
        run(['dotnet', 'build', str(inspector_source / 'AdminJitTraceInspector.csproj'), '-c', 'Release', '--disable-build-servers'], repository, archive / 'build-inspector.log')
        inspector = inspector_source / 'bin' / 'Release' / 'net10.0' / 'AdminJitTraceInspector.dll'
        shutil.copytree(inspector.parent, archive / 'binaries' / 'inspector')
        verify_copied_tree(inspector.parent, archive / 'binaries' / 'inspector')
        trace_directory = archive / 'trace-tool'
        run(['dotnet', 'tool', 'install', 'dotnet-trace', '--version', metadata['trace_tool_version'],
             '--tool-path', str(trace_directory), '--allow-roll-forward'], repository, archive / 'install-trace.log')
        trace = trace_directory / ('dotnet-trace.exe' if os.name == 'nt' else 'dotnet-trace')
        run([str(trace), '--version'], repository, archive / 'trace-version.txt')
        env = os.environ.copy()
        env.update(metadata['runtime'])
        if os.environ.get('GITHUB_ACTIONS') == 'true':
            run(['dotnet', 'build-server', 'shutdown'], repository, archive / 'build-server-shutdown.log')
        summary = {}
        for phase in metadata['phases']:
            destination = archive / phase
            command = ['dotnet', str(binary), 'probe', metadata['case'], str(destination),
                       str(metadata['warmup_seconds']), str(metadata['measured_seconds'])]
            if phase == 'traced':
                command = [str(trace), 'collect', '--providers',
                           'Microsoft-Windows-DotNETRuntime:0x10:5,Dekaf-AdminEvidence-Phases:0xffffffffffffffff:5',
                           '--rundown', 'false', '--show-child-io', '--output', str(archive / 'retry.nettrace'), '--', *command]
            run(command, checkout, destination / 'run.log', env)
            validate_probe(destination / 'primer.json', 1)
            validate_primer_segments(destination / 'primer.json')
            warmup = validate_probe(destination / 'warmup.json', metadata['warmup_seconds'])
            measured = validate_probe(destination / 'measured.json', metadata['measured_seconds'])
            retain_loaded_binaries(destination / 'binaries.json', binary.parent, archive / 'binaries' / 'B')
            summary[phase] = dict(warmup=warmup, measured=measured)
        run(['dotnet', str(inspector), str(archive / 'retry.nettrace'), str(archive / 'trace-events.json')],
            repository, archive / 'inspect-trace.log')
        events = json.loads((archive / 'trace-events.json').read_text(encoding='utf-8-sig'))
        phases = [row for row in events if row['Kind'] == 'phase']
        if [row['Name'] for row in phases] != ['initialize', 'primer', 'warmup', 'measured', 'finalize']:
            raise ValueError('Trace phase markers are missing or out of order')
        start, end = phases[3]['Milliseconds'], phases[4]['Milliseconds']
        save(archive / 'diagnostic-summary.json', dict(verdict='INCONCLUSIVE: startup diagnosis only; no product controls',
             phases=summary, measured_jit_events=[row for row in events if row['Kind'] == 'jit' and start <= row['Milliseconds'] < end]))
    finally:
        try:
            metadata['main_at_end'] = run(['git', 'ls-remote', 'origin', 'refs/heads/main'], repository).split()[0]
        except (subprocess.SubprocessError, IndexError) as error:
            metadata['main_at_end_error'] = str(error)
        save(archive / 'provenance.json', metadata)
        save(archive / 'inventory.json', [{'path': str(path.relative_to(archive)), 'bytes': path.stat().st_size,
             'sha256': hashlib.sha256(path.read_bytes()).hexdigest()} for path in sorted(archive.rglob('*'))
             if path.is_file() and path.name != 'inventory.json'])
        if created:
            if not checkout.resolve().is_relative_to((output / 'work').resolve()):
                raise ValueError('Refusing cleanup outside the owned diagnostic directory')
            run(['git', 'worktree', 'unlock', str(checkout)], repository)
            run(['git', 'worktree', 'remove', '--force', str(checkout)], repository)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--smoke', action='store_true')
    execute(parser.parse_args())
