import hashlib, json, subprocess, uuid
from pathlib import Path
root=Path.cwd(); work=root/'.artifacts/retained-codegen'
image='sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c'
pins=json.loads((work/'pins.json').read_text())
name='dekaf-3116-codegen-'+uuid.uuid4().hex[:12]
(work/'container-name.txt').write_text(name)
script='''set -euo pipefail
dotnet --info > /work/environment.log
lscpu >> /work/environment.log
for label in A B; do
  sha=$(python3 -c "import json; print(json.load(open('/work/pins.json'))['$label'])")
  dotnet build /work/product-$label/diagnostic/Probe.csproj -c Release -f net10.0 -p:SourceRevisionId=$sha -p:RepositoryCommit=$sha -p:RunAnalyzers=false -p:UseSharedCompilation=false > /work/build-$label.log 2>&1
done
dotnet build-server shutdown > /work/build-server-shutdown.log 2>&1
for phase in A1 B A2; do
  label=${phase:0:1}
  DOTNET_TieredCompilation=0 DOTNET_JitDisasm='*Traverse*' DOTNET_JitStdOutFile=/work/disasm-$phase.log taskset -c 2 dotnet /work/product-$label/diagnostic/bin/Release/net10.0/Dekaf.Benchmarks.dll /work/layout-$phase.json > /work/probe-$phase.log 2>&1
done
'''
# Avoid relying on Python in the SDK container: the immutable pins are substituted before launch.
script=script.replace('  sha=$(python3 -c "import json; print(json.load(open(\'/work/pins.json\'))[\'$label\'])")',f"  if [ \"$label\" = A ]; then sha={pins['A']}; else sha={pins['B']}; fi")
(work/'run.sh').write_text(script,encoding='utf-8',newline='\n')
try:
    with (work/'container.log').open('w',encoding='utf-8') as log:
        subprocess.run(['docker','run','--name',name,'--cpuset-cpus','0-4','-e','NUGET_PACKAGES=/work/.nuget','-v',f'{work.as_posix()}:/work','--entrypoint','bash',image,'/work/run.sh'],stdout=log,stderr=subprocess.STDOUT,check=True,timeout=900)
finally:
    with (work/'container-inspect.json').open('w',encoding='utf-8') as log: exists=subprocess.run(['docker','inspect',name],stdout=log).returncode==0
    if exists:
        with (work/'cleanup.log').open('w',encoding='utf-8') as log: subprocess.run(['docker','rm','-f','-v',name],stdout=log,stderr=subprocess.STDOUT,check=True)
print('Code-generation sequence completed.')
