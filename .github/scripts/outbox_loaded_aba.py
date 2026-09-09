"""Real Kafka outbox observations; successful collection does not set a PR gate."""
import importlib.util
from datetime import datetime, timezone
import json
import math
import os
from pathlib import Path
import shutil
import struct
import subprocess
import threading

CONFIGS = [(store, listener) for store in ('legacy', 'renewal') for listener in ('off', 'on')]
WARMUP = 180
MEASURED = 180


def configurations(shard='all'):
    selected = [case for case in CONFIGS if shard == 'all' or '-'.join(case) == shard]
    if not selected:
        raise ValueError(f'Unknown outbox shard: {shard}')
    return selected


def execution_plan(adjacent=False, shard='all'):
    configs = configurations(shard)
    for phase, label in [('DryA', 'A'), ('DryB', 'B')]:
        for store, listener in configs:
            yield phase, label, True, store, listener
    phases = [('A1', 'A'), ('B', 'B'), ('A2', 'A')]
    if adjacent:
        for store, listener in configs:
            for phase, label in phases:
                yield phase, label, False, store, listener
    else:
        for phase, label in phases:
            for store, listener in configs:
                yield phase, label, False, store, listener


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def validate_phase(folder, name, seconds):
    data = json.loads((folder / f'{name}.json').read_text(encoding='utf-8'))
    raw = (folder / f'{name}-cycles.bin').read_bytes()
    if len(raw) != data['Cycles'] * 16 or data['Cycles'] <= 0:
        raise ValueError('Missing raw batch latency samples')
    cycles = list(struct.iter_unpack('<qq', raw))
    if data['Completed'] != len(cycles) * 500 or data['BatchCount'] != 500:
        raise ValueError('Incorrect completion denominator')
    if data['Seconds'] < seconds or data['RequestedSeconds'] != seconds:
        raise ValueError('Insufficient elapsed phase duration')
    if data['Start']['Pending'] != 0 or data['End']['Pending'] != 0:
        raise ValueError('Phase boundary has leftover work')
    if data['End']['Completed'] - data['Start']['Completed'] != data['Completed']:
        raise ValueError('Completed counts differ')
    duration = (data['End']['Timestamp'] - data['Start']['Timestamp']) / data['StopwatchFrequency']
    expected_metrics = {
        'Seconds': duration,
        'MessagesPerSecond': data['Completed'] / duration,
        'CpuNsPerMessage': (data['End']['CpuTicks'] - data['Start']['CpuTicks']) * 100 / data['Completed'],
        'AllocatedBytesPerMessage': (data['End']['Allocated'] - data['Start']['Allocated']) / data['Completed'],
    }
    for metric, expected in expected_metrics.items():
        if not math.isclose(data[metric], expected, rel_tol=1e-12):
            raise ValueError(f'Incorrect measurement boundary statistic {metric}')
    previous = data['Start']['Timestamp']
    latencies = []
    for start, end in cycles:
        if not previous <= start < end <= data['End']['Timestamp']:
            raise ValueError('Invalid or overlapping batch boundaries')
        previous = end
        latencies.append(end - start)
    latencies.sort()
    for metric, fraction in [('P50Ns', .5), ('P99Ns', .99), ('MaxNs', 1)]:
        expected = latencies[math.ceil(len(latencies) * fraction) - 1] * 1e9 / data['StopwatchFrequency']
        if not math.isclose(data[metric], expected, rel_tol=1e-12):
            raise ValueError(f'Incorrect raw latency statistic {metric}')
    for metric in ('MessagesPerSecond', 'CpuNsPerMessage', 'AllocatedBytesPerMessage', 'P50Ns', 'P99Ns', 'MaxNs'):
        if not math.isfinite(data[metric]) or data[metric] <= 0:
            raise ValueError(f'Missing protected metric {metric}')
    return data


