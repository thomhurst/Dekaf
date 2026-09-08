import csv, hashlib, json, math, pathlib, re
root=pathlib.Path(__file__).resolve().parent
cases=['ByteArray','ReadOnlyMemory','Memory','ArraySegment','String','Int32','NullableInt32','CustomString','NullKinds']
phases=['A1','B','A2']
def require(condition,message):
 if not condition: raise ValueError(message)
def read_case(phase,case):
 folder=root/'measure'/phase/case
 reports=list((folder/'results').glob('*full.json'))
 require(len(reports)==1,f'{phase}/{case}: expected one report')
 report=json.loads(reports[0].read_text(encoding='utf-8-sig'))
 require(len(report['Benchmarks'])==1,'wrong benchmark count')
 benchmark=report['Benchmarks'][0]
 require(benchmark['MethodTitle']==case,'wrong method')
 measurements=benchmark['Measurements']
 actual=[m for m in measurements if m['IterationMode']=='Workload' and m['IterationStage']=='Actual']
 results=[m for m in measurements if m['IterationMode']=='Workload' and m['IterationStage']=='Result']
 warm=[m for m in measurements if m['IterationMode']=='Workload' and m['IterationStage']=='Warmup']
 require(len(actual)==15 and len(results)==15 and len(warm)==10,'missing workload samples')
 count=67108864 if case in ('Int32','NullableInt32','NullKinds') else 16777216
 require(all(m['Operations']==count for m in actual+results+warm),'wrong invocation count')
 require({m['IterationIndex'] for m in actual}==set(range(1,16)),'duplicate or missing actual index')
 stats=benchmark['Statistics']
 require(stats['N']==15 and len(stats['OriginalValues'])==15,'missing statistics samples')
 values=[m['Nanoseconds']/m['Operations'] for m in results]
 require(all(math.isclose(a,b,rel_tol=1e-12,abs_tol=1e-12) for a,b in zip(sorted(values),sorted(stats['OriginalValues']))),'result/statistics mismatch')
 require(math.isclose(max(values),stats['Max'],rel_tol=1e-12,abs_tol=1e-12),'lost maximum')
 warmup=json.loads((folder/'warmup.json').read_text())
 require(warmup['seconds']>=20 and warmup['operations']>0,'insufficient direct warmup')
 hosts=json.loads((root/'host-inventories.json').read_text())
 host='B' if phase=='B' else 'A'
 for assembly in json.loads((folder/'loaded.json').read_text()):
  name=pathlib.Path(assembly['path']).name
  require(assembly['sha256'].lower()==hosts[host][name],'loaded assembly mismatch')
  require(hashlib.sha256((root/'hosts'/host/name).read_bytes()).hexdigest()==hosts[host][name],'retained assembly mismatch')
 runtime=list(csv.DictReader((folder/'runtime.csv').open()))
 actual_runtime=[r for r in runtime if r['workload'].startswith('WorkloadActual')]
 warm_runtime=[r for r in runtime if r['workload'].startswith('WorkloadWarmup')]
 require(len(actual_runtime)==15 and len(warm_runtime)==10,'missing runtime intervals')
 log=(folder/'console.log').read_text(encoding='utf-8-sig')
 raw=re.findall(r'RAW operations=1000 bytes=(\d+)',log)
 require(len(raw)==1,'missing exact allocation probe')
 sizes=re.findall(r'SIZES .*',log)
 require(len(sizes)==1,'missing sizes')
 observed=[warm_runtime[-1]]+actual_runtime
 return dict(meanNs=stats['Mean'],minNs=stats['Min'],maxNs=stats['Max'],ci=stats['ConfidenceInterval'],n=15,
  bytesPerOperation=benchmark['Memory']['BytesAllocatedPerOperation'],allocationProbeBytes=int(raw[0]),
  actualOperations=sum(m['Operations'] for m in actual),actualSeconds=sum(m['Nanoseconds'] for m in actual)/1e9,
  directWarmup=warmup,bdnWarmupSeconds=sum(m['Nanoseconds'] for m in warm)/1e9,
  jitDelta=int(observed[-1]['jit_methods'])-int(observed[0]['jit_methods']),
  jitTimeMs=float(observed[-1]['jit_ms'])-float(observed[0]['jit_ms']),
  jitIntervals=[int(b['jit_methods'])-int(a['jit_methods']) for a,b in zip(observed,observed[1:])],
  threadMin=min(int(r['threads']) for r in observed),threadMax=max(int(r['threads']) for r in observed),
  pendingMax=max(int(r['pending_work']) for r in observed),
  gcDeltas=[int(observed[-1][k])-int(observed[0][k]) for k in ['gc0','gc1','gc2']],
  heapRange=[min(int(r['heap_bytes']) for r in observed),max(int(r['heap_bytes']) for r in observed)],
  rssRange=[min(int(r['rss_bytes']) for r in observed),max(int(r['rss_bytes']) for r in observed)],
  cpuBoundaryDeltaMs=float(observed[-1]['cpu_ms'])-float(observed[0]['cpu_ms']),
  sizes=sizes[0],samples=values,actualMeasurements=actual)
