"""Same-VM completed share-acknowledgement comparison; never promotes a PR gate."""
import argparse
import array
import importlib.util
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import time
import zipfile

SPEC = importlib.util.spec_from_file_location('dispatch_common', Path(__file__).parents[1] / 'dispatch-aba/run.py')
common = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(common)
WARMUP, DURATION, RATE = 210, 180, 5120
MODES = ('legacy', 'batch')
SHARE_ENVIRONMENT = ('KAFKA_GROUP_SHARE_ENABLE=true',
    'KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS=classic,consumer,share',
    'KAFKA_SHARE_COORDINATOR_STATE_TOPIC_REPLICATION_FACTOR=1',
    'KAFKA_SHARE_COORDINATOR_STATE_TOPIC_MIN_ISR=1',
    'KAFKA_SHARE_COORDINATOR_STATE_TOPIC_NUM_PARTITIONS=3')


def validate(folder, warmup, seconds, rate, acceptance=False):
    metrics = json.loads((folder / 'metrics.json').read_text())
    producer = json.loads((folder / 'producer.json').read_text())
    series = json.loads((folder / 'series.json').read_text())
    total = (warmup + seconds) * rate
    if not (metrics['Processed'] == metrics['Completed'] == producer['Acknowledged'] == producer['Sent'] == total):
        raise ValueError('Incomplete processed/acknowledged workload')
    if metrics['Measured'] != seconds * rate or metrics['WarmupCompleted'] != warmup * rate:
        raise ValueError('Incorrect measurement denominator')
    if any((metrics['Failures'], metrics['BacklogAtEnd'], producer['Failed'])):
        raise ValueError('Failed or leftover work')
    if metrics['ExplicitCommits'] != total // 128 or total % 128:
        raise ValueError('Incorrect explicit-commit boundaries')
    if acceptance and (metrics['ActualWarmupSeconds'] < 180 or metrics['MeasuredDurationSeconds'] < DURATION - 2):
        raise ValueError('Insufficient actual workload duration')
    for name in ('MessagesPerSecond', 'CpuNsPerMessage', 'AllocatedBytesPerMessage', 'P50Ns', 'P99Ns', 'MaxNs'):
        if not isinstance(metrics.get(name), (float, int)) or not 0 < metrics[name] < math.inf:
            raise ValueError(f'Missing/invalid metric {name}')
    for name, expected in (
        ('CpuNsPerMessage', (metrics['CpuTicksEnd'] - metrics['CpuTicksStart']) * 100 / metrics['Measured']),
        ('AllocatedBytesPerMessage', (metrics['AllocatedBytesEnd'] - metrics['AllocatedBytesStart']) / metrics['Measured']),
        ('MessagesPerSecond', metrics['Measured'] / metrics['MeasuredDurationSeconds'])):
        if not math.isclose(metrics[name], expected, rel_tol=1e-12):
            raise ValueError(f'Incorrect measurement denominator for {name}')
    measured = None
    for name, count in [('all-latency-ticks.bin', total), ('latency-ticks.bin', seconds * rate)]:
        if (folder / name).stat().st_size != count * 8:
            raise ValueError('Missing raw latency samples')
        values = array.array('q')
        with (folder / name).open('rb') as stream:
            values.fromfile(stream, count)
        if any(value <= 0 for value in values):
            raise ValueError('Invalid raw latency value')
        if name == 'all-latency-ticks.bin':
            measured = values[warmup * rate:]
        elif values != measured:
            raise ValueError('Measured latency population differs from full capture')
    ordered = sorted(measured)
    for name, index in [('P50Ns', math.ceil(len(ordered) * .5) - 1),
                        ('P99Ns', math.ceil(len(ordered) * .99) - 1), ('MaxNs', -1)]:
        expected = ordered[index] * 1e9 / metrics['StopwatchFrequency']
        if not math.isclose(metrics[name], expected, rel_tol=1e-12):
            raise ValueError(f'Incorrect latency statistic {name}')
    if len(series) < warmup + seconds - 3:
        raise ValueError('Missing runtime time series')
    for sample in series:
        for field in ('JitMethods', 'JitMs', 'Threads', 'PendingWork', 'CpuTicks', 'Gen0', 'Gen1', 'Gen2', 'HeapBytes', 'RssBytes'):
            if field not in sample:
                raise ValueError(f'Missing runtime metric {field}')
    if metrics['JitMethodsEnd'] < metrics['JitMethodsStart'] or metrics['JitMsEnd'] < metrics['JitMsStart']:
        raise ValueError('Invalid JIT boundary counters')
    common.latency_series(folder, metrics)
    return metrics


def workload(hosts, label, folder, mode, warmup, seconds, rate, broker, pinned=True):
    folder.mkdir(parents=True)
    topic = 'share-' + folder.name.lower()
    common.command(['docker', 'exec', broker, '/opt/kafka/bin/kafka-topics.sh', '--bootstrap-server', 'localhost:9092',
        '--create', '--topic', topic, '--partitions', '1', '--replication-factor', '1'], folder / 'topic.log')
    arguments = [topic, str(folder), mode, str(warmup), str(seconds), str(rate)]
    environment = dict(os.environ, DOTNET_TieredCompilation='1', DOTNET_GCDynamicAdaptationMode='0')
    processes = []
    with (folder / 'producer.log').open('w') as producer_log, (folder / 'consumer.log').open('w') as consumer_log:
        try:
            for role, product, cpus, stream in [('produce', 'A', common.AFFINITY['infrastructure'], producer_log),
                                                ('consume', label, common.AFFINITY['consumer'], consumer_log)]:
                prefix = ['taskset', '-c', cpus] if pinned else []
                processes.append(subprocess.Popen(prefix + ['dotnet', str(hosts[product]), role, *arguments],
                    stdout=stream, stderr=subprocess.STDOUT, env=environment))
            deadline = time.monotonic() + warmup + seconds + 190
            while any(process.poll() is None for process in processes):
                if any(process.poll() not in (None, 0) for process in processes):
                    raise RuntimeError(f'Share workload failed; inspect {folder}')
                if time.monotonic() > deadline:
                    raise RuntimeError(f'Share workload deadline exceeded; inspect {folder}')
                time.sleep(.5)
            if any(process.returncode != 0 for process in processes):
                raise RuntimeError(f'Share workload failed; inspect {folder}')
        finally:
            for process in processes:
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
    metrics = validate(folder, warmup, seconds, rate, acceptance=pinned and warmup == WARMUP)
    if metrics['Mode'] != mode or metrics['OfferedMessagesPerSecond'] != rate:
        raise ValueError('Wrong measured workload')
    return metrics