def validate(folder, warmup, measured, candidate, listener):
    phases = {name: validate_phase(folder, name, seconds)
              for name, seconds in [('primer', 20), ('warmup', warmup), ('measured', measured)]}
    completion = json.loads((folder / 'completion.json').read_text(encoding='utf-8'))
    if completion['Error'] is not None or completion['Pending'] or completion['Failures']:
        raise ValueError('Failed, incomplete or leftover work')
    if completion['TotalCompleted'] != sum(p['Completed'] for p in phases.values()):
        raise ValueError('Phases do not cover all completed work')
    if completion['ShutdownSeconds'] > 30:
        raise ValueError('Shutdown exceeded its deadline')
    if candidate and listener == 'on':
        if completion['Acknowledged'] != completion['TotalCompleted'] or min(completion['MetricQueries'], completion['GaugeSamples']) <= 0:
            raise ValueError('Missing or incorrect active telemetry coverage')
    elif completion['MetricQueries'] or completion['Acknowledged'] or completion['GaugeSamples']:
        raise ValueError('Unexpected telemetry in baseline/disabled mode')
    samples = json.loads((folder / 'series.json').read_text(encoding='utf-8'))
    if len(samples) < 20 + warmup + measured - 3:
        raise ValueError('Missing runtime time series')
    for sample in samples:
        for key in ('Timestamp', 'CpuTicks', 'Allocated', 'Completed', 'Pending', 'Gen0', 'Gen1', 'Gen2',
                    'HeapBytes', 'RssBytes', 'Threads', 'PendingWork'):
            if not math.isfinite(sample[key]) or sample[key] < 0:
                raise ValueError(f'Missing runtime metric {key}')
    return {'phases': phases, 'completion': completion, 'runtime_samples': len(samples)}


