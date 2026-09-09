"""Replay the predeclared sampler control; never grant product acceptance."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

REPO = Path(__file__).resolve().parents[4]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def call_bounds(call, clock, event_ms, frequency):
    scale = 1000 / frequency
    return dict(start_earliest_ms=event_ms+(call['StartTimestamp']-clock['AfterTimestamp'])*scale,
                start_latest_ms=event_ms+(call['StartTimestamp']-clock['BeforeTimestamp'])*scale,
                end_earliest_ms=event_ms+(call['EndTimestamp']-clock['AfterTimestamp'])*scale,
                end_latest_ms=event_ms+(call['EndTimestamp']-clock['BeforeTimestamp'])*scale)


def overlap(bounds, start, end):
    if bounds['start_earliest_ms'] >= end or bounds['end_latest_ms'] <= start:
        return 'none'
    if bounds['start_latest_ms'] < end and bounds['end_earliest_ms'] > start:
        return 'guaranteed within clock bounds'
    return 'possible within clock bounds'


def analyze(root):
    plan = json.loads((root/'plan.json').read_text())
    assert plan['sampler_control'] and plan['A']==plan['B']
    assert plan['warmup_seconds']==480 and plan['measured_seconds']==180
    inventory = json.loads((root/'inventory.json').read_text())
    for entry in inventory:
        path = root/entry['path']
        assert path.resolve().is_relative_to(root.resolve())
        with path.open('rb') as stream:
            digest = hashlib.file_digest(stream,'sha256').hexdigest()
        assert digest==entry['sha256'] and path.stat().st_size==entry['bytes'], entry['path']
    trees = []
    for label in ['A','B']:
        trees.append({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'binaries'/label).iterdir() if p.is_file()})
    assert trees[0]==trees[1]
    validation = module('admin_validation',REPO/'tools/AdminMutationEvidence/validate_results.py')
    timing = module('admin_timing',REPO/'.github/scripts/admin_timing.py')
    rows = []
    for phase in ['A1','B','A2']:
        folder = root/phase/'legacy-delete-16'
        warm = timing.validate_timing(validation.validate(folder/'warmup.json',480))
        data = timing.validate_timing(validation.validate(folder/'measured.json',180))
        for binary in json.loads((folder/'binaries.json').read_text()):
            assert trees[0][Path(binary['Path']).name]==binary['Sha256']
        trace = json.loads((folder/'suspensions-by-thread-review.json').read_text())
        assert trace['EventsLost']==0 and not trace['Anomalies'] and not trace['OpenSuspensions']
        sampled = trace['ProviderCounts'].get('Microsoft-DotNETCore-SampleProfiler',0)
        assert (sampled>0)==(phase=='B'), (phase,sampled)
        assert ('Microsoft-DotNETCore-SampleProfiler' in plan['phase_providers'][phase])==(phase=='B')
        clock = data['TraceClock']
        anchors = [p for p in trace['Phases'] if p['EventName']=='Clock' and p['Payload']['timestamp']==clock['BeforeTimestamp']]
        assert len(anchors)==1
        event_ms = anchors[0]['Milliseconds']
        marker = {p['Payload']['name']:p['Milliseconds'] for p in trace['Phases'] if 'name' in p['Payload']}
        pauses = [p for p in trace['AllSuspensions'] if marker['measured']<=p['StartMs']<marker['finalize']]
        interval = max(data['Intervals'],key=lambda r:r['MaximumCall']['Ticks'])
        bounds = call_bounds(interval['MaximumCall'],clock,event_ms,data['StopwatchFrequency'])
        nearby=[]
        for pause in pauses:
            request = overlap(bounds,pause['StartMs'],pause['EndMs'])
            suspended = overlap(bounds,pause['SuspendedMs'],pause['RestartMs'])
            if request!='none' or suspended!='none':
                nearby.append(dict(**pause,request_overlap=request,fully_suspended_overlap=suspended))
        reasons={}
        for reason in sorted({p['Reason'] for p in pauses}):
            selected=[p for p in pauses if p['Reason']==reason]
            reasons[reason]=dict(count=len(selected),total_fully_suspended_ms=sum(p['FullySuspendedMs'] for p in selected),
                                 max_fully_suspended_ms=max(p['FullySuspendedMs'] for p in selected))
        row=dict(phase=phase,metrics={k:data[k] for k in ['CallsPerSecond','CpuNsPerCall','AllocatedBytesPerCall','P50Ns','P99Ns','MaxNs']},
                 warmup_completed=warm['Completed'],measured_completed=data['Completed'],warmup_seconds=warm['Seconds'],measured_seconds=data['Seconds'],
                 clock_bracket_ns=(clock['AfterTimestamp']-clock['BeforeTimestamp'])*1e9/data['StopwatchFrequency'],
                 maximum_call=interval['MaximumCall'],maximum_call_trace_bounds=bounds,maximum_call_overlapping_suspensions=nearby,
                 sample_profiler_provider_events_whole_trace=sampled,measured_suspensions=reasons,events_lost=trace['EventsLost'],anomalies=trace['Anomalies'])
        rows.append(row)
        print(phase,json.dumps(row['metrics']),'clock ns',row['clock_bracket_ns'],'overlaps',[(p['Reason'],p['request_overlap'],p['fully_suspended_overlap']) for p in nearby])
    deltas={k:dict(B_vs_A1_percent=(rows[1]['metrics'][k]/rows[0]['metrics'][k]-1)*100,
                   B_vs_A2_percent=(rows[1]['metrics'][k]/rows[2]['metrics'][k]-1)*100,
                   A2_vs_A1_percent=(rows[2]['metrics'][k]/rows[0]['metrics'][k]-1)*100) for k in rows[0]['metrics']}
    result=dict(scope='Diagnostic only: sampler intentionally differs. Never product acceptance.',plan=plan,rows=rows,deltas=deltas,verified_files=len(inventory),binary_files=len(trees[0]))
    (root/'sampler-control-review.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    return result


if __name__=='__main__':
    analyze(Path(sys.argv[1]))
