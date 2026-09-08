"""Candidate-only U1/trace/U2 diagnostic of sampler priming with immutable product binaries."""
from pathlib import Path
import csv,hashlib,json,os,re,shutil,subprocess,sys,zipfile
ROOT=Path.cwd();OUT=ROOT/"evidence";FIXTURE=ROOT/".github/benchmarks/pool-jit"
ARTIFACT="10041764756";DIGEST="ede8e8e1beb3b6016ff2f779bae516720ab7cca465ff4172e10524e6bb08b3c5"
def run(args,log):
    with log.open("w") as output:subprocess.run([str(x) for x in args],stdout=output,stderr=subprocess.STDOUT,check=True)

def prepare_primer():
    fixture = ROOT / ".github/benchmarks/pool-primer"
    rebuilt = OUT / "rebuilt-fixture"
    shutil.copytree(OUT / "host", OUT / "original-host")
    shutil.copytree(fixture, OUT / "fixture-source")
    run(["dotnet", "build", fixture / "Harness.csproj", "-c", "Release",
         "--disable-build-servers", "-p:UseSharedCompilation=false",
         "-p:ArchivedHost=" + str(OUT / "host"), "-o", rebuilt], OUT / "fixture-build.log")
    replaced = {"Dekaf.Benchmarks.dll", "Dekaf.Benchmarks.pdb"}
    for name in replaced:
        shutil.copy2(rebuilt / name, OUT / "host" / name)
    bindings = []
    for original in sorted((OUT / "original-host").rglob("*")):
        if not original.is_file():
            continue
        relative = original.relative_to(OUT / "original-host")
        loaded = OUT / "host" / relative
        before = hashlib.sha256(original.read_bytes()).hexdigest()
        after = hashlib.sha256(loaded.read_bytes()).hexdigest()
        if str(relative) not in replaced and before != after:
            raise RuntimeError("Product or dependency changed: " + str(relative))
        bindings.append(dict(path=relative.as_posix(), original=before, loaded=after))
    (OUT / "fixture-bindings.json").write_text(json.dumps(bindings, indent=2))
