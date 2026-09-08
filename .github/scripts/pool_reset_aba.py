"""Task-scoped exact-SHA comparison. Never merge this experimental branch."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,sys
ROOT=Path.cwd()
OUT=ROOT/"evidence"
A=os.environ['BASELINE_SHA']
B=os.environ['CANDIDATE_SHA']
FIXTURE=ROOT/".github/benchmarks/pool-reset"
def run(args,log,cwd=ROOT):
    with log.open("w") as output:
        subprocess.run([str(x) for x in args],cwd=cwd,stdout=output,stderr=subprocess.STDOUT,check=True)
if sys.argv[1]=="prepare":
    OUT.mkdir()
    subprocess.run(["git","merge-base","--is-ancestor",A,B],check=True)
    manifest=dict(A=A,B=B,harness=subprocess.check_output(["git","rev-parse","HEAD"],text=True).strip(),mainAtRun=subprocess.check_output(["git","ls-remote","origin","refs/heads/main"],text=True).strip(),imageVersion=os.getenv("ImageVersion"),imageOS=os.getenv("ImageOS"),runnerName=os.getenv("RUNNER_NAME"),settings={k:os.getenv(k) for k in ["DOTNET_TieredCompilation","DOTNET_TieredPGO","DOTNET_ReadyToRun"]})
    (OUT/"manifest.json").write_text(json.dumps(manifest,indent=2))
    shutil.copytree(FIXTURE,OUT/"fixture")
    shutil.copy2(__file__,OUT/"pool_reset_aba.py")
    run(["dotnet","--info"],OUT/"dotnet-info.txt")
    run(["lscpu"],OUT/"hardware.txt")
    for tag,sha in [("A",A),("B",B)]:
        source=ROOT/("source-"+tag)
        run(["git","worktree","add","--detach",source,sha],OUT/(tag+"-checkout.log"))
        run(["git","archive","--format=zip","--output="+str(OUT/(tag+"-source.zip")),sha],OUT/(tag+"-archive.log"))
        run(["dotnet","publish","src/Dekaf/Dekaf.csproj","-c","Release","-f","net10.0","--disable-build-servers","-p:UseSharedCompilation=false","-o",OUT/"products"/tag],OUT/(tag+"-build.log"),source)
    run(["dotnet","build",FIXTURE/"Harness.csproj","-c","Release","--disable-build-servers","-p:UseSharedCompilation=false"],OUT/"fixture-build.log")
    for tag in ["A","B"]:
        host=OUT/"hosts"/tag
        shutil.copytree(FIXTURE/"bin/Release/net10.0",host)
        for name in ["Dekaf.dll","Dekaf.pdb","Dekaf.Abstractions.dll","Reservoir.dll"]:
            shutil.copy2(OUT/"products"/tag/name,host/name)
        run(["dotnet",host/"Dekaf.Benchmarks.dll",OUT/("smoke-"+tag),"--smoke"],OUT/("smoke-"+tag+".log"))
    (OUT/"host-bindings.json").write_text(json.dumps([dict(path=str(p.relative_to(OUT)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in (OUT/"hosts").rglob("*") if p.is_file()],indent=2))
    run(["dotnet","build-server","shutdown"],OUT/"build-server-shutdown.log")
    run(["git","archive","--format=zip","--output="+str(OUT/"harness-source.zip"),"HEAD"],OUT/"harness-archive.log")
else:
    phase=sys.argv[1]
    if phase not in ["A1","B","A2"]: raise ValueError(phase)
    if (OUT/phase).exists(): raise FileExistsError(OUT/phase)
    tag="B" if phase=="B" else "A"
    run(["dotnet",OUT/"hosts"/tag/"Dekaf.Benchmarks.dll",OUT/phase],OUT/(phase+".log"))
