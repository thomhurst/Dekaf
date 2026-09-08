"""Run existing administrative probes against exact products on one runner."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path.cwd()
OUT = ROOT / 'evidence'
PR = int(os.environ['PR'])
A = os.environ['BASELINE_SHA']
B = os.environ['CANDIDATE_SHA']
PROJECTS = {3128: 'AdminDescriptionEvidence', 3138: 'AdminMutationEvidence', 3136: 'AdminShareOffsetEvidence', 3129: 'AdminMemberRemovalEvidence'}
SOURCE = ROOT / 'tools' / PROJECTS[PR]


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding='utf-8')


def run(command, log, cwd=ROOT, env=None):
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open('w', encoding='utf-8') as stream:
        subprocess.run(command, cwd=cwd, env=env, stdout=stream, stderr=subprocess.STDOUT, check=True)


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def execute():
    OUT.mkdir(exist_ok=False)
    common = module(ROOT / 'tools/AdminShareOffsetEvidence/run_comparison.py', 'common')
    if PR == 3138:
        validator = module(SOURCE / 'validate_results.py', 'validation').validate
        controls = ['legacy-create:16', 'legacy-delete:16']
        added = ['create:1', 'create:16', 'mixed:16', 'retry:16', 'delete:16', 'expand:16', 'reassign:16', 'reassign-retry:16', 'wrapped:16', 'disconnected:16', 'disposed:16', 'unregistered:16']
    else:
        original = module(SOURCE / 'run_comparison.py', 'original')
        validator = original.validate_probe
        controls, added = original.CONTROLS, original.NEW_CASES
    pilot = os.getenv('ADMIN_PILOT') == '1'
    if pilot:
        # Diagnose the observed report/JIT transition before expanding a campaign.
        controls, added = controls[:1], []
    environment = dict(os.environ, DOTNET_TieredCompilation='1', DOTNET_TieredPGO='1', DOTNET_gcServer='0')
    cpu = max(os.sched_getaffinity(0))
    probe_prefix = ['taskset', '-c', str(cpu), 'dotnet']
    plan = dict(A=A, B=B, harness=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
                controls=controls, candidate_only=added, warmup_seconds=180, measured_seconds=60,
                pilot=pilot, report_aggregation='deferred until after both captures; no warmup aggregate before measurement',
                cpu_affinity=[cpu], jit_attribution='CLR MethodJittingStarted; identical observer in all phases',
                image=os.getenv('ImageOS'), image_version=os.getenv('ImageVersion'), run_id=os.getenv('GITHUB_RUN_ID'),
                scope='Cached-transport completed administrative calls; no network/broker acceptance.',
                verdict='INCONCLUSIVE: partial scope; assess all retained runtime and metric evidence')
    save(OUT / 'plan.json', plan)
    run(['git', 'merge-base', '--is-ancestor', A, B], OUT / 'ancestry.log')
    run(['dotnet', '--info'], OUT / 'dotnet-info.txt')
    run(['lscpu'], OUT / 'hardware.txt')
    run(['git', 'archive', '--format=zip', '--output=' + str(OUT / 'harness.zip'), 'HEAD'], OUT / 'archive-harness.log')
    hosts = {}
    for label, sha in [('A', A), ('B', B)]:
        product = ROOT / ('product-' + label)
        run(['git', 'worktree', 'add', '--detach', str(product), sha], OUT / f'checkout-{label}.log')
        run(['git', 'archive', '--format=zip', '--output=' + str(OUT / f'product-{label}.zip'), sha], OUT / f'archive-{label}.log')
        fixture = product / 'tools' / PROJECTS[PR]
        fixture.mkdir(parents=True, exist_ok=True)
        for source in SOURCE.iterdir():
            if source.suffix in {'.cs', '.csproj'}:
                shutil.copy2(source, fixture / source.name)
        shutil.copy2(ROOT / '.github/benchmarks/CompilationLog.cs', fixture / 'CompilationLog.cs')
        run(['dotnet', 'build', str(fixture / 'Runner.csproj'), '-c', 'Release', '--disable-build-servers',
             '-p:Candidate=' + str(label == 'B').lower()], OUT / f'build-{label}.log', cwd=product)
        binary = fixture / 'bin/Release/net10.0/Dekaf.Benchmarks.dll'
        hosts[label] = binary
        shutil.copytree(binary.parent, OUT / 'binaries' / label)
        common.verify_copied_tree(binary.parent, OUT / 'binaries' / label)
    run(['dotnet', 'build-server', 'shutdown'], OUT / 'build-server-shutdown.log')
    for label, binary in hosts.items():
        for case in controls + (added if label == 'B' else []):
            destination = OUT / 'validation' / label / case.replace(':', '-')
            run(probe_prefix + [str(binary), 'probe', case, str(destination), '.2', '.2'], destination / 'run.log', env=environment)
            validator(destination / 'measured.json', .2)
            common.retain_loaded_binaries(destination / 'binaries.json', binary.parent, OUT / 'binaries' / label)
    observations = {}
    for phase, label in [('A1', 'A'), ('B', 'B'), ('A2', 'A')]:
        observations[phase] = {}
        for case in controls:
            destination = OUT / phase / case.replace(':', '-')
            binary = hosts[label]
            run(probe_prefix + [str(binary), 'probe', case, str(destination), '180', '60'], destination / 'run.log', env=environment)
            validator(destination / 'warmup.json', 180)
            observations[phase][case] = validator(destination / 'measured.json', 60)
            common.retain_loaded_binaries(destination / 'binaries.json', binary.parent, OUT / 'binaries' / label)
    save(OUT / 'comparison.json', {case: common.compare(*(observations[phase][case] for phase in ['A1', 'B', 'A2'])) for case in controls})
    for case in added:
        destination = OUT / 'candidate-only' / case.replace(':', '-')
        run(probe_prefix + [str(hosts['B']), 'probe', case, str(destination), '180', '60'], destination / 'run.log', env=environment)
        validator(destination / 'warmup.json', 180)
        validator(destination / 'measured.json', 60)
        common.retain_loaded_binaries(destination / 'binaries.json', hosts['B'].parent, OUT / 'binaries/B')


if __name__ == '__main__':
    try:
        execute()
    finally:
        if OUT.exists():
            save(OUT / 'inventory.json', [dict(path=str(p.relative_to(OUT)), bytes=p.stat().st_size,
                                              sha256=hashlib.sha256(p.read_bytes()).hexdigest())
                                          for p in OUT.rglob('*') if p.is_file() and p.name != 'inventory.json'])

