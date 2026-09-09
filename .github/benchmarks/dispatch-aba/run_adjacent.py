"""Full dispatch comparison with adjacent controls for each exact workload."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import subprocess

import run

WARMUP = 361
DURATION = 180


def schedule(loaded_only=False):
    cases = [] if loaded_only else [('micro', f'{pattern}-{batch}', (pattern, batch)) for pattern, batch in run.CASES]
    cases += [('loaded', mode, mode) for mode in run.MODES]
    if not loaded_only:
        cases += [('shutdown', f'batch-{batch}-keys-{keys}', (batch, keys))
                  for batch in (1, 16) for keys in (1, 2)]
    # Validate every fixture against both revisions before the first measurement.
    for phases, smoke in [([('DryA', 'A'), ('DryB', 'B')], True),
                          ([('A1', 'A'), ('B', 'B'), ('A2', 'A')], False)]:
        for kind, name, value in cases:
            for phase, label in phases:
                yield dict(kind=kind, name=name, value=value, phase=phase, label=label, smoke=smoke)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--loaded-only', action='store_true',
                        help='Measure all public Kafka modes; private dispatch/shutdown scope remains missing')
    args = parser.parse_args()
    for sha in (args.baseline, args.candidate):
        if not re.fullmatch('[0-9a-f]{40}', sha):
            raise ValueError('Exact product SHA required')
    if os.environ.get('DISPATCH_STAGE_TIMING') == '1':
        raise ValueError('Full adjacent comparison uses the normal fixture without stage timing')
    subprocess.run(['git', 'merge-base', '--is-ancestor', args.baseline, args.candidate], check=True)
    root, output = Path.cwd(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    topology = run.configure_affinity()
    plan = list(schedule(args.loaded_only))
    provenance = dict(baseline=args.baseline, candidate=args.candidate,
                      harness=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
                      cpu_core_socket=topology, affinity=run.AFFINITY,
                      runner_image=os.environ.get('ImageVersion'), runner_os=os.environ.get('ImageOS'),
                      run_url=f"https://github.com/{os.environ.get('GITHUB_REPOSITORY')}/actions/runs/{os.environ.get('GITHUB_RUN_ID')}",
                      phases=['A1', 'B', 'A2'], loaded_warmup_seconds=WARMUP,
                      loaded_duration_seconds=DURATION, loaded_modes=run.MODES,
                      loaded_only=args.loaded_only, handler_stage_timing=False, partial_loaded_scope=False,
                      focused_dispatcher_shutdown_measured=not args.loaded_only, adjacent_controls=True)
    (output/'provenance.json').write_text(json.dumps(provenance, indent=2))
    (output/'execution-plan.json').write_text(json.dumps(plan, indent=2))
    run.command(['dotnet', '--info'], output/'dotnet-info.log')
    run.command(['lscpu'], output/'hardware.log')
    hosts = {label: run.build(root, output, sha, label, loaded_only=args.loaded_only)
             for label, sha in [('A', args.baseline), ('B', args.candidate)]}
    loaded_hosts = {label: values['Loaded'] for label, values in hosts.items()}
    run.command(['dotnet', 'build-server', 'shutdown'], output/'build-server-shutdown.log')
    loaded = {phase: {} for phase in ['A1', 'B', 'A2']}
    shutdown = {phase: {} for phase in ['DryA', 'DryB', 'A1', 'B', 'A2']}
    order = []
    try:
        for item in plan:
            phase, label, smoke = item['phase'], item['label'], item['smoke']
            phase_folder = output / phase
            phase_folder.mkdir(exist_ok=True)
            entry = dict(**item, started_utc=datetime.now(timezone.utc).isoformat())
            order.append(entry)
            (output/'capture-order.json').write_text(json.dumps(order, indent=2))
            if item['kind'] == 'micro':
                run.micro(hosts[label]['Harness'], output, label, phase, smoke, cases=[item['value']])
            elif item['kind'] == 'loaded':
                mode = item['value']
                broker = f'dispatch-{phase.lower()}-{mode}'
                broker_folder = phase_folder / ('broker-' + mode)
                broker_folder.mkdir()
                folder = phase_folder / f'{phase}-{mode}'
                warmup, duration = (2, 2) if smoke else (WARMUP, DURATION)
                rate = 1000 if smoke or mode.startswith('pending') else 50000
                try:
                    run.broker_start(broker_folder, broker)
                    metrics = run.workload(loaded_hosts, label, folder, mode, warmup, duration, rate, broker)
                    if not smoke:
                        metrics = run.validate_loaded(folder, warmup, duration, rate, acceptance=True)
                        if metrics['ActualWarmupSeconds'] < 360:
                            raise ValueError('Completed warmup is shorter than the declared 360 seconds')
                    run.latency_series(folder, metrics)
                finally:
                    run.broker_stop(broker_folder, broker)
                if not smoke:
                    loaded[phase][mode] = metrics
                    (output/'loaded-metrics.json').write_text(json.dumps(loaded, indent=2))
            else:
                batch, keys = item['value']
                metrics = run.shutdown(hosts[label]['Harness'], phase_folder/('shutdown-'+item['name']), batch, keys, smoke)
                shutdown[phase][item['name']] = metrics
                (phase_folder/'shutdown-metrics.json').write_text(json.dumps(shutdown[phase], indent=2))
            entry['completed_utc'] = datetime.now(timezone.utc).isoformat()
            (output/'capture-order.json').write_text(json.dumps(order, indent=2))
        (output/'decision.json').write_text(json.dumps(dict(measurement='COMPLETE', acceptance='INCONCLUSIVE',
            reason='Review all protected metrics, runtime transitions, controls and scope before acceptance.'), indent=2))
    finally:
        (output/'sha256.json').write_text(json.dumps({str(path.relative_to(output)): run.digest(path)
                                                    for path in output.rglob('*') if path.is_file()}, indent=2))


if __name__ == '__main__':
    main()
