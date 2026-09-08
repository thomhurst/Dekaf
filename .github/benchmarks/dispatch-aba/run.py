"""Pinned one-VM dispatch comparison. Measurement completion never sets a PR gate."""
import argparse
import array
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import zipfile

CASES = [(pattern, batch) for pattern in ('Repeated', 'Distinct', 'PendingPairs') for batch in (1, 16)]
MODES = ('sync-records', 'sync-batches', 'pending-records', 'pending-batches')
WARMUP = 121
DURATION = 120


def command(args, log, **kwargs):
    with Path(log).open('w', encoding='utf-8') as stream:
        subprocess.run([str(arg) for arg in args], stdout=stream, stderr=subprocess.STDOUT, check=True, **kwargs)


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def validate_loaded(folder, warmup, seconds, rate, acceptance):
    metrics = json.loads((folder / 'metrics.json').read_text())
    producer = json.loads((folder / 'producer.json').read_text())
    series = json.loads((folder / 'series.json').read_text())
    total = (warmup + seconds) * rate
    if not (metrics['Completed'] == producer['Acknowledged'] == producer['Sent'] == total):
        raise ValueError('Incomplete workload')
    if metrics['Measured'] != seconds * rate or metrics['WarmupCompleted'] != warmup * rate:
        raise ValueError('Incorrect measurement denominator')
    if any((metrics['Failures'], metrics['BacklogAtEnd'], metrics['PendingAfterStop'], producer['Failed'])):
        raise ValueError('Failed or leftover work')
    expected_offsets = [(total + 3 - partition) // 4 for partition in range(4)]
    if metrics['CommittedOffsets'] != expected_offsets:
        raise ValueError('Incorrect committed progress')
    if (folder / 'latency-ticks.bin').stat().st_size != seconds * rate * 8:
        raise ValueError('Missing measured latency samples')
    if (folder / 'all-latency-ticks.bin').stat().st_size != total * 8:
        raise ValueError('Missing warmup/measurement latency samples')
    if metrics['Mode'].startswith('pending') and metrics['PendingCompletions'] == 0:
        raise ValueError('No pending completion coverage')
    if metrics['Mode'] == 'pending-batches' and metrics['BatchCounts'][16] == 0:
        raise ValueError('No 16-record pending batch coverage')
    if sum(size * count for size, count in enumerate(metrics['BatchCounts'])) != total:
        raise ValueError('Handler batch histogram does not account for all records')
    if sum(metrics['MeasuredBatchCounts']) != metrics['MeasuredHandlerInvocations']:
        raise ValueError('Incorrect handler allocation denominator')
    for key in ('MessagesPerSecond', 'CpuNsPerMessage', 'AllocatedBytesPerMessage',
                'AllocatedBytesPerHandlerInvocation', 'P50Ns', 'P99Ns', 'MaxNs'):
        if type(metrics.get(key)) not in (int, float) or not 0 < metrics[key] < float('inf'):
            raise ValueError(f'Missing/invalid metric {key}')
    if not metrics['P50Ns'] <= metrics['P99Ns'] <= metrics['MaxNs']:
        raise ValueError('Invalid latency percentiles')
    if acceptance and metrics['ActualWarmupSeconds'] < 120:
        raise ValueError('Less than 120 seconds of actual loaded warmup')
    for start, end in [('JitMethodsStart', 'JitMethodsEnd'), ('JitMsStart', 'JitMsEnd')]:
        if start not in metrics or end not in metrics or metrics[end] < metrics[start]:
            raise ValueError('Missing/invalid runtime measurement boundaries')
    if len(series) < seconds + warmup - 3:
        raise ValueError('Missing runtime time series')
    for sample in series:
        for key in ('JitMethods', 'JitMs', 'Threads', 'PendingWork', 'CpuTicks', 'Gen0', 'Gen1', 'Gen2', 'HeapBytes', 'RssBytes'):
            if key not in sample:
                raise ValueError(f'Missing runtime metric {key}')
    # Keep startup transitions explicit; no automatic PASS from scalar metrics.
    actual = [s for s in series if metrics['MeasurementStart'] <= s['Timestamp'] <= metrics['MeasurementEnd']]
    metrics['MeasuredJitDelta'] = metrics['JitMethodsEnd'] - metrics['JitMethodsStart']
    metrics['MeasuredThreadRange'] = [min(s['Threads'] for s in actual), max(s['Threads'] for s in actual)] if actual else None
    return metrics


def workload(hosts, label, folder, mode, warmup, seconds, rate, broker, pinned=True):
    folder.mkdir(parents=True)
    topic = 'dispatch-' + folder.name.lower()
    command(['docker', 'exec', broker, '/opt/kafka/bin/kafka-topics.sh', '--bootstrap-server', 'localhost:9092',
             '--create', '--topic', topic, '--partitions', '4', '--replication-factor', '1'], folder / 'topic.log')
    common = [topic, str(folder), mode, str(warmup), str(seconds), str(rate)]
    environment = dict(os.environ, DOTNET_TieredCompilation='1', DOTNET_GCDynamicAdaptationMode='0')
    processes = []
    with (folder / 'producer.log').open('w') as producer_log, (folder / 'consumer.log').open('w') as consumer_log:
        try:
            for role, product, cpus, stream in [('produce', 'A', '1', producer_log), ('consume', label, '2,3', consumer_log)]:
                prefix = ['taskset', '-c', cpus] if pinned else []
                processes.append(subprocess.Popen(prefix + ['dotnet', str(hosts[product]), role, *common],
                                                  stdout=stream, stderr=subprocess.STDOUT, env=environment))
            deadline = time.monotonic() + warmup + seconds + 190
            while any(process.poll() is None for process in processes):
                if any(process.poll() not in (None, 0) for process in processes):
                    raise RuntimeError(f'Loaded process failed; inspect {folder}')
                if time.monotonic() > deadline:
                    raise RuntimeError(f'Loaded process exceeded deadline; inspect {folder}')
                time.sleep(.5)
            if any(process.returncode != 0 for process in processes):
                raise RuntimeError(f'Loaded process failed; inspect {folder}')
        finally:
            for process in processes:
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
    metrics = validate_loaded(folder, warmup, seconds, rate, acceptance=pinned and warmup == WARMUP)
    if metrics['Mode'] != mode or metrics['OfferedMessagesPerSecond'] != rate:
        raise ValueError('Wrong measured workload')
    return metrics


def latency_series(folder, metrics):
    producer = json.loads((folder / 'producer.json').read_text())
    latencies = array.array('q')
    with (folder / 'latency-ticks.bin').open('rb') as stream:
        latencies.fromfile(stream, metrics['Measured'])
    bins = {}
    frequency = metrics['StopwatchFrequency']
    for index, latency in enumerate(latencies, start=metrics['WarmupCompleted']):
        scheduled = producer['ScheduledStart'] + int((index // producer['OfferBurst'] * producer['OfferBurst']) *
                                                     frequency / producer['Rate'])
        second = (scheduled + latency - metrics['MeasurementStart']) // frequency
        bins.setdefault(second, []).append(latency)
    samples = []
    for second, values in sorted(bins.items()):
        values.sort()
        count = len(values)
        samples.append(dict(second=second, completed=count,
                            p50_ns=values[(count - 1) // 2] * 1e9 / frequency,
                            p99_ns=values[(99 * count + 99) // 100 - 1] * 1e9 / frequency,
                            max_ns=values[-1] * 1e9 / frequency))
    if sum(row['completed'] for row in samples) != metrics['Measured']:
        raise ValueError('Latency time series dropped samples')
    (folder / 'latency-series.json').write_text(json.dumps(samples, indent=2))


def broker_start(folder, name):
    command(['docker', 'run', '-d', '--name', name, '--cpuset-cpus', '0', '-p', '9092:9092',
             '-e', 'KAFKA_HEAP_OPTS=-Xms1g -Xmx1g', '-e', 'KAFKA_NODE_ID=1',
             '-e', 'KAFKA_PROCESS_ROLES=broker,controller', '-e', 'KAFKA_CONTROLLER_QUORUM_VOTERS=1@localhost:9093',
             '-e', 'KAFKA_CONTROLLER_LISTENER_NAMES=CONTROLLER',
             '-e', 'KAFKA_LISTENERS=PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093',
             '-e', 'KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092',
             '-e', 'KAFKA_LISTENER_SECURITY_PROTOCOL_MAP=PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT',
             '-e', 'KAFKA_INTER_BROKER_LISTENER_NAME=PLAINTEXT', '-e', 'KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1',
             '-e', 'KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR=1', '-e', 'KAFKA_TRANSACTION_STATE_LOG_MIN_ISR=1',
             '-e', 'KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS=0', 'apache/kafka:4.3.1'], folder / 'broker-start.log')
    deadline = time.monotonic() + 120
    with (folder / 'broker-ready.log').open('w') as stream:
        while time.monotonic() < deadline:
            result = subprocess.run(['docker', 'exec', name, '/opt/kafka/bin/kafka-topics.sh', '--bootstrap-server',
                                     'localhost:9092', '--list'], stdout=stream, stderr=subprocess.STDOUT, timeout=20)
            if result.returncode == 0:
                command(['docker', 'inspect', name], folder / 'broker-inspect.json')
                return
            time.sleep(2)
    raise RuntimeError('Broker readiness deadline exceeded')


def broker_stop(folder, name):
    with (folder / 'broker.log').open('w') as stream:
        subprocess.run(['docker', 'logs', name], stdout=stream, stderr=subprocess.STDOUT)
    subprocess.run(['docker', 'rm', '-f', '-v', name], check=True, capture_output=True)


def build(root, output, sha, label):
    source = output / f'product-{label}'
    snapshot = output / f'product-{label}.zip'
    command(['git', 'archive', '--format=zip', f'--output={snapshot}', sha], output / f'archive-{label}.log', cwd=root)
    with zipfile.ZipFile(snapshot) as archive:
        for entry in archive.infolist():
            destination = (source / entry.filename).resolve()
            if not destination.is_relative_to(source.resolve()):
                raise ValueError('Source archive path escapes product directory')
        archive.extractall(source)
    product = source / 'src/Dekaf/bin/Release/net10.0'
    command(['dotnet', 'build', source / 'src/Dekaf/Dekaf.csproj', '-c', 'Release', '-f', 'net10.0',
             '-p:CopyLocalLockFileAssemblies=true', '-m:1', '-nr:false'], output / f'build-{label}.log', cwd=source)
    fixture = output / f'fixture-{label}'
    shutil.copytree(root / '.github/benchmarks/dispatch-aba', fixture,
                    ignore=shutil.ignore_patterns('bin', 'obj', '__pycache__'))
    for name in ('global.json', 'Directory.Packages.props'):
        shutil.copyfile(root / name, fixture / name)
    hosts = {}
    for project, assembly in [('Harness', 'Dekaf.Benchmarks'), ('Loaded', 'Loaded')]:
        target = output / f'{project}-{label}'
        command(['dotnet', 'build', fixture / f'{project}.csproj', '-c', 'Release',
                 f'-p:ProductDirectory={product}', '-m:1', '-nr:false', '-o', target],
                output / f'build-{project}-{label}.log', cwd=fixture)
        for name in ('Dekaf.dll', 'Dekaf.Abstractions.dll'):
            if digest(product / name) != digest(target / name):
                raise ValueError(f'Wrong loaded {name} for {label}')
        hosts[project] = target / f'{assembly}.dll'
    return hosts


def micro(host, output, label, phase, smoke):
    environment = dict(os.environ, DOTNET_TieredCompilation='0')
    for pattern, batch in CASES:
        folder = output / f'{phase}-micro-{pattern}-{batch}'
        args = ['taskset', '-c', '2', 'dotnet', host, pattern, str(batch), folder]
        if smoke:
            args.append('--smoke')
        if label == 'B':
            args.append('--require-zero')
        log = output / f'{folder.name}.log'
        command(args, log, env=environment)
        content = log.read_text()
        if not smoke:
            warm = re.search(r'WARM completed seconds=([\d.]+) operations=(\d+)', content)
            if not warm or float(warm[1]) < 20 or int(warm[2]) <= 0:
                raise ValueError(f'Missing actual warmup: {folder}')
            actual = re.findall(r'^WorkloadActual\s+\d+\s*:', content, re.MULTILINE)
    if len(actual) != 25:
                raise ValueError(f'Missing measured BDN iterations: {folder}')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    root = Path.cwd()
    for sha in (args.baseline, args.candidate):
        if not re.fullmatch('[0-9a-f]{40}', sha):
            raise ValueError('Exact product SHA required')
    subprocess.run(['git', 'merge-base', '--is-ancestor', args.baseline, args.candidate], check=True)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    provenance = dict(baseline=args.baseline, candidate=args.candidate,
                      harness=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
                      runner_image=os.environ.get('ImageVersion'), runner_os=os.environ.get('ImageOS'),
                      run_url=f"https://github.com/{os.environ.get('GITHUB_REPOSITORY')}/actions/runs/{os.environ.get('GITHUB_RUN_ID')}",
                      phases=['A1', 'B', 'A2'], loaded_warmup_seconds=WARMUP, loaded_duration_seconds=DURATION)
    (output / 'provenance.json').write_text(json.dumps(provenance, indent=2))
    command(['dotnet', '--info'], output / 'dotnet-info.log')
    command(['lscpu'], output / 'hardware.log')
    hosts = {label: build(root, output, sha, label) for label, sha in [('A', args.baseline), ('B', args.candidate)]}
    loaded_hosts = {label: values['Loaded'] for label, values in hosts.items()}
    command(['dotnet', 'build-server', 'shutdown'], output / 'build-server-shutdown.log')
    phases = {}
    try:
        for phase, label, smoke in [('DryA', 'A', True), ('DryB', 'B', True),
                                    ('A1', 'A', False), ('B', 'B', False), ('A2', 'A', False)]:
            micro(hosts[label]['Harness'], output, label, phase, smoke)
            broker = f'dispatch-3117-{phase.lower()}'
            phase_folder = output / phase
            phase_folder.mkdir()
            values = {}
            try:
                broker_start(phase_folder, broker)
                for mode in MODES:
                    rate = 1000 if smoke or mode.startswith('pending') else 50000
                    workload_folder = phase_folder / f'{phase}-{mode}'
                    values[mode] = workload(loaded_hosts, label, workload_folder, mode,
                                            2 if smoke else WARMUP, 2 if smoke else DURATION, rate, broker)
                    latency_series(workload_folder, values[mode])
            finally:
                broker_stop(phase_folder, broker)
            if not smoke:
                phases[phase] = values
                (output / 'loaded-metrics.json').write_text(json.dumps(phases, indent=2))
        (output / 'decision.json').write_text(json.dumps({
            'measurement': 'COMPLETE', 'acceptance': 'INCONCLUSIVE',
            'reason': 'Human review of protected metrics, controls, startup transitions, and missing loaded shutdown evidence required.'
        }, indent=2))
    finally:
        inventory = {str(path.relative_to(output)): digest(path) for path in output.rglob('*') if path.is_file()}
        (output / 'sha256.json').write_text(json.dumps(inventory, indent=2))


if __name__ == '__main__':
    main()
