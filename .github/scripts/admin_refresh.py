"""Run existing administrative probes against exact products on one runner."""
import hashlib
import importlib.util
import json
import os
from datetime import datetime, timezone
from pathlib import Path
import shutil
import subprocess
import sys

from runner_affinity import configure_affinity

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
    calibration = os.getenv('ADMIN_CALIBRATION') == '1'
    if calibration and A != B:
        raise ValueError('Calibration requires identical exact product SHAs')
    pilot = os.getenv('ADMIN_PILOT') == '1'
    # Administrative calls get a shorter capture budget than sustained message
    # pipelines. Keep elapsed warmup above the 20-second repository minimum and
    # use the same settings for comparisons, calibration and the one-case pilot.
    warmup_seconds = 30
    measured_seconds = 30
    if calibration:
        added = []
    if pilot:
        # Limit the pilot to one workload before expanding a campaign.
        controls, added = controls[:1], []
    environment = dict(os.environ, DOTNET_TieredCompilation='1', DOTNET_TieredPGO='1', DOTNET_gcServer='0')
    topology, affinity = configure_affinity()
    cpu = max(map(int, affinity['consumer'].split(',')))
    probe_prefix = ['taskset', '-c', str(cpu), 'dotnet']
    plan = dict(calibration=calibration, topology=topology, affinity=affinity, A=A, B=B, harness=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
                controls=controls, candidate_only=added, warmup_seconds=warmup_seconds, measured_seconds=measured_seconds,
                warmup_rationale='30 seconds continuous workload for administrative calls, above the 20-second minimum; assess throughput and latency trends',
                pilot=pilot, report_aggregation='all overflow sorting and aggregation deferred until both captures finish',
                histograms='Exact ticks, touched buckets only; 65536 distinct ticks/interval, shared archive capped at 4194304 entries (64 MiB); exhaustion invalidates',
                phase_transition='one continuous warmed call loop; no return/re-entry between warmup and measurement',
                cpu_affinity=[cpu], diagnostics='Manual standard tools, separate from timing',
                phase_order='For each control workload, run A1 then B then A2 before starting the next workload',
                endpoint='127.0.0.1:9092; literal loopback avoids external OS DNS in cached-transport probes',
                image=os.getenv('ImageOS'), image_version=os.getenv('ImageVersion'), run_id=os.getenv('GITHUB_RUN_ID'),
                scope='Cached-transport completed administrative calls; no network/broker acceptance.',
                verdict='INCONCLUSIVE: partial scope; assess all retained runtime and metric evidence')
    save(OUT / 'plan.json', plan)
    run(['git', 'merge-base', '--is-ancestor', A, B], OUT / 'ancestry.log')
    run(['dotnet', '--info'], OUT / 'dotnet-info.txt')
    run(['lscpu'], OUT / 'hardware.txt')
    run(['git', 'archive', '--format=zip', '--output=' + str(OUT / 'harness.zip'), 'HEAD'], OUT / 'archive-harness.log')
    hosts = {}
    revisions = [('A', A)] if calibration else [('A', A), ('B', B)]
    for label, sha in revisions:
        product = ROOT / ('product-' + label)
        run(['git', 'worktree', 'add', '--detach', str(product), sha], OUT / f'checkout-{label}.log')
        run(['git', 'archive', '--format=zip', '--output=' + str(OUT / f'product-{label}.zip'), sha], OUT / f'archive-{label}.log')
        fixture = product / 'tools' / PROJECTS[PR]
        fixture.mkdir(parents=True, exist_ok=True)
        for source in SOURCE.iterdir():
            if source.suffix in {'.cs', '.csproj'}:
                shutil.copy2(source, fixture / source.name)
        run(['dotnet', 'build', str(fixture / 'Runner.csproj'), '-c', 'Release', '--disable-build-servers',
             '-p:Candidate=' + str(label == 'B').lower()], OUT / f'build-{label}.log', cwd=product)
        binary = fixture / 'bin/Release/net10.0/Dekaf.Benchmarks.dll'
        hosts[label] = binary
        shutil.copytree(binary.parent, OUT / 'binaries' / label)
        common.verify_copied_tree(binary.parent, OUT / 'binaries' / label)
    run(['dotnet', 'build-server', 'shutdown'], OUT / 'build-server-shutdown.log')
    if calibration:
        hosts['B'] = hosts['A']
        shutil.copytree(OUT / 'binaries/A', OUT / 'binaries/B')
    for label, binary in hosts.items():
        for case in controls + (added if label == 'B' else []):
            destination = OUT / 'validation' / label / case.replace(':', '-')
            run(probe_prefix + [str(binary), 'probe', case, str(destination), '.2', '.2'], destination / 'run.log', env=environment)
            validator(destination / 'measured.json', .2)
            common.retain_loaded_binaries(destination / 'binaries.json', binary.parent, OUT / 'binaries' / label)
    observations = {phase: {} for phase in ['A1', 'B', 'A2']}
    captures = []
    # Keep each candidate close to its own controls. Running every A1 workload
    # first separated matching controls by up to an hour in the full matrix.
    for case in controls:
        for phase, label in [('A1', 'A'), ('B', 'B'), ('A2', 'A')]:
            destination = OUT / phase / case.replace(':', '-')
            binary = hosts[label]
            capture = dict(case=case, phase=phase, product=A if label == 'A' else B, binary=str(binary),
                           started_utc=datetime.now(timezone.utc).isoformat())
            run(probe_prefix + [str(binary), 'probe', case, str(destination), str(warmup_seconds), str(measured_seconds)], destination / 'run.log', env=environment)
            capture['completed_utc'] = datetime.now(timezone.utc).isoformat()
            captures.append(capture)
            save(OUT / 'capture-order.json', captures)
            validator(destination / 'warmup.json', warmup_seconds)
            observations[phase][case] = validator(destination / 'measured.json', measured_seconds)
            common.retain_loaded_binaries(destination / 'binaries.json', binary.parent, OUT / 'binaries' / label)
    comparisons = {case: common.compare(*(observations[phase][case] for phase in ['A1', 'B', 'A2'])) for case in controls}
    save(OUT / 'comparison.json', comparisons)
    if calibration:
        save(OUT / 'calibration.json', {
            'product_sha': A, 'binary': str(hosts['A']),
            'point_estimates_within_declared_limits': all(row['point_estimates_within_declared_limits'] for row in comparisons.values()),
            'verdict': 'DIAGNOSTIC ONLY: identical-product repeatability; never product acceptance',
            'comparisons': comparisons})
    for case in added:
        destination = OUT / 'candidate-only' / case.replace(':', '-')
        run(probe_prefix + [str(hosts['B']), 'probe', case, str(destination), str(warmup_seconds), str(measured_seconds)], destination / 'run.log', env=environment)
        validator(destination / 'warmup.json', warmup_seconds)
        validator(destination / 'measured.json', measured_seconds)
        common.retain_loaded_binaries(destination / 'binaries.json', hosts['B'].parent, OUT / 'binaries/B')


if __name__ == '__main__':
    try:
        execute()
    finally:
        if OUT.exists():
            save(OUT / 'inventory.json', [dict(path=str(p.relative_to(OUT)), bytes=p.stat().st_size,
                                              sha256=hashlib.sha256(p.read_bytes()).hexdigest())
                                          for p in OUT.rglob('*') if p.is_file() and p.name != 'inventory.json'])