def build(root, output, sha, label):
    source = output / f'product-{label}'
    snapshot = output / f'product-{label}.zip'
    common.command(['git', 'archive', '--format=zip', f'--output={snapshot}', sha], output / f'archive-{label}.log', cwd=root)
    with zipfile.ZipFile(snapshot) as archive:
        for entry in archive.infolist():
            if not (source / entry.filename).resolve().is_relative_to(source.resolve()):
                raise ValueError('Source archive escapes product directory')
        archive.extractall(source)
    product = source / 'src/Dekaf/bin/Release/net10.0'
    common.command(['dotnet', 'build', source / 'src/Dekaf/Dekaf.csproj', '-c', 'Release', '-f', 'net10.0',
        '-p:CopyLocalLockFileAssemblies=true', '-m:1', '-nr:false'], output / f'build-{label}.log', cwd=source)
    fixture = output / f'fixture-{label}'
    shutil.copytree(root / '.github/benchmarks/share-loaded', fixture, ignore=shutil.ignore_patterns('bin', 'obj', '__pycache__'))
    for name in ('global.json', 'Directory.Packages.props'):
        shutil.copyfile(root / name, fixture / name)
    target = output / f'Loaded-{label}'
    common.command(['dotnet', 'build', fixture / 'Loaded.csproj', '-c', 'Release', f'-p:ProductDirectory={product}',
        '-p:Candidate=' + str(label == 'B').lower(), '-m:1', '-nr:false', '-o', target], output / f'build-loaded-{label}.log', cwd=fixture)
    for name in ('Dekaf.dll', 'Dekaf.Abstractions.dll'):
        if common.digest(product / name) != common.digest(target / name):
            raise ValueError(f'Wrong loaded {name}')
    return target / 'Loaded.dll'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    root, output = Path.cwd(), args.output.resolve()
    for sha in (args.baseline, args.candidate):
        if len(sha) != 40 or subprocess.check_output(['git', 'rev-parse', sha + '^{commit}'], text=True).strip() != sha:
            raise ValueError('Exact product SHA required')
    subprocess.run(['git', 'merge-base', '--is-ancestor', args.baseline, args.candidate], check=True)
    output.mkdir(exist_ok=False)
    common.configure_affinity()
    provenance = dict(A=args.baseline, B=args.candidate,
        harness=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
        image=os.getenv('ImageOS'), image_version=os.getenv('ImageVersion'), affinity=common.AFFINITY,
        warmup_offered_seconds=WARMUP, minimum_actual_warmup_seconds=180, measured_seconds=DURATION, offered_rate=RATE,
        modes=MODES, phases=['A1', 'B', 'A2'], run_id=os.getenv('GITHUB_RUN_ID'),
        scope='Completed share acknowledgements; explicit mode; no loaded recovery/shutdown acceptance')
    (output / 'provenance.json').write_text(json.dumps(provenance, indent=2))
    try:
        common.command(['git', 'archive', '--format=zip', '--output=' + str(output / 'harness.zip'), 'HEAD'],
                       output / 'archive-harness.log', cwd=root)
        common.command(['dotnet', '--info'], output / 'runtime.txt')
        common.command(['lscpu'], output / 'hardware.txt')
        hosts = {label: build(root, output, sha, label) for label, sha in [('A', args.baseline), ('B', args.candidate)]}
        common.command(['dotnet', 'build-server', 'shutdown'], output / 'build-server-shutdown.log')
        results = {}
        for phase, label, smoke in [('DryA', 'A', True), ('DryB', 'B', True), ('A1', 'A', False), ('B', 'B', False), ('A2', 'A', False)]:
            folder = output / phase
            folder.mkdir()
            broker = 'share-' + phase.lower()
            common.broker_start(folder, broker, SHARE_ENVIRONMENT)
            try:
                values = {mode: workload(hosts, label, folder / mode, mode, 2 if smoke else WARMUP,
                    2 if smoke else DURATION, 1024 if smoke else RATE, broker) for mode in MODES}
            finally:
                common.broker_stop(folder, broker)
            if not smoke:
                results[phase] = values
                (output / 'metrics.json').write_text(json.dumps(results, indent=2))
        (output / 'decision.json').write_text(json.dumps(dict(measurement='COMPLETE', acceptance='INCONCLUSIVE',
            reason='Review protected metrics, drift, uncertainty, runtime and missing scope; no automatic gate override'), indent=2))
    finally:
        (output / 'inventory.json').write_text(json.dumps({str(p.relative_to(output)): common.digest(p)
            for p in output.rglob('*') if p.is_file() and p.name != 'inventory.json'}, indent=2))


if __name__ == '__main__':
    main()
