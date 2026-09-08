"""Local, bounded attribution using the exact retained hosted binaries; no performance gate writes."""
import argparse
import json
import math
from pathlib import Path
import subprocess
import time
import uuid

import run

ROOT = Path(__file__).resolve().parents[3]
WORK = ROOT / '.artifacts/linux-jit'
INPUTS = Path('C:/git/Dekaf-evidence/pr-3117/linux-jit-20260908/raw/inputs')
RESULTS = WORK / 'results'
SDK = 'sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c'
KAFKA = 'sha256:77e3df9054047a88b520d0cc46e16696d3b22022e1d580aeccd2632df6532837'
BROKER_ENV = {
    'KAFKA_HEAP_OPTS': '-Xms1g -Xmx1g', 'KAFKA_NODE_ID': '1',
    'KAFKA_PROCESS_ROLES': 'broker,controller', 'KAFKA_CONTROLLER_QUORUM_VOTERS': '1@localhost:9093',
    'KAFKA_CONTROLLER_LISTENER_NAMES': 'CONTROLLER',
    'KAFKA_LISTENERS': 'PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093',
    'KAFKA_ADVERTISED_LISTENERS': 'PLAINTEXT://localhost:9092',
    'KAFKA_LISTENER_SECURITY_PROTOCOL_MAP': 'PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT',
    'KAFKA_INTER_BROKER_LISTENER_NAME': 'PLAINTEXT', 'KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR': '1',
    'KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR': '1', 'KAFKA_TRANSACTION_STATE_LOG_MIN_ISR': '1',
    'KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS': '0',
}


def command(args, log, timeout=120):
    run.command(args, log, timeout=timeout)


def trace_check(folder, metrics):
    command(['dotnet', INPUTS / 'inspector/AdminJitTraceInspector.dll', folder / 'consumer.nettrace',
             folder / 'trace-events.json'], folder / 'trace-parse.log')
    events = json.loads((folder / 'trace-events.json').read_text())
    pid = int((folder / 'consumer.pid').read_text())
    events = [e for e in events if e['ProcessID'] == pid]
    if len(events) < 2:
        raise ValueError('Missing traced consumer identity')
    first, last = min(events, key=lambda e: e['TimeStampQPC']), max(events, key=lambda e: e['TimeStampQPC'])
    ticks_per_ms = (last['TimeStampQPC']-first['TimeStampQPC']) / (last['Milliseconds']-first['Milliseconds'])
    if not math.isclose(ticks_per_ms, metrics['StopwatchFrequency']/1000, rel_tol=1e-7):
        raise ValueError('Trace and workload clock scales differ')
    if not first['TimeStampQPC'] < metrics['MeasurementStart'] < metrics['MeasurementEnd'] < last['TimeStampQPC']:
        raise ValueError('Trace does not span workload measurement')
    measured = [e for e in events if metrics['MeasurementStart'] <= e['TimeStampQPC'] <= metrics['MeasurementEnd']]
    result = dict(pid=pid, ticks_per_ms=ticks_per_ms, first_qpc=first['TimeStampQPC'], last_qpc=last['TimeStampQPC'],
                  measurement_start=metrics['MeasurementStart'], measurement_end=metrics['MeasurementEnd'],
                  measured_jit=[e for e in measured if e['Kind']=='jit'], measured_load=[e for e in measured if e['Kind']=='load'])
    (folder / 'trace-correlation.json').write_text(json.dumps(result, indent=2))
    return result


def execute_client(client, broker, topic, folder, warmup, seconds, rate, traced):
    relative = folder.relative_to(RESULTS).as_posix()
    command(['docker','run','--name',client,'--network',f'container:{broker}','--cpuset-cpus','1-4',
             '-e','DOTNET_TieredCompilation=1','-e','DOTNET_GCDynamicAdaptationMode=0',
             '-v',f'{INPUTS.as_posix()}:/inputs:ro','-v',f'{WORK.as_posix()}:/scripts:ro',
             '-v',f'{RESULTS.as_posix()}:/results','--entrypoint','bash',SDK,
             '/scripts/workload.sh',f'/results/{relative}',topic,str(warmup),str(seconds),str(rate),str(traced).lower()],
            folder/'container.log',timeout=480)


