import hashlib,json,pathlib,subprocess,os,datetime
root=pathlib.Path(__file__).resolve().parent
cases=['ByteArray','ReadOnlyMemory','Memory','ArraySegment','String','Int32','NullableInt32','CustomString','NullKinds']
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
inventories={}
for phase in ['A','B']:
 inventories[phase]={str(p.relative_to(root/'hosts'/phase)):digest(p) for p in (root/'hosts'/phase).rglob('*') if p.is_file()}
assert inventories['A'].keys()==inventories['B'].keys()
changed=[p for p in inventories['A'] if inventories['A'][p]!=inventories['B'][p]]
assert set(changed)=={'Dekaf.dll','Dekaf.pdb'},changed
(root/'host-inventories.json').write_text(json.dumps(inventories,indent=2))
(root/'run-settings.json').write_text(json.dumps(dict(baseline='766b9c605104906e4580c44a95392c94942c089b',candidate='fa2c5b88b262a342d08a9f9315fdf4fd3047bd31',main='5df2f0d03607389384b5c1466e17812a9084fac9',cases=cases,tieredCompilation=1,tieredPGO=1,gcServer=0,affinityMask=4),indent=2))
env=os.environ.copy()
env.update(DOTNET_TieredCompilation='1',DOTNET_TieredPGO='1',DOTNET_gcServer='0')
records=[]
for phase,host,kind in [('A1','A','baseline'),('B','B','candidate'),('A2','A','baseline')]:
 for case in cases:
  output=root/'measure'/phase/case
  output.mkdir(parents=True,exist_ok=False)
  command=['dotnet',str(root/'hosts'/host/'Dekaf.Benchmarks.dll'),case,str(output),'measure',kind]
  record=dict(phase=phase,case=case,start=datetime.datetime.now(datetime.timezone.utc).isoformat(),command=command)
  print('START',phase,case,flush=True)
  with (output/'console.log').open('w') as log:
   result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,env=env,cwd=root.parents[2])
  record.update(end=datetime.datetime.now(datetime.timezone.utc).isoformat(),exitCode=result.returncode)
  records.append(record)
  (root/'phase-execution.json').write_text(json.dumps(records,indent=2))
  if result.returncode: raise SystemExit(f'Benchmark failed: {phase}/{case}: {result.returncode}')
  loaded=json.loads((output/'loaded.json').read_text())
  for assembly in loaded:
   assert assembly['sha256'].lower()==inventories[host][pathlib.Path(assembly['path']).name]
  warmup=json.loads((output/'warmup.json').read_text())
  assert warmup['seconds']>=20 and warmup['operations']>0
  print('DONE',phase,case,flush=True)
print('ALL PHASES COMPLETE',flush=True)