def delta(b,a): return 100*(b/a-1)
rows=[]
for case in cases:
 data={phase:read_case(phase,case) for phase in phases}
 require(len({d['actualOperations'] for d in data.values()})==1,'different completed operations')
 require(len({d['sizes'] for d in data.values()})==1,'key layout grew')
 a,b,c=[data[p] for p in phases]
 drift=delta(c['meanNs'],a['meanNs'])
 deltas=[delta(b['meanNs'],a['meanNs']),delta(b['meanNs'],c['meanNs'])]
 same_work=case!='NullKinds'
 startup=any(d['jitDelta']!=0 or d['threadMin']!=d['threadMax'] for d in data.values())
 zero=all(d['bytesPerOperation']==0 and d['allocationProbeBytes']==0 for d in data.values())
 within=all(b['ci']['Upper']<=control['ci']['Lower']*1.03 for control in [a,c])
 loss=all(b['ci']['Lower']>control['ci']['Upper']*1.03 for control in [a,c])
 screen='CHANGED-SEMANTICS' if not same_work else 'INCONCLUSIVE'
 if same_work and abs(drift)<=2 and zero and not startup:
  if within: screen='WITHIN-DIAGNOSTIC-BOUND'
  elif loss: screen='REGRESSION'
 rows.append(dict(case=case,phases=data,deltasPct=deltas,controlDriftPct=drift,startupTransitions=startup,
  zeroAllocation=zero,conservativeTimingLoss=loss,conservativeWithinBound=within,diagnosticScreen=screen))
output=dict(productVerdict='INCONCLUSIVE',reason='Local incremental microbenchmarks do not supply fresh-main Ubuntu end-to-end dispatch CPU, message latency, zero-allocation and sustained-stability acceptance; the previous large-key regression remains unapproved.',cases=rows)
(root/'summary.json').write_text(json.dumps(output,indent=2))
lines=['# Deserialized-null incremental diagnosis','','Product acceptance: **INCONCLUSIVE**. No protected-metric tradeoff is approved.','',
 '| Key operation | A1 ns | B ns | A2 ns | B/A1 | B/A2 | Control drift | B/op A1/B/A2 |',
 '| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |']
for row in rows:
 d=row['phases'];a,b,c=[d[p] for p in phases]
 lines.append(f"| {row['case']} | {a['meanNs']:.6f} | {b['meanNs']:.6f} | {c['meanNs']:.6f} | {row['deltasPct'][0]:+.3f}% | {row['deltasPct'][1]:+.3f}% | {row['controlDriftPct']:+.3f}% | {a['bytesPerOperation']}/{b['bytesPerOperation']}/{c['bytesPerOperation']} |")
lines+=['','NullKinds intentionally changes equality from equal in A to distinct in B. It is not equivalent completed dispatch work. Other cases use identical runtime false wire flags and identical normal hash/equality outputs.','',
 'Each phase retains 15 corrected BDN iteration samples and all actual measurements, including maxima. These are iteration statistics, not per-message latency percentiles. CPU boundary deltas include BDN/logging/bookkeeping between the last warmup and last actual boundary; they are diagnostic process activity, not isolated client CPU per completed message.','',
 '| Key operation | Direct warmup seconds A1/B/A2 | Direct warmup ops A1/B/A2 | BDN warmup seconds A1/B/A2 | Measured JIT counts A1/B/A2 | Diagnostic screen |',
 '| --- | --- | --- | --- | --- | --- |']
for row in rows:
 values=[row['phases'][p] for p in phases]
 joined=lambda f:'/'.join(f(v) for v in values)
 lines.append('| '+row['case']+' | '+joined(lambda v:f"{v['directWarmup']['seconds']:.6f}")+' | '+joined(lambda v:str(v['directWarmup']['operations']))+' | '+joined(lambda v:f"{v['bdnWarmupSeconds']:.3f}")+' | '+joined(lambda v:str(v['jitDelta']))+' | '+row['diagnosticScreen']+' |')
lines+=['','Full confidence intervals, individual samples, runtime intervals, layout checks, memory scopes and loaded assembly bindings are retained in summary.json and the phase artifacts. Overlapping intervals do not prove equivalence.']
(root/'summary.md').write_text('\n'.join(lines)+'\n')
print(json.dumps(dict(verdict=output['productVerdict'],cases=len(rows),samples=len(rows)*3*15,allZero=all(r['zeroAllocation'] for r in rows))))