def workload(label, warmup, seconds, rate, traced):
    folder = RESULTS / label
    folder.mkdir(parents=True, exist_ok=False)
    suffix = uuid.uuid4().hex[:12]
    broker = f'dekaf-3117-jit-{suffix}'
    client = f'dekaf-3117-client-{suffix}'
    topic = f'dispatch-jit-{suffix}'
    seed_client = client + '-seed'
    (folder / 'containers.json').write_text(json.dumps(dict(broker=broker, client=client, topic=topic), indent=2))
    try:
        args = ['docker', 'run', '-d', '--name', broker, '--cpuset-cpus', '0']
        for key, value in BROKER_ENV.items():
            args += ['-e', f'{key}={value}']
        command(args+[KAFKA], folder / 'broker-start.log')
        deadline = time.monotonic()+120
        with (folder / 'broker-ready.log').open('w') as log:
            while time.monotonic() < deadline:
                ready = subprocess.run(['docker','exec',broker,'/opt/kafka/bin/kafka-topics.sh',
                                        '--bootstrap-server','localhost:9092','--list'], stdout=log, stderr=subprocess.STDOUT, timeout=20)
                if ready.returncode == 0:
                    break
                time.sleep(2)
            else:
                raise TimeoutError('Broker readiness expired')
        seed_folder = folder / 'seed'
        seed_folder.mkdir()
        seed_topic = topic + '-seed'
        command(['docker','exec',broker,'/opt/kafka/bin/kafka-topics.sh','--bootstrap-server','localhost:9092',
                 '--create','--topic',seed_topic,'--partitions','4','--replication-factor','1'],seed_folder/'topic.log')
        seed_group = seed_topic + '-group'
        command(['docker','exec',broker,'/opt/kafka/bin/kafka-consumer-groups.sh','--bootstrap-server','localhost:9092',
                 '--group',seed_group,'--reset-offsets','--topic',seed_topic,'--to-offset','0','--execute','--timeout','30000'],
                seed_folder/'coordinator-ready.log')
        rows = [line.split() for line in (seed_folder/'coordinator-ready.log').read_text().splitlines()]
        offsets = [row for row in rows if len(row)==4 and row[0]==seed_group and row[1]==seed_topic]
        if len(offsets)!=4 or {row[2] for row in offsets}!={'0','1','2','3'} or any(row[3]!='0' for row in offsets):
            raise ValueError('Broker preparation did not confirm all four zero offsets')
        execute_client(seed_client, broker, seed_topic, seed_folder, 2, 2, 1000, False)
        seed_metrics = run.validate_loaded(seed_folder, 2, 2, 1000, acceptance=False)
        (seed_folder/'validated-metrics.json').write_text(json.dumps(seed_metrics, indent=2))
        command(['docker','exec',broker,'/opt/kafka/bin/kafka-topics.sh','--bootstrap-server','localhost:9092',
                 '--create','--topic',topic,'--partitions','4','--replication-factor','1'],folder/'topic.log')
        execute_client(client, broker, topic, folder, warmup, seconds, rate, traced)
        metrics = run.validate_loaded(folder,warmup,seconds,rate,acceptance=warmup==121)
        run.latency_series(folder,metrics)
        if traced:
            correlation=trace_check(folder,metrics)
            metrics['TraceMeasuredJitStarts']=len(correlation['measured_jit'])
        (folder/'validated-metrics.json').write_text(json.dumps(metrics,indent=2))
        print(json.dumps(dict(label=label,throughput=metrics['MessagesPerSecond'],cpu_ns=metrics['CpuNsPerMessage'],
                              maximum_ns=metrics['MaxNs'],jit=metrics['MeasuredJitDelta'],traced=traced)),flush=True)
    finally:
        cleanup_errors = []
        for name in (client,seed_client,broker):
            with (folder/f'{name}-inspect.json').open('w') as log:
                exists=subprocess.run(['docker','inspect',name],stdout=log,stderr=subprocess.DEVNULL).returncode==0
            if exists:
                with (folder/f'{name}.log').open('w') as log:
                    subprocess.run(['docker','logs',name],stdout=log,stderr=subprocess.STDOUT)
                try:
                    command(['docker','rm','-f','-v',name],folder/f'{name}-cleanup.log')
                except subprocess.SubprocessError as error:
                    cleanup_errors.append(str(error))
        if cleanup_errors:
            raise RuntimeError('Container cleanup failed: ' + '; '.join(cleanup_errors))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage',choices=['smoke','measure'])
    args=parser.parse_args()
    RESULTS.mkdir(parents=True,exist_ok=True)
    if args.stage=='smoke':
        workload('smoke-coordinator-ready',2,2,1000,True)
    else:
        if not (RESULTS/'smoke-coordinator-ready/trace-correlation.json').is_file():
            raise ValueError('Validated trace smoke required before measurement')
        for label,traced in [('U1-coordinator-ready',False),('T-coordinator-ready',True),('U2-coordinator-ready',False)]:
            workload(label,121,120,50000,traced)


if __name__=='__main__':
    main()
