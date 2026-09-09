import hashlib
import importlib.util
import json
from pathlib import Path
import sys

root = Path(sys.argv[1])
output = Path(sys.argv[2])
repo = Path(__file__).resolve().parents[4]
spec = importlib.util.spec_from_file_location('validate_results', repo/'tools/AdminMutationEvidence/validate_results.py')
validation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validation)
plan = json.loads((root/'plan.json').read_text())
assert plan['A'] == plan['B']
for entry in json.loads((root/'inventory.json').read_text()):
    path = root / entry['path']
    assert path.resolve().is_relative_to(root.resolve()), entry['path']
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    assert digest == entry['sha256'] and path.stat().st_size == entry['bytes'], entry['path']
code = json.loads((root/'review-code-and-cpu.json').read_text())
rows = []
for phase in ['A1','B','A2']:
    folder = root/phase/'legacy-delete-16'
    warm = validation.validate(folder/'warmup.json',480)
    data = validation.validate(folder/'measured.json',180)
    trace = json.loads((folder/'suspensions-by-thread-review.json').read_text())
    assert trace['EventsLost']==0 and not trace['Anomalies'] and not trace['OpenSuspensions']
    markers = {row['Payload']['name']: row['Milliseconds'] for row in trace['Phases'] if 'name' in row['Payload']}
    pauses = [row for row in trace['AllSuspensions'] if markers['measured']<=row['StartMs']<markers['finalize']]
    metrics = {key:data[key] for key in ['CallsPerSecond','CpuNsPerCall','AllocatedBytesPerCall','P50Ns','P99Ns','MaxNs']}
    maximum = max(data['Intervals'], key=lambda row: max(v['Ticks'] for v in row['Latencies']))
    start = markers['measured']+(maximum['Start']['Seconds']-data['Start']['Seconds'])*1000
    end = markers['measured']+(maximum['End']['Seconds']-data['Start']['Seconds'])*1000
    groups = {}
    for reason in sorted({row['Reason'] for row in pauses}):
        selected = [row for row in pauses if row['Reason']==reason]
        groups[reason] = dict(count=len(selected), total_fully_suspended_ms=sum(row['FullySuspendedMs'] for row in selected),
            largest_request_to_restart=max(selected,key=lambda row:row['DurationMs']),
            largest_fully_suspended=max(selected,key=lambda row:row['FullySuspendedMs']))
    native = next(row for row in code['rows'] if row['phase']==phase)
    row = dict(phase=phase,metrics=metrics,warmup_seconds=warm['Seconds'],warmup_completed=warm['Completed'],
        seconds=data['Seconds'],completed=data['Completed'],trace_events_lost=trace['EventsLost'],
        trace_suspensions=len(trace['AllSuspensions']), trace_anomalies=trace['Anomalies'],
        measured_suspensions=groups,gen0_gen1_gen2=[data['End'][key]-data['Start'][key] for key in ['Gen0','Gen1','Gen2']],
        delivered_measured_jit=native['measured_jit'],cpu_blocks=native['cpu_blocks'],
        max_interval=dict(start_trace_ms=start,end_trace_ms=end,start_seconds=maximum['Start']['Seconds'],
            largest_suspensions=sorted([r for r in pauses if r['StartMs']<end and r['EndMs']>start],key=lambda r:r['DurationMs'],reverse=True)[:5]),
        heap_range=[min(r['End']['HeapBytes'] for r in data['Intervals']),max(r['End']['HeapBytes'] for r in data['Intervals'])],
        rss_range=[min(r['End']['RssBytes'] for r in data['Intervals']),max(r['End']['RssBytes'] for r in data['Intervals'])],
        max_workers=max(r['End']['ThreadPoolThreads'] for r in data['Intervals']),
        max_pending=max(r['End']['PendingWorkItems'] for r in data['Intervals']))
    rows.append(row)
files = []
for label in ['A','B']:
    files.append({path.name:hashlib.sha256(path.read_bytes()).hexdigest() for path in (root/'binaries'/label).iterdir() if path.is_file()})
assert files[0]==files[1]
for phase in ['A1','B','A2']:
    loaded=json.loads((root/phase/'legacy-delete-16/binaries.json').read_text())
    for binary in loaded:
        assert files[0][Path(binary['Path']).name]==binary['Sha256']
metrics = list(rows[0]['metrics'])
deltas = {metric:dict(B_vs_A1_percent=(rows[1]['metrics'][metric]/rows[0]['metrics'][metric]-1)*100,
    B_vs_A2_percent=(rows[1]['metrics'][metric]/rows[2]['metrics'][metric]-1)*100,
    A2_vs_A1_percent=(rows[2]['metrics'][metric]/rows[0]['metrics'][metric]-1)*100) for metric in metrics}
result=dict(plan=plan,rows=rows,deltas=deltas,binary_files=len(files[0]),shared_tier1_methods=code['shared_tier1_methods'],
    changed_instruction_bodies=code['changed_instruction_bodies'],
    scope='Diagnostic only. Profiled identical-product control; not product acceptance. Max-call interval alignment has only one-second resolution in this historical capture.')
output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(result,indent=2),encoding='utf-8')
print('Validated',len(rows),'warmup/measured pairs; identical',len(files[0]),'file trees and loaded modules; all thread suspension sequences valid.')
print(json.dumps(deltas,indent=2))