def execute(phase,traced=False,smoke=False):
    dest=OUT/phase;dest.mkdir()
    env=dict(os.environ,DOTNET_TieredCompilation="1",DOTNET_TieredPGO="1",DOTNET_ReadyToRun="1")
    for name in ["DOTNET_DiagnosticPorts","DOTNET_DefaultDiagnosticPortSuspend","COMPlus_DiagnosticPorts","COMPlus_DefaultDiagnosticPortSuspend"]:env.pop(name,None)
    command=["dotnet",str(OUT/"host/Dekaf.Benchmarks.dll"),str(dest/"bdn")]
    if smoke:command.append("--smoke")
    tracer=None;trace_log=None;host=None;status={"phase":phase,"traced":traced,"ceilingSeconds":240}
    try:
        with (dest/"host.log").open("w") as log:
            host=subprocess.Popen(command,env=env,stdout=log,stderr=subprocess.STDOUT);status["hostPid"]=host.pid
            if traced:
                if host.poll() is not None:raise RuntimeError("Host exited before attachment")
                trace_log=(dest/"trace.log").open("w")
                tracer=subprocess.Popen([str(OUT/"trace-tool/dotnet-trace"),"collect","--process-id",str(host.pid),"--providers","Microsoft-Windows-DotNETRuntime:0x10019:5,BenchmarkDotNet.EngineEventSource:0xffffffffffffffff:5","--rundown","false","--output",str(dest/"worker.nettrace")],env=env,stdout=trace_log,stderr=subprocess.STDOUT)
                status["tracerPid"]=tracer.pid
            status["hostExit"]=host.wait(timeout=240)
            if tracer is not None:status["tracerExit"]=tracer.wait(timeout=30)
            if status["hostExit"] or status.get("tracerExit",0):raise RuntimeError(str(status))
    except Exception as error:
        status["error"]=repr(error);raise
    finally:
        for process in [host,tracer]:
            if process is not None and process.poll() is None:
                process.terminate()
                try:process.wait(timeout=10)
                except subprocess.TimeoutExpired:process.kill();process.wait()
        if trace_log:trace_log.close()
        (dest/"status.json").write_text(json.dumps(status,indent=2))
    if traced:
        run(["dotnet",OUT/"inspector/Inspector.dll",dest/"worker.nettrace",dest/"events.json"],dest/"inspect.log")
        trace=json.loads((dest/"events.json").read_text());events=[e for e in trace["events"] if e["kind"]=="engine"]
        expected=1 if smoke else 25
        if trace["lost"] or sum(e["id"]==15 for e in events)!=expected or sum(e["id"]==16 for e in events)!=expected:raise RuntimeError("Incomplete engine trace")
        expected_warmup = 1 if smoke else 50
        if any(sum(e["id"] == event_id for e in events) != expected_warmup for event_id in (13, 14)):
            raise RuntimeError("Incomplete engine warmup trace")
    benchmark=json.loads(next((dest/"bdn/results").glob("*full.json")).read_text())["Benchmarks"][0]
    warm=[m for m in benchmark["Measurements"] if m["IterationMode"]=="Workload" and m["IterationStage"]=="Warmup"]
    actual=[m for m in benchmark["Measurements"] if m["IterationMode"]=="Workload" and m["IterationStage"]=="Actual"]
    if len(actual)!=(1 if smoke else 25) or len(warm)!=(1 if smoke else 50):raise RuntimeError("Wrong BDN sample shape")
    if not smoke and sum(m["Nanoseconds"] for m in warm)<20e9:raise RuntimeError("Insufficient elapsed BDN warmup")
    setup = re.findall(r"WARM completed seconds=([\d.]+) calls=(\d+)", (dest / "host.log").read_text())
    if len(setup) != 1 or int(setup[0][1]) <= 0 or (not smoke and float(setup[0][0]) < 20):
        raise RuntimeError("Insufficient elapsed workload setup")
    primer = re.findall(r"PRIMER completed seconds=([\d.]+) callbacks=(\d+)", (dest / "host.log").read_text())
    if len(primer) != 1 or float(primer[0][0]) < (1 if smoke else 20):
        raise RuntimeError("Missing elapsed sampler primer")
    with (dest / "bdn/sampler-primer.csv").open(newline="") as stream:
        series = list(csv.DictReader(stream))
    if not series or int(series[-1]["callbacks"]) != int(primer[0][1]) or int(primer[0][1]) <= 0:
        raise RuntimeError("Incomplete sampler primer series")
    helper = json.loads((dest / "bdn/helper-primer.json").read_text())
    if helper["Seconds"] < (1 if smoke else 20) or helper["Completed"] <= 0:
        raise RuntimeError("Incomplete elapsed helper primer")
    if not helper["Samples"] or helper["Samples"][-1]["Completed"] != helper["Completed"]:
        raise RuntimeError("Incomplete helper primer series")
    bindings = json.loads((dest / "bdn/helper-bindings.json").read_text())
    if len(bindings) != 4 or len({binding["ModuleId"] for binding in bindings}) != 1:
        raise RuntimeError("Unexpected helper method bindings")
    with (dest / "bdn/runtime.csv").open(newline="") as stream:
        runtime = list(csv.DictReader(stream))
    if any("primer" in row["workload"] for row in runtime):
        raise RuntimeError("Primer samples leaked into workload samples")
    if sum(row["workload"].startswith("WorkloadActual") for row in runtime) != len(actual):
        raise RuntimeError("Missing actual runtime samples")
