"""Task-scoped PR #3137 harness. Never merge this experimental branch."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path.cwd()
OUT = ROOT / 'evidence'
OUT.mkdir(exist_ok=True)
A = '5df2f0d03607389384b5c1466e17812a9084fac9'
B = '451d6fb40d907998a1bf72a8c8ca427cb1d85da7'


def run(args, log=None, cwd=ROOT, check=True, env=None):
    print(' '.join(map(str, args)), flush=True)
    if log:
        with Path(log).open('w') as output:
            return subprocess.run(args, cwd=cwd, stdout=output, stderr=subprocess.STDOUT,
                                  check=check, env=env)
    return subprocess.run(args, cwd=cwd, check=check, env=env)


def replace_once(text, before, after):
    assert text.count(before) == 1, before
    return text.replace(before, after)


run(['git', 'merge-base', '--is-ancestor', A, B])
manifest = dict(baseline=A, candidate=B,
    harness=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
    mainAtRun=subprocess.check_output(['git', 'ls-remote', 'origin', 'refs/heads/main'], text=True).strip(),
    runnerImage=os.getenv('ImageVersion'), runnerOS=os.getenv('ImageOS'),
    settings=dict(order=['A1', 'B', 'A2'], stressMinutes=5, warmupSeconds=30,
                  messageBytes=1000, partitions=6, connections=1,
                  tolerancePercent=3, maxControlDriftPercent=10,
                  poolAllocationBytes=0, stressAllocationNoiseFloor=1,
                  bdnWarmupIterations=30, bdnIterationMilliseconds=1000,
                  bdnMeasuredIterations=15, outliers='DontRemove'),
    limitations=['Consumer end-to-end, recovery, and shutdown are not measured.',
                 'BDN process JIT/thread-pool time series are not collected; overall acceptance cannot be PASS.'])
(OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2))
run(['dotnet', '--info'], OUT / 'dotnet-info.txt')
run(['lscpu'], OUT / 'hardware.txt')
run(['dotnet', 'tool', 'install', '--tool-path', str(ROOT / 'diagnostics'),
     'dotnet-counters', '--version', '10.0.731102'], OUT / 'diagnostics-install.log')
run(['docker', 'pull', 'apache/kafka:4.3.1'], OUT / 'docker-pull.log')
run(['docker', 'image', 'inspect', 'apache/kafka:4.3.1'], OUT / 'broker-image.json')

for label, sha in [('A', A), ('B', B)]:
    source = ROOT / ('source-' + label)
    run(['git', 'worktree', 'add', '--detach', str(source), sha])
    helper = source / 'tools/Dekaf.StressTests/Scenarios/StressTestHelpers.cs'
    text = helper.read_text()
    text = replace_once(text,
        'for (var i = 1; i < ProducerWarmupMessageCount; i++)',
        'long warmupCount = 1;\n        var warmupClock = Stopwatch.StartNew();\n'
        '        while (warmupClock.Elapsed < TimeSpan.FromSeconds(30))')
    text = replace_once(text,
        'await producer.FireAsync(options.Topic, warmupKey, warmupValue).ConfigureAwait(false);',
        'await producer.FireAsync(options.Topic, warmupKey, warmupValue).ConfigureAwait(false);\n'
        '            warmupCount++;\n'
        '            if (warmupCount % 1000 == 0)\n'
        '            {\n'
        '                await producer.ProduceAsync(options.Topic, warmupKey, warmupValue, cancellationToken).ConfigureAwait(false);\n'
        '                warmupCount++;\n'
        '            }')
    text = replace_once(text, '            ProducerWarmupMessageCount,', '            warmupCount,')
    text = replace_once(text, '        ResetProducerDeliveryDiagnostics(producer);',
        '        Console.WriteLine($"ABA warmup: {warmupClock.Elapsed.TotalSeconds:F3}s; {warmupCount} completed records");\n'
        '        ResetProducerDeliveryDiagnostics(producer);')
    helper.write_text(text)
    scenario = source / 'tools/Dekaf.StressTests/Scenarios/ProducerStressTest.cs'
    text = replace_once(scenario.read_text(),
        '            throughput,\n            cancellationToken);',
        '            throughput,\n            "warmup",\n            messageValue,\n            cancellationToken);')
    scenario.write_text(text)
    run(['git', 'diff'], OUT / f'{label}-fixture.patch', cwd=source)
    for project in ['Dekaf.Benchmarks', 'Dekaf.StressTests']:
        run(['dotnet', 'build', f'tools/{project}', '-c', 'Release', '--disable-build-servers'],
            OUT / f'{label}-{project}-build.log', cwd=source)
    run(['dotnet', 'run', '--project', 'tools/Dekaf.Benchmarks', '-c', 'Release', '--no-build', '--',
         '--filter', '*PoolHotPathBenchmarks*', '--job', 'Dry', '--exporters', 'fulljson',
         '--artifacts', str(OUT / f'{label}-dry')], OUT / f'{label}-dry.log', cwd=source)

assert (OUT / 'A-fixture.patch').read_bytes() == (OUT / 'B-fixture.patch').read_bytes()
run(['dotnet', 'build-server', 'shutdown'])
cpu_count = os.cpu_count()
assert cpu_count >= 4, 'Need separate broker and client CPU pairs'
client_cpus = f'{cpu_count-2},{cpu_count-1}'
broker_cpus = f'0-{cpu_count-3}'
manifest['settings'].update(clientCpus=client_cpus, brokerCpus=broker_cpus)
(OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2))

for phase, product in [('A1', 'A'), ('B', 'B'), ('A2', 'A')]:
    source = ROOT / ('source-' + product)
    dest = OUT / phase
    dest.mkdir()
    run(['taskset', '-c', client_cpus, 'dotnet', 'run', '--project', 'tools/Dekaf.Benchmarks',
         '-c', 'Release', '--no-build', '--', '--filter', '*PoolHotPathBenchmarks*',
         '--warmupCount', '30', '--iterationTime', '1000', '--iterationCount', '15',
         '--launchCount', '1', '--outliers', 'DontRemove', '--exporters', 'fulljson',
         '--artifacts', str(dest / 'micro')], dest / 'micro.log', cwd=source)
    run(['dotnet', 'build-server', 'shutdown'])
    config = dict(KAFKA_HEAP_OPTS='-Xmx1g -Xms1g', KAFKA_NODE_ID='1',
        KAFKA_PROCESS_ROLES='broker,controller',
        KAFKA_LISTENERS='PLAINTEXT://:9092,CONTROLLER://:9093',
        KAFKA_ADVERTISED_LISTENERS='PLAINTEXT://localhost:9092',
        KAFKA_CONTROLLER_LISTENER_NAMES='CONTROLLER',
        KAFKA_LISTENER_SECURITY_PROTOCOL_MAP='CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT',
        KAFKA_CONTROLLER_QUORUM_VOTERS='1@localhost:9093',
        KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR='1',
        KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR='1',
        KAFKA_TRANSACTION_STATE_LOG_MIN_ISR='1', KAFKA_LOG_DIRS='/var/lib/kafka/data',
        CLUSTER_ID='MkU3OEVBNTcwNTJENDM2Qg', KAFKA_LOG_RETENTION_MS='10000',
        KAFKA_LOG_RETENTION_BYTES='134217728', KAFKA_LOG_SEGMENT_BYTES='33554432',
        KAFKA_LOG_SEGMENT_DELETE_DELAY_MS='100', KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS='1000',
        KAFKA_LOG_INITIAL_TASK_DELAY_MS='1000', KAFKA_LOG_CLEANUP_POLICY='delete')
    args = ['docker', 'run', '-d', '--name', 'kafka', '--cpuset-cpus', broker_cpus,
            '--tmpfs', '/var/lib/kafka/data:rw,size=3g,mode=1777', '-p', '9092:9092']
    for key, value in config.items():
        args += ['-e', f'{key}={value}']
    run(args + ['apache/kafka:4.3.1'], dest / 'broker-start.log')
    try:
        for attempt in range(60):
            probe = run(['docker', 'exec', 'kafka', '/opt/kafka/bin/kafka-broker-api-versions.sh',
                         '--bootstrap-server', 'localhost:9092'], dest / 'broker-ready.log', check=False)
            if probe.returncode == 0:
                break
            time.sleep(2)
        else:
            raise RuntimeError('Broker did not become ready')
        env = dict(os.environ, KAFKA_BOOTSTRAP_SERVERS='localhost:9092')
        run([str(ROOT / 'diagnostics/dotnet-counters'), 'collect', '--format', 'csv',
             '--output', str(dest / 'runtime.csv'), '--refresh-interval', '1',
             '--counters', 'System.Runtime', '--show-child-io', '--', 'taskset', '-c', client_cpus,
             'dotnet', str(source / 'tools/Dekaf.StressTests/bin/Release/net10.0/Dekaf.StressTests.dll'),
             '--duration', '5', '--message-size', '1000', '--scenario', 'producer', '--client', 'dekaf',
             '--brokers', '1', '--connections-per-broker', '1', '--producer-delivery-diagnostics',
             '--output', str(dest / 'stress')], dest / 'stress.log', cwd=source, env=env)
        run(['docker', 'inspect', 'kafka'], dest / 'broker-state.json')
    finally:
        run(['docker', 'logs', 'kafka'], dest / 'broker.log', check=False)
        run(['docker', 'rm', '-f', 'kafka'], check=False)
