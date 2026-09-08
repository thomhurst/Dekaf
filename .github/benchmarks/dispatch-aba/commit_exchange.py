"""Bounded broker-request attribution; failures remain failures and are never retried."""
import json
from pathlib import Path
import subprocess
import time
import uuid

import linux_jit as jit
import run

ROOT=Path(__file__).resolve().parents[3]
WORK=ROOT/'.artifacts/commit-exchange'
RESULTS=WORK/'results'
INPUTS=Path('C:/git/Dekaf-evidence/pr-3117/linux-jit-20260908/raw/inputs')
TOPIC='dispatch-commit-diagnosis'


def capture(args,path,timeout=120):
    with path.open('w') as log:
        return subprocess.run([str(a) for a in args],stdout=log,stderr=subprocess.STDOUT,timeout=timeout).returncode


def required(args,path,timeout=120):
    code=capture(args,path,timeout)
    if code:
        raise RuntimeError(f'Command failed ({code}); see {path}')


def workload(label):
    folder=RESULTS/label
    folder.mkdir(parents=True,exist_ok=False)
    suffix=uuid.uuid4().hex[:12]
    broker=f'dekaf-3117-commit-{suffix}'
    client=f'dekaf-3117-commit-client-{suffix}'
    (folder/'containers.json').write_text(json.dumps(dict(broker=broker,client=client),indent=2))
    try:
        args=['docker','run','-d','--name',broker,'--cpuset-cpus','0']
        for key,value in jit.BROKER_ENV.items():
            args += ['-e',f'{key}={value}']
        required(args+[jit.KAFKA],folder/'broker-start.log')
        deadline=time.monotonic()+120
        while time.monotonic()<deadline:
            if capture(['docker','exec',broker,'/opt/kafka/bin/kafka-topics.sh','--bootstrap-server','localhost:9092','--list'],folder/'broker-ready.log',20)==0:
                break
            time.sleep(2)
        else:
            raise TimeoutError('Broker readiness expired')
        required(['docker','exec',broker,'/opt/kafka/bin/kafka-configs.sh','--bootstrap-server','localhost:9092',
                  '--entity-type','broker-loggers','--entity-name','1','--alter','--add-config','kafka.request.logger=DEBUG'],folder/'logger-change.log')
        required(['docker','exec',broker,'/opt/kafka/bin/kafka-configs.sh','--bootstrap-server','localhost:9092',
                  '--entity-type','broker-loggers','--entity-name','1','--describe'],folder/'logger-state.log')
        required(['docker','exec',broker,'/opt/kafka/bin/kafka-topics.sh','--bootstrap-server','localhost:9092',
                  '--create','--topic',TOPIC,'--partitions','4','--replication-factor','1'],folder/'topic.log')
        code=capture(['docker','run','--name',client,'--network',f'container:{broker}','--cpuset-cpus','1-4',
                      '-e','DOTNET_TieredCompilation=1','-e','DOTNET_GCDynamicAdaptationMode=0',
                      '-e',f'DIAG_CONSUMER_LABEL={label}','-v',f'{INPUTS.as_posix()}:/inputs:ro',
                      '-v',f'{WORK.as_posix()}:/scripts:ro','-v',f'{RESULTS.as_posix()}:/results',
                      '--entrypoint','bash',jit.SDK,'/scripts/workload.sh',f'/results/{label}',TOPIC,'2','2','1000','false'],folder/'container.log')
        outcome=dict(label=label,exit_code=code,correctness='FAILED' if code else 'UNVERIFIED')
        if code==0:
            metrics=run.validate_loaded(folder,2,2,1000,acceptance=False)
            outcome.update(correctness='PASSED',completed=metrics['Completed'],committed=metrics['CommittedOffsets'])
        (folder/'outcome.json').write_text(json.dumps(outcome,indent=2))
        print(json.dumps(outcome),flush=True)
    finally:
        capture(['docker','logs',broker],folder/'broker.log')
        capture(['docker','exec',broker,'sh','-c','ls -l /opt/kafka/logs'],folder/'broker-log-files.txt')
        capture(['docker','cp',f'{broker}:/opt/kafka/logs',str(folder/'broker-files')],folder/'broker-log-copy.log')
        errors=[]
        for name in (client,broker):
            exists=capture(['docker','inspect',name],folder/f'{name}-inspect.json')==0
            if exists:
                code=capture(['docker','rm','-f','-v',name],folder/f'{name}-cleanup.log')
                if code:
                    errors.append(name)
        if errors:
            raise RuntimeError(f'Cleanup failed: {errors}')


if __name__=='__main__':
    for label in ('A','B'):
        workload(label)
