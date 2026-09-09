"""Diagnostic broker-loss validation. This is not performance acceptance."""
import argparse
import hashlib
import json
import socket
import subprocess
import time
import uuid
from pathlib import Path


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--observe-shutdown', action='store_true',
                        help='After a failed 45s exit ceiling, observe default disposal up to 150s; never relabel it a pass.')
    args = parser.parse_args()
    host = args.host.resolve(strict=True)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    write_json(output / 'host-files.json', {
        path.name: hashlib.sha256(path.read_bytes()).hexdigest()
        for path in host.parent.iterdir() if path.is_file()
    })
    with socket.socket() as listener:
        listener.bind(('127.0.0.1', 0))
        port = listener.getsockname()[1]
    name = 'dekaf-3142-failure-' + uuid.uuid4().hex[:12]
    settings = {
        'KAFKA_HEAP_OPTS': '-Xms512m -Xmx512m', 'KAFKA_NODE_ID': '1',
        'KAFKA_PROCESS_ROLES': 'broker,controller',
        'KAFKA_LISTENERS': f'PLAINTEXT://:{port},CONTROLLER://:9093',
        'KAFKA_ADVERTISED_LISTENERS': f'PLAINTEXT://localhost:{port}',
        'KAFKA_CONTROLLER_LISTENER_NAMES': 'CONTROLLER',
        'KAFKA_LISTENER_SECURITY_PROTOCOL_MAP': 'CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT',
        'KAFKA_CONTROLLER_QUORUM_VOTERS': '1@localhost:9093',
        'KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR': '1',
        'KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR': '1',
        'KAFKA_TRANSACTION_STATE_LOG_MIN_ISR': '1',
        'KAFKA_LOG_DIRS': '/var/lib/kafka/data', 'CLUSTER_ID': 'MkU3OEVBNTcwNTJENDM2Qg',
        'KAFKA_LOG_RETENTION_MS': '60000', 'KAFKA_LOG_RETENTION_BYTES': '134217728',
        'KAFKA_LOG_SEGMENT_BYTES': '33554432', 'KAFKA_LOG_SEGMENT_DELETE_DELAY_MS': '100',
        'KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS': '1000', 'KAFKA_LOG_INITIAL_TASK_DELAY_MS': '1000',
    }
    command = ['docker', 'run', '-d', '--name', name, '--memory', '1536m',
               '--tmpfs', '/var/lib/kafka/data:rw,size=1024m,mode=1777',
               '-p', f'127.0.0.1:{port}:{port}']
    for key, value in settings.items():
        command.extend(['-e', key + '=' + value])
    command.append('apache/kafka:4.3.1')
    container = None
    process = None
    outcome = {'status': 'FAILED', 'scope': 'Synthetic broker loss; no performance acceptance',
               'exitCeilingAfterFaultSeconds': 45}
    try:
        container = subprocess.check_output(command, text=True, encoding='utf-8').strip()
        write_json(output / 'owned-container.json', dict(id=container, name=name, port=port, command=command))
        with (output / 'ready.log').open('wb') as log:
            for attempt in range(12):
                ready = subprocess.run(['docker', 'exec', container,
                    '/opt/kafka/bin/kafka-broker-api-versions.sh', '--bootstrap-server', f'localhost:{port}'],
                    stdout=log, stderr=subprocess.STDOUT, timeout=20)
                if ready.returncode == 0:
                    break
                time.sleep(1)
            else:
                raise RuntimeError('Owned broker did not become ready')
        with (output / 'client.log').open('wb') as log:
            process = subprocess.Popen(['dotnet', str(host), f'localhost:{port}', str(output / 'results'),
                                        name, '1000', '3', '3', '5'], stdout=log, stderr=subprocess.STDOUT)
            write_json(output / 'owned-process.json', dict(pid=process.pid, host=str(host)))
            deadline = time.monotonic() + 60
            while not (output / 'results/warmup.json').exists():
                if process.poll() is not None or time.monotonic() >= deadline:
                    raise RuntimeError('Fixture did not complete warmup before fault injection')
                time.sleep(0.05)
            # File publication can race the final write; wait for it to close before reading.
            time.sleep(1)
            warmup = json.loads((output / 'results/warmup.json').read_text(encoding='utf-8'))
            if warmup['Failure'] or not warmup['Sent'] or not warmup['Sent'] == warmup['Acknowledged'] == warmup['Consumed']:
                raise RuntimeError('Warmup completion is invalid')
            if process.poll() is not None or (output / 'results/measured.json').exists():
                raise RuntimeError('Measurement finished before fault injection')
            fault_started = time.monotonic()
            with (output / 'fault.log').open('wb') as fault_log:
                subprocess.run(['docker', 'kill', container], stdout=fault_log, stderr=subprocess.STDOUT,
                               timeout=10, check=True)
            outcome['faultInjected'] = True
            try:
                process.wait(timeout=max(0, 45 - (time.monotonic() - fault_started)))
            except subprocess.TimeoutExpired:
                outcome['hostTimedOut'] = True
                if not args.observe_shutdown:
                    raise RuntimeError('Fixture exceeded its 45-second failure exit ceiling')
                outcome['observationCeilingSeconds'] = 150
                process.wait(timeout=max(0, 150 - (time.monotonic() - fault_started)))
            outcome['exitSecondsAfterFault'] = time.monotonic() - fault_started
            outcome['hostExitCode'] = process.returncode
            if process.returncode == 0:
                raise RuntimeError('Broker loss incorrectly returned success')
            for filename, error_field in (('measured.json', 'Failure'), ('completion.json', 'Error')):
                report = json.loads((output / 'results' / filename).read_text(encoding='utf-8'))
                if not report[error_field]:
                    raise RuntimeError(f'{filename} does not retain failure evidence')
            if outcome.get('hostTimedOut'):
                outcome['error'] = 'Exit observed after the failed 45-second ceiling; this does not pass that criterion'
            else:
                outcome['status'] = 'VALIDATED'
    except Exception as error:
        outcome['error'] = str(error)
    finally:
        if process is not None and process.poll() is None:
            process.kill()
            process.wait(timeout=10)
            outcome['driverKilledHost'] = True
        if container:
            for arguments, filename in (
                (['docker', 'logs', container], 'broker.log'),
                (['docker', 'inspect', container], 'broker-inspect.json'),
                (['docker', 'rm', '-f', container], 'cleanup.log'),
            ):
                try:
                    with (output / filename).open('wb') as log:
                        cleanup = subprocess.run(arguments, stdout=log, stderr=subprocess.STDOUT, timeout=30)
                    cleanup_failed = cleanup.returncode != 0
                except subprocess.TimeoutExpired:
                    cleanup_failed = True
                if cleanup_failed:
                    outcome['status'] = 'FAILED'
                    outcome.setdefault('cleanupErrors', []).append(filename)
        write_json(output / 'outcome.json', outcome)
    print(json.dumps(outcome), flush=True)
    return 0 if outcome['status'] == 'VALIDATED' else 1


if __name__ == '__main__':
    raise SystemExit(main())
