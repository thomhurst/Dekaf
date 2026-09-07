"""Task-scoped consumer and shutdown evidence; never promotes a performance gate."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
from xml.sax.saxutils import escape

MODES = {3117: ['key-records', 'key-batches'], 3086: ['fetch-depth-1', 'fetch-depth-3']}
TESTS = {
    3117: ('(PartitionedDispatchCoordinatorTests|AsyncAutoResetSignalTests)', 'PartitionedDispatchIntegrationTests'),
    3086: ('(ConsumerFollowerOffsetRetryTests|ConsumerRackAwarenessTests)', 'ConsumerFollowerOffsetRetryIntegrationTests'),
    3109: ('(PartitionedBackpressureTests|PartitionedConsumerRuntimeTests)', 'PartitionedBackpressureIntegrationTests'),
}


def command(args, log, **kwargs):
    with Path(log).open('w') as stream:
        result = subprocess.run(args, stdout=stream, stderr=subprocess.STDOUT, **kwargs)
    if result.returncode:
        raise RuntimeError(f'{args[0]} failed ({result.returncode}); see {log}')


def build(repository, workspace, artifacts, environment):
    hosts = {}
    for label in ('A', 'B'):
        fixture = workspace / f'loaded-{label}'
        fixture.mkdir()
        shutil.copyfile(repository / 'global.json', fixture / 'global.json')
        shutil.copyfile(repository / 'Directory.Packages.props', fixture / 'Directory.Packages.props')
        shutil.copyfile(repository / '.github/benchmarks/aba/loaded/Program.cs', fixture / 'Program.cs')
        project = workspace / f'product-{label}/src/Dekaf/Dekaf.csproj'
        (fixture / 'Loaded.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><LangVersion>preview</LangVersion><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup>
  <ItemGroup><ProjectReference Include="{escape(str(project))}" /></ItemGroup>
</Project>
''')
        command(['dotnet', 'build', str(fixture / 'Loaded.csproj'), '-c', 'Release', '-m:1', '-nr:false'],
                artifacts / f'build-loaded-{label}.log', cwd=fixture, env=environment)
        hosts[label] = fixture / 'bin/Release/net10.0/Loaded.dll'
        snapshot = artifacts / f'loaded-fixture-{label}'
        snapshot.mkdir()
        for name in ('Program.cs', 'Loaded.csproj', 'global.json', 'Directory.Packages.props'):
            shutil.copyfile(fixture / name, snapshot / name)
    return hosts


def broker_start(artifacts, tag):
    command(['docker', 'run', '-d', '--name', 'aba-kafka', '--cpuset-cpus', '0',
             '-p', '9092:9092', '-e', 'KAFKA_HEAP_OPTS=-Xms1g -Xmx1g',
             '-e', 'KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS=0', f'apache/kafka:{tag}'],
            artifacts / 'broker-start.log')
    for _ in range(60):
        result = subprocess.run(['docker', 'exec', 'aba-kafka', '/opt/kafka/bin/kafka-topics.sh',
                                 '--bootstrap-server', 'localhost:9092', '--list'], capture_output=True)
        if result.returncode == 0:
            command(['docker', 'inspect', 'aba-kafka'], artifacts / 'broker-inspect.json')
            return
        time.sleep(2)
    raise RuntimeError('Kafka readiness deadline exceeded')


def broker_stop(artifacts):
    with (artifacts / 'broker.log').open('w') as stream:
        subprocess.run(['docker', 'logs', 'aba-kafka'], stdout=stream, stderr=subprocess.STDOUT)
    subprocess.run(['docker', 'rm', '-f', 'aba-kafka'], check=True, capture_output=True)


def workload(pr, phase, mode, hosts, label, folder, seconds, rate, environment):
    folder.mkdir(parents=True)
    topic = f'aba-{pr}-{phase.lower()}-{mode}'
    command(['docker', 'exec', 'aba-kafka', '/opt/kafka/bin/kafka-topics.sh',
             '--bootstrap-server', 'localhost:9092', '--create', '--topic', topic,
             '--partitions', '4', '--replication-factor', '1'], folder / 'topic.log')
    common = [topic, str(folder), mode, str(seconds), str(rate)]
    with (folder / 'producer.log').open('w') as producer_log, (folder / 'consumer.log').open('w') as consumer_log:
        producer = subprocess.Popen(['taskset', '-c', '1', 'dotnet', str(hosts['A']), 'produce', *common],
                                    stdout=producer_log, stderr=subprocess.STDOUT, env=environment)
        consumer = subprocess.Popen(['taskset', '-c', '2,3', 'dotnet', str(hosts[label]), 'consume', *common],
                                    stdout=consumer_log, stderr=subprocess.STDOUT, env=environment)
        try:
            deadline = time.monotonic() + seconds + 230
            with (folder / 'broker-stats.jsonl').open('w') as stats:
                while producer.poll() is None or consumer.poll() is None:
                    if (producer.poll() not in (None, 0)) or (consumer.poll() not in (None, 0)):
                        raise RuntimeError(f'{mode} workload process failed; see {folder}')
                    if time.monotonic() > deadline:
                        raise RuntimeError(f'{mode} workload exceeded deadline')
                    sample = subprocess.run(['docker', 'stats', '--no-stream', '--format', '{{json .}}', 'aba-kafka'],
                                            capture_output=True, text=True, check=True)
                    stats.write(json.dumps({'monotonic': time.monotonic(), 'docker': json.loads(sample.stdout)}) + '\n')
                    stats.flush()
                    time.sleep(4)
            if producer.returncode or consumer.returncode:
                raise RuntimeError(f'{mode} workload failed; see {folder}')
        finally:
            for process in (producer, consumer):
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
    return validate_workload(folder, seconds, rate)


def validate_workload(folder, seconds, rate):
    measured = json.loads((folder / 'metrics.json').read_text())
    producer = json.loads((folder / 'producer.json').read_text())
    expected = (20 + seconds) * rate
    if not (measured['Completed'] == producer['Sent'] == producer['Acknowledged'] == expected):
        raise ValueError('Completed/acknowledged message counts differ')
    if measured['Measured'] != seconds * rate or measured['Failures'] or producer['Failed'] or measured['BacklogAtEnd']:
        raise ValueError('Incomplete successful workload')
    if (folder / 'latency-ticks.bin').stat().st_size != seconds * rate * 8:
        raise ValueError('Missing raw per-message latency samples')
    for key in ('CpuNsPerMessage', 'AllocatedBytesPerMessage', 'P50Ns', 'P99Ns', 'MaxNs', 'MessagesPerSecond'):
        if not isinstance(measured.get(key), (int, float)) or not 0 <= measured[key] < float('inf'):
            raise ValueError(f'Missing/invalid protected metric: {key}')
    return measured


def comparison(phases, metrics):
    if set(phases) != {'A1', 'B', 'A2'}:
        raise ValueError('Missing A-B-A phase')
    if not phases['A1'].keys() == phases['B'].keys() == phases['A2'].keys():
        raise ValueError('Mismatched workload modes')
    rows = []
    for mode in phases['A1']:
        values = {phase: modes[mode] for phase, modes in phases.items()}
        deltas = {}
        for metric in metrics:
            a1, b, a2 = (values[phase][metric] for phase in ('A1', 'B', 'A2'))
            deltas[metric] = {
                'B_minus_A1': b - a1, 'B_minus_A2': b - a2, 'A2_minus_A1': a2 - a1,
                'B_vs_A1_percent': 100 * (b / a1 - 1) if a1 else None,
                'B_vs_A2_percent': 100 * (b / a2 - 1) if a2 else None,
                'control_drift_percent': 100 * (a2 / a1 - 1) if a1 else None,
            }
        rows.append({'mode': mode, **values, 'deltas': deltas})
    return {'measurement_status': 'COMPLETE', 'acceptance': 'NOT_EVALUATED', 'cases': rows}


def validate(pr, hosts, benchmark_hosts, artifacts, environment):
    if pr == 3109:
        command(['taskset', '-c', '2,3', 'dotnet', str(benchmark_hosts['A']), '--shutdown-probe',
                 str(artifacts / 'shutdown-dry-A'), '100'], artifacts / 'shutdown-dry-A.log', env=environment)
        command(['taskset', '-c', '2,3', 'dotnet', str(benchmark_hosts['B']), '--shutdown-probe',
                 str(artifacts / 'shutdown-dry-B'), '100'], artifacts / 'shutdown-dry-B.log', env=environment)
    else:
        for phase, label in (('DryA', 'A'), ('DryB', 'B')):
            root = artifacts / f'loaded-{phase}'
            root.mkdir()
            broker_start(root, '4.3.1')
            try:
                for mode in MODES[pr]:
                    workload(pr, phase, mode, hosts, label, root / mode, 2, 1000, environment)
            finally:
                broker_stop(root)


def execute(pr, hosts, benchmark_hosts, artifacts, environment):
    phases = {}
    if pr == 3109:
        for phase, label in (('A1', 'A'), ('B', 'B'), ('A2', 'A')):
            folder = artifacts / f'loaded-{phase}/shutdown-full-queue'
            command(['taskset', '-c', '2,3', 'dotnet', str(benchmark_hosts[label]), '--shutdown-probe',
                     str(folder), '10000'], artifacts / f'shutdown-{phase}.log', env=environment)
            values = json.loads((folder / 'metrics.json').read_text())
            if values['Samples'] != 10000 or values['Completed'] != 10240000 or values['Failures'] or values['BacklogAtEnd']:
                raise ValueError('Incomplete shutdown evidence')
            phases[phase] = {'shutdown-full-queue': values}
        metrics = ['LifecycleOperationsPerSecond', 'LifecycleCpuNsPerOperation', 'LifecycleAllocatedBytesPerOperation', 'P50Ns', 'P99Ns', 'MaxNs']
    else:
        for phase, label in (('A1', 'A'), ('B', 'B'), ('A2', 'A')):
            root = artifacts / f'loaded-{phase}'
            root.mkdir()
            broker_start(root, '4.3.1')
            try:
                values = {mode: workload(pr, phase, mode, hosts, label, root / mode, 120, 50000, environment)
                          for mode in MODES[pr]}
                phases[phase] = values
            finally:
                broker_stop(root)
        metrics = ['MessagesPerSecond', 'CpuNsPerMessage', 'AllocatedBytesPerMessage', 'P50Ns', 'P99Ns', 'MaxNs']
    report = comparison(phases, metrics)
    (artifacts / 'loaded-comparison.json').write_text(json.dumps(report, indent=2))
    with Path(os.environ['GITHUB_STEP_SUMMARY']).open('a') as stream:
        stream.write('\n## Additional protected metrics\n\nRaw metrics, latency samples, GC/heap/RSS/backlog time series and separate A1/B/A2 deltas are in `loaded-comparison.json` and `loaded-*`. Acceptance requires review; workflow success does not promote the gate.\n')


def correctness(pr, product, artifacts, environment):
    # Tests execute after all timed work; test builds cannot interfere with controls.
    for category, test_class in zip(('Unit', 'Integration'), TESTS[pr]):
        command(['dotnet', 'test', '--project', str(product / f'tests/Dekaf.Tests.{category}'),
                 '-c', 'Release', '-f', 'net10.0', '--treenode-filter', f'/*/*/{test_class}/*'],
                artifacts / f'correctness-{category.lower()}.log', cwd=product, env=environment, timeout=1200)
