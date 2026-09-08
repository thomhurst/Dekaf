"""Collect paired loaded pool evidence; successful collection is not acceptance."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import threading
import time
from datetime import datetime

ROOT = Path.cwd()
OUT = ROOT / 'evidence'
CONFIGS = [(1000, 3), (65536, 1)]
WARMUP_SECONDS = 660
MEASURED_SECONDS = 300
PROFILE_WINDOWS = [(10, 30, 'early'), (240, 30, 'late')]


def profile_windows(output, stop, errors, dispatch, trace):
    """Attach only during declared diagnostic windows; preserve every trace."""
    try:
        marker = output / 'measured-start.json'
        while not marker.exists():
            if stop.wait(0.1):
                raise RuntimeError('Client stopped before profiling measurement marker')
        # The marker is written synchronously before measured CPU/allocation boundaries.
        for attempt in range(20):
            try:
                start = json.loads(marker.read_text())
                break
            except json.JSONDecodeError:
                if stop.wait(0.05) or attempt == 19:
                    raise
        epoch = datetime.fromisoformat(start['StartedUtc']).timestamp()
        for offset, duration, label in PROFILE_WINDOWS:
            if stop.wait(max(0, epoch + offset - time.time())):
                raise RuntimeError('Client stopped before all profiling windows')
            dispatch.command(['taskset', '-c', dispatch.AFFINITY['infrastructure'], trace,
                'collect', '--process-id', start['ProcessId'], '--profile', 'gc-verbose',
                '--duration', f'00:00:{duration:02}', '--buffersize', '256',
                '--output', output / f'{label}.nettrace'], output / f'{label}-trace.log', timeout=90)
    except Exception as error:
        errors.append(error)
BROKER_RETENTION = (
    'KAFKA_LOG_RETENTION_BYTES=1073741824',
    'KAFKA_LOG_SEGMENT_BYTES=16777216',
    'KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS=1000',
    'KAFKA_LOG_SEGMENT_DELETE_DELAY_MS=1000',
)


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def broker_samples(name, path, stop, errors):
    # Docker observations retain broker CPU/memory separately from client process
    # accounting. Receipt times identify the observation window; percentages are
    # Docker observations, not substituted for client CPU/message.
    try:
        with path.open('w') as log:
            while not stop.is_set():
                result = subprocess.run(['docker', 'stats', '--no-stream', '--format', '{{json .}}', name],
                                        capture_output=True, text=True, check=True, timeout=10)
                observation = json.loads(result.stdout)
                if not observation.get('CPUPerc') or not observation.get('MemUsage'):
                    raise ValueError('Missing broker CPU/memory observation')
                log.write(json.dumps({'utc_ns': time.time_ns(), 'observation': observation,
                                      'runner_disk_free_bytes': shutil.disk_usage(ROOT).free}) + '\n')
                log.flush()
                stop.wait(1)
    except Exception as error:
        errors.append(error)


def execute():
    OUT.mkdir(exist_ok=False)
    dispatch = module(ROOT / '.github/benchmarks/dispatch-aba/run.py', 'dispatch_driver')
    analyzer = module(ROOT / '.github/benchmarks/pool-loaded/analyze-blocks.py', 'block_analyzer')
    topology = dispatch.configure_affinity()
    profiling = os.getenv('POOL_PROFILE') == '1'
    configs = [(65536, 1)] if profiling else CONFIGS
    trace = None
    if profiling:
        trace_version = json.loads((ROOT / '.config/stress-diagnostics/dotnet-tools.json').read_text())['tools']['dotnet-trace']['version']
        tool_path = OUT / 'diagnostic-tools'
        dispatch.command(['dotnet', 'tool', 'install', 'dotnet-trace', '--tool-path', tool_path,
                          '--version', trace_version], OUT / 'trace-install.log')
        trace = tool_path / 'dotnet-trace'
        dispatch.command([trace, '--version'], OUT / 'trace-version.log')
    a, b = os.environ['BASELINE_SHA'], os.environ['CANDIDATE_SHA']
    dispatch.command(['git', 'merge-base', '--is-ancestor', a, b], OUT / 'ancestry.log')
    plan = {'A': a, 'B': b, 'harness': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
            'primer_seconds': 20, 'warmup_seconds': WARMUP_SECONDS, 'measured_seconds': MEASURED_SECONDS, 'configurations': configs,
            'warmup_reason': 'Exercise default ten-minute bootstrap connection idle retirement before measured collection',
            'profiling': {'purpose': 'allocation/GC transition diagnosis; not acceptance',
                          'profile': 'gc-verbose', 'windows': PROFILE_WINDOWS, 'version': trace_version} if profiling else None,
            'setup_admin': 'disposed before primer; successful disposal observed separately',
            'reporting': 'histogram snapshots and JSON serialization deferred until after all phases finish',
            'broker_retention': BROKER_RETENTION,
            'cpu_core_socket': topology, 'affinity': dispatch.AFFINITY.copy(),
            'runner': 'ubuntu-latest', 'image': os.getenv('ImageVersion'), 'runtime': {
                'TieredCompilation': '1', 'TieredPGO': '1', 'ReadyToRun': '1', 'ServerGC': True},
            'scope': 'Closed-loop producer and consumer completion; interval distributions and runtime attribution',
            'acceptance': 'NOT_EVALUATED; uncertainty, cold reset and failure/shutdown scope require review'}
    (OUT / 'plan.json').write_text(json.dumps(plan, indent=2))
    dispatch.command(['dotnet', '--info'], OUT / 'runtime.log')
    dispatch.command(['lscpu'], OUT / 'hardware.log')
    dispatch.command(['git', 'archive', '--format=zip', f'--output={OUT / "harness.zip"}', 'HEAD'], OUT / 'harness-archive.log')
    hosts = {}
    for label, sha in [('A', a), ('B', b)]:
        product = OUT / f'product-{label}'
        dispatch.command(['git', 'worktree', 'add', '--detach', product, sha], OUT / f'checkout-{label}.log')
        fixture = product / '.github/benchmarks/pool-loaded'
        shutil.copytree(ROOT / '.github/benchmarks/pool-loaded', fixture, dirs_exist_ok=True,
                        ignore=shutil.ignore_patterns('bin', 'obj'))
        shutil.copyfile(ROOT / '.github/benchmarks/CompilationLog.cs', fixture.parent / 'CompilationLog.cs')
        dispatch.command(['dotnet', 'build', fixture / 'Harness.csproj', '-c', 'Release', '--disable-build-servers'],
                         OUT / f'build-{label}.log', cwd=product)
        hosts[label] = fixture / 'bin/Release/net10.0/Dekaf.Benchmarks.dll'
    if os.environ['PR'] == '3137':
        module(ROOT / '.github/scripts/reservoir_fixture.py', 'reservoir_fixture').validate(
            OUT / 'product-B', OUT / 'product-A')
    else:
        inputs = module(ROOT / '.github/scripts/stress_fixtures.py', 'stress_fixtures')
        for name in inputs.BUILD_INPUTS:
            paths = [OUT / f'product-{label}' / name for label in ('A', 'B')]
            contents = [p.read_bytes() if p.exists() else None for p in paths]
            if contents[0] != contents[1]:
                raise ValueError(f'Product build inputs differ: {name}')
    dispatch.command(['dotnet', 'build-server', 'shutdown'], OUT / 'build-server-shutdown.log')
    bindings = {str(file): dispatch.digest(file) for binary in hosts.values() for file in binary.parent.glob('*.dll')}
    (OUT / 'bindings.json').write_text(json.dumps(bindings, indent=2))
    observations = {}
    for phase, label, smoke in [('DryA', 'A', True), ('DryB', 'B', True),
                               ('A1', 'A', False), ('B', 'B', False), ('A2', 'A', False)]:
        if any(dispatch.digest(Path(path)) != digest for path, digest in bindings.items()):
            raise ValueError('Measured binaries changed')
        observations[phase] = {}
        for size, partitions in configs:
            folder = OUT / phase / f'{size}-{partitions}'
            folder.mkdir(parents=True)
            broker = f'pool-{os.environ["PR"]}-{phase.lower()}-{size}'
            stop = threading.Event()
            sampler_errors = []
            sampler = None
            profiler = None
            profile_errors = []
            try:
                dispatch.broker_start(folder, broker, BROKER_RETENTION)
                sampler = threading.Thread(target=broker_samples,
                    args=(broker, folder / 'broker-stats.jsonl', stop, sampler_errors))
                sampler.start()
                output = folder / 'client'
                if profiling and not smoke:
                    profiler = threading.Thread(target=profile_windows,
                        args=(output, stop, profile_errors, dispatch, trace))
                    profiler.start()
                dispatch.command(['taskset', '-c', dispatch.AFFINITY['consumer'], 'dotnet', hosts[label],
                    'localhost:9092', output, broker, size, partitions, 12 if smoke else WARMUP_SECONDS, 13 if smoke else MEASURED_SECONDS],
                    folder / 'client.log', timeout=210 if smoke else 1200,
                    env=dict(os.environ, DOTNET_TieredCompilation='1', DOTNET_TieredPGO='1', DOTNET_ReadyToRun='1'))
                result = {}
                for stage in ('warmup', 'measured'):
                    data = json.loads((output / f'{stage}.json').read_text())
                    result[stage] = analyzer.analyze(data)
                for resource in ('consumer', 'producer', 'admin'):
                    shutdown = json.loads((output / f'shutdown-{resource}.json').read_text())
                    if shutdown['Error'] is not None:
                        raise ValueError(f'Failed {resource} shutdown')
                observations[phase][f'{size}-{partitions}'] = result
                (OUT / 'collection.json').write_text(json.dumps(observations, indent=2))
            finally:
                stop.set()
                try:
                    if sampler is not None:
                        sampler.join(timeout=15)
                        if sampler.is_alive():
                            raise RuntimeError('Broker sampler did not exit')
                        if sampler_errors:
                            raise RuntimeError('Broker sampler failed') from sampler_errors[0]
                    if profiler is not None:
                        profiler.join(timeout=95)
                        if profiler.is_alive():
                            raise RuntimeError('Profiler did not exit')
                        if profile_errors:
                            raise RuntimeError('Profiler failed') from profile_errors[0]
                finally:
                    dispatch.broker_stop(folder, broker)
    (OUT / 'decision.json').write_text(json.dumps({'collection': 'VALIDATED', 'acceptance': 'INCONCLUSIVE',
        'reason': 'Review protected metrics, uncertainty, runtime activity and missing failure/cold-path evidence.'}, indent=2))


if __name__ == '__main__':
    try:
        execute()
    finally:
        if OUT.exists():
            import hashlib
            inventory = {str(path.relative_to(OUT)): hashlib.sha256(path.read_bytes()).hexdigest()
                         for path in OUT.rglob('*') if path.is_file()}
            (OUT / 'sha256.json').write_text(json.dumps(inventory, indent=2))