def execute():
    root = Path.cwd()
    out = root / 'evidence'
    out.mkdir(exist_ok=False)
    adjacent = os.environ.get('OUTBOX_ADJACENT') == '1'
    shard = os.environ.get('PERFORMANCE_SHARD', 'all')
    configs = configurations(shard)
    declared_warmup = 480 if adjacent else WARMUP
    dispatch = module(root / '.github/benchmarks/dispatch-aba/run.py', 'outbox_dispatch')
    pool = module(root / '.github/scripts/pool_loaded_aba.py', 'outbox_broker')
    topology = dispatch.configure_affinity()
    a, b = os.environ['BASELINE_SHA'], os.environ['CANDIDATE_SHA']
    dispatch.command(['git', 'merge-base', '--is-ancestor', a, b], out / 'ancestry.log')
    if os.environ.get('PERFORMANCE_CAMPAIGN'):
        from performance_shards import validate_campaign
        campaign = json.loads(Path(os.environ['PERFORMANCE_CAMPAIGN']).read_text())
        pins = validate_campaign(campaign, os.environ['HARNESS_SHA'], a, b, int(os.environ['PR']), os.environ['SUITE'])
        main = pins['main_at_start']
    else:
        main = subprocess.check_output(['git', 'ls-remote', 'origin', 'refs/heads/main'], text=True).split()[0]
        if main != a:
            raise ValueError('Main moved before this new campaign; rebase and repin')
    plan = {'A': a, 'B': b, 'harness': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
            'main_at_start': main, 'primer_seconds': 20, 'warmup_seconds': declared_warmup, 'measured_seconds': MEASURED,
            'adjacent_controls': adjacent or shard != 'all', 'execution_plan': list(execution_plan(adjacent, shard)),
            'configs': configs, 'shard': shard, 'runner': 'ubuntu-latest', 'image': os.getenv('ImageVersion'),
            'topology': topology, 'affinity': dispatch.AFFINITY.copy(),
            'runtime': {'TieredCompilation': '1', 'TieredPGO': '1', 'ReadyToRun': '1', 'ServerGC': True},
            'run_url': f'https://github.com/{os.environ["GITHUB_REPOSITORY"]}/actions/runs/{os.environ["GITHUB_RUN_ID"]}',
            'verdict': 'INCONCLUSIVE until all protected metrics and uncertainty are assessed'}
    (out / 'plan.json').write_text(json.dumps(plan, indent=2), encoding='utf-8')
    dispatch.command(['dotnet', '--info'], out / 'runtime.log')
    dispatch.command(['lscpu'], out / 'hardware.log')
    dispatch.command(['git', 'archive', '--format=zip', f'--output={out / "harness.zip"}', 'HEAD'], out / 'harness-archive.log')
    hosts = {}
    fixtures = root / '.github/benchmarks/outbox-loaded'
    for label, sha in [('A', a), ('B', b)]:
        product = out / f'product-{label}'
        dispatch.command(['git', 'worktree', 'add', '--detach', product, sha], out / f'checkout-{label}.log')
        fixture = out / f'fixture-{label}' / 'outbox-loaded'
        shutil.copytree(fixtures, fixture, ignore=shutil.ignore_patterns('bin', 'obj'))
        for name in ('global.json', 'Directory.Packages.props'):
            shutil.copyfile(root / name, fixture / name)
        dispatch.command(['dotnet', 'build', fixture / 'Harness.csproj', '-c', 'Release', '--disable-build-servers',
                          f'-p:ProductProject={product}/src/Dekaf.Outbox/Dekaf.Outbox.csproj',
                          f'-p:FixtureSource={fixture}', f'-p:CandidateFixture={str(label == "B").lower()}'],
                         out / f'build-{label}.log', cwd=fixture)
        hosts[label] = fixture / 'bin/Release/net10.0/Harness.dll'
    dispatch.command(['dotnet', 'build-server', 'shutdown'], out / 'build-server-shutdown.log')
    bindings = {str(file): dispatch.digest(file) for host in hosts.values() for file in host.parent.iterdir() if file.is_file()}
    (out / 'bindings.json').write_text(json.dumps(bindings, indent=2), encoding='utf-8')
    observations = {}
    order = []
    for phase, label, smoke, store, listener in execution_plan(adjacent, shard):
        if any(dispatch.digest(Path(path)) != digest for path, digest in bindings.items()):
            raise ValueError('Measurement inputs changed')
        observations.setdefault(phase, {})
        key = f'{store}-{listener}'
        folder = out / phase / key
        folder.mkdir(parents=True)
        broker = f'outbox-{phase.lower()}-{key}'
        stop = threading.Event()
        errors = []
        sampler = None
        entry = dict(phase=phase, label=label, smoke=smoke, case=key,
                     started_utc=datetime.now(timezone.utc).isoformat())
        order.append(entry)
        (out/'capture-order.json').write_text(json.dumps(order, indent=2))
        try:
            dispatch.broker_start(folder, broker, pool.BROKER_RETENTION)
            sampler = threading.Thread(target=pool.broker_samples, args=(broker, folder / 'broker-stats.jsonl', stop, errors))
            sampler.start()
            warmup, measured = (2, 2) if smoke else (declared_warmup, MEASURED)
            dispatch.command(['taskset', '-c', dispatch.AFFINITY['consumer'], 'dotnet', hosts[label],
                              'localhost:9092', folder / 'client', broker, store, listener, warmup, measured],
                             folder / 'client.log', timeout=20 + warmup + measured + 150,
                             env=dict(os.environ, DOTNET_TieredCompilation='1', DOTNET_TieredPGO='1', DOTNET_ReadyToRun='1'))
            observations[phase][key] = validate(folder / 'client', warmup, measured, label == 'B', listener)
            (out / 'collection.json').write_text(json.dumps(observations, indent=2), encoding='utf-8')
        finally:
            stop.set()
            try:
                if sampler is not None:
                    sampler.join(timeout=15)
                    if sampler.is_alive() or errors:
                        raise RuntimeError(f'Broker sampling failed: {errors}')
            finally:
                dispatch.broker_stop(folder, broker)
        entry['completed_utc'] = datetime.now(timezone.utc).isoformat()
        (out/'capture-order.json').write_text(json.dumps(order, indent=2))


if __name__ == '__main__':
    try:
        execute()
    finally:
        folder = Path.cwd() / 'evidence'
        if folder.exists():
            import hashlib
            inventory = {}
            for path in folder.rglob('*'):
                if path.is_file():
                    with path.open('rb') as stream:
                        inventory[str(path.relative_to(folder))] = hashlib.file_digest(stream, 'sha256').hexdigest()
            (folder / 'sha256.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