if sys.argv[1]=="prepare":
    OUT.mkdir()
    run(["dotnet","--info"],OUT/"dotnet-info.txt");run(["lscpu"],OUT/"hardware.txt")
    metadata=json.loads(subprocess.check_output(["gh","api","repos/thomhurst/Dekaf/actions/artifacts/"+ARTIFACT]))
    (OUT/"input-artifact.json").write_text(json.dumps(metadata,indent=2))
    path=ROOT/"input.zip"
    with path.open("wb") as target:subprocess.run(["gh","api","repos/thomhurst/Dekaf/actions/artifacts/"+ARTIFACT+"/zip"],stdout=target,check=True)
    if hashlib.sha256(path.read_bytes()).hexdigest()!=DIGEST or metadata["digest"]!="sha256:"+DIGEST:raise RuntimeError("Original artifact digest mismatch")
    source=(ROOT/"input").resolve();source.mkdir()
    with zipfile.ZipFile(path) as archive:
        for item in archive.infolist():
            if not (source/item.filename).resolve().is_relative_to(source):raise RuntimeError("Invalid input path")
        archive.extractall(source)
    bindings=json.loads((source/"host-bindings.json").read_text())
    for binding in bindings:
        p=source/binding["path"]
        if p.stat().st_size!=binding["bytes"] or hashlib.sha256(p.read_bytes()).hexdigest()!=binding["sha256"]:raise RuntimeError("Original host changed")
    shutil.copytree(source/"hosts/B",OUT/"host")
    shutil.copy2(source/"host-bindings.json",OUT/"original-host-bindings.json")
    shutil.copy2(source/"manifest.json",OUT/"original-manifest.json")
    shutil.copytree(FIXTURE,OUT/"inspector-source")
    shutil.copy2(__file__,OUT/"pool_jit_trace.py")
    manifest=dict(product="4479317a650ea51a2a2ecdc0ccf5a7de8fb51c7d",originalHarness="b26ffdc9070cc68ceaca07bf4193f334df6b2744",harness=subprocess.check_output(["git","rev-parse","HEAD"],text=True).strip(),mainAtRun=subprocess.check_output(["git","ls-remote","origin","refs/heads/main"],text=True).strip(),imageVersion=os.getenv("ImageVersion"),imageOS=os.getenv("ImageOS"),settings={k:os.getenv(k) for k in ["DOTNET_TieredCompilation","DOTNET_TieredPGO","DOTNET_ReadyToRun"]})
    manifest.update(fixtureIntervention="20-second logger primer, 20-second exact helper primer, and 50 BDN workload warmups", plan=".github/benchmarks/pool-primer/HELPER-PLAN.md")
    (OUT/"manifest.json").write_text(json.dumps(manifest,indent=2))
    run(["dotnet","tool","install","--tool-path",OUT/"trace-tool","dotnet-trace","--version","10.0.731102"],OUT/"trace-install.log")
    run(["dotnet","build",FIXTURE/"Inspector.csproj","-c","Release","--disable-build-servers","-p:UseSharedCompilation=false","-o",OUT/"inspector"],OUT/"inspector-build.log")
    prepare_primer()
    run(["dotnet","build-server","shutdown"],OUT/"build-server-shutdown.log")
    execute("smoke",True,True)
    runtime = OUT / "runtime"
    runtime.mkdir()
    core = Path(json.loads((OUT / "smoke/bdn/helper-bindings.json").read_text())[0]["Location"])
    shutil.copy2(core, runtime / core.name)
    (OUT / "runtime-bindings.json").write_text(json.dumps(dict(location=str(core), sha256=hashlib.sha256(core.read_bytes()).hexdigest()), indent=2))
    run(["git","archive","--format=zip","--output="+str(OUT/"harness-source.zip"),"HEAD",".github/benchmarks/pool-jit",".github/benchmarks/pool-primer",".github/scripts/pool_jit_trace.py",".github/workflows/benchmarks.yml","global.json","Directory.Build.props","Directory.Packages.props"],OUT/"harness-archive.log")
elif sys.argv[1] in ["U1","T","U2"]:execute(sys.argv[1],sys.argv[1]=="T")
else:raise ValueError(sys.argv[1])
