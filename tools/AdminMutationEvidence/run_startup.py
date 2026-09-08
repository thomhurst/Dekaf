"""Bounded untraced/traced/untraced startup diagnosis; no acceptance gate."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

from validate_results import validate, runtime_transitions


def save(path, data):
    path.write_text(json.dumps(data, indent=2), encoding='utf-8')


def run(command, log, env):
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open('w', encoding='utf-8') as output:
        subprocess.run(command, stdout=output, stderr=subprocess.STDOUT, env=env, check=True, timeout=600)


def execute(args):
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=False)
    root = Path(__file__).resolve().parent
    binary = root / 'bin/Release/net10.0/Dekaf.Benchmarks.dll'
    inspector = root.parent / 'AdminJitTraceInspector/bin/Release/net10.0/AdminJitTraceInspector.dll'
    tracer = Path(args.tracer).resolve()
    if not all(path.is_file() for path in [binary, inspector, tracer]):
        raise ValueError('Build the probe and inspector and install the pinned tracer before diagnosis.')
    env = os.environ.copy()
    settings = {'DOTNET_TieredCompilation': '1', 'DOTNET_TieredPGO': '1', 'DOTNET_gcServer': '0'}
    env.update(settings)
    warmup, measured = (.2, .2) if args.smoke else (120, 20)
    save(output / 'plan.json', dict(case=args.case, warmup_seconds=warmup, measured_seconds=measured,
                                  phases=['untraced1', 'traced', 'untraced2'], runtime=settings,
                                  fixture_sha256=hashlib.sha256(binary.read_bytes()).hexdigest(),
                                  product_sha256=hashlib.sha256(binary.with_name('Dekaf.dll').read_bytes()).hexdigest(),
                                  tracer=str(tracer), verdict='INCONCLUSIVE: startup diagnosis only'))
    shutil.copytree(binary.parent, output / 'binaries/probe')
    shutil.copytree(inspector.parent, output / 'binaries/inspector')
    shutil.copytree(root, output / 'fixture', ignore=shutil.ignore_patterns('bin', 'obj', '__pycache__'))
    summary = {}
    for phase in ['untraced1', 'traced', 'untraced2']:
        destination = output / phase
        command = ['dotnet', str(binary), 'probe', args.case, str(destination), str(warmup), str(measured)]
        if phase == 'traced':
            command = [str(tracer), 'collect', '--providers',
                       'Microsoft-Windows-DotNETRuntime:0x10:5,Dekaf-AdminEvidence-Phases:0xffffffffffffffff:5',
                       '--rundown', 'false', '--show-child-io', '--output', str(output / 'startup.nettrace'), '--', *command]
        run(command, destination / 'run.log', env)
        w = validate(destination / 'warmup.json', warmup)
        m = validate(destination / 'measured.json', measured)
        validate(destination / 'primer.json', 1)
        segments = json.loads((destination / 'segments-primer.json').read_text(encoding='utf-8-sig'))
        if len(segments) != 128 or any(row['Seconds'] < .05 for row in segments):
            raise ValueError(f'{phase}: incomplete primer')
        for row in json.loads((destination / 'binaries.json').read_text(encoding='utf-8-sig')):
            source = Path(row['Path'])
            copied = output / 'binaries/probe' / source.relative_to(binary.parent)
            if hashlib.sha256(source.read_bytes()).hexdigest() != row['Sha256'] or hashlib.sha256(copied.read_bytes()).hexdigest() != row['Sha256']:
                raise ValueError(f'{phase}: loaded binary differs from retained copy')
        summary[phase] = dict(warmup=w, measured=m, transitions=runtime_transitions(w, m))
        print(f'{phase}: {m["Completed"]} measured calls; JIT delta {m["End"]["JitMethods"] - m["Start"]["JitMethods"]}', flush=True)
    run(['dotnet', str(inspector), str(output / 'startup.nettrace'), str(output / 'events.json')], output / 'inspect.log', env)
    events = json.loads((output / 'events.json').read_text(encoding='utf-8-sig'))
    phases = [row for row in events if row['Kind'] == 'phase']
    if [row['Name'] for row in phases] != ['initialize', 'primer', 'warmup', 'measured', 'finalize']:
        raise ValueError('Missing or unordered phase markers')
    start, end = phases[3]['Milliseconds'], phases[4]['Milliseconds']
    measured_events = [row for row in events if row['Kind'] == 'jit' and start <= row['Milliseconds'] < end]
    save(output / 'summary.json', dict(verdict='INCONCLUSIVE: startup diagnosis only; no product controls',
                                     phases=summary, measured_jit=measured_events))
    save(output / 'inventory.json', [dict(path=str(path.relative_to(output)), bytes=path.stat().st_size,
                                         sha256=hashlib.sha256(path.read_bytes()).hexdigest())
                                     for path in sorted(output.rglob('*')) if path.is_file()])


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', required=True)
    parser.add_argument('--tracer', required=True)
    parser.add_argument('--case', default='unregistered:16')
    parser.add_argument('--smoke', action='store_true')
    execute(parser.parse_args())
