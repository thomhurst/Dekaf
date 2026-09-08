import json, subprocess, zipfile
from pathlib import Path
root=Path.cwd(); work=root/'.artifacts/retained-codegen'
pins={'A':'a48fafe4121350da7ad83fcdd238f0f8039d6d59','B':'677b93bc1aac9d70f474ada85117d99aa952734a'}
evidence='472f37f9dfdc349f580cd53154802915a42226e8'
for label,sha in pins.items():
    dest=work/('product-'+label); dest.mkdir(exist_ok=False)
    names=subprocess.check_output(['git','ls-tree','--name-only',sha],text=True).splitlines()
    include=['src']+[p for p in names if p.startswith('Directory.') or p in ('global.json','NuGet.Config','nuget.config','.editorconfig','README.md')]
    z=work/('source-'+label+'.zip')
    subprocess.run(['git','archive','--format=zip',f'--output={z}',sha,*include],check=True)
    with zipfile.ZipFile(z) as f: f.extractall(dest)
    fixture=dest/'diagnostic'; fixture.mkdir()
    prefix=f'docs/performance-evidence/ubuntu-aba-2026-09-07/pr-3116/raw/fixture-{label}/'
    content=subprocess.check_output(['git','show',evidence+':'+prefix+'ShareConsumerParsingBenchmarks.cs'])
    (fixture/'ShareConsumerParsingBenchmarks.cs').write_bytes(content)
    project=f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AssemblyName>Dekaf.Benchmarks</AssemblyName><DefineConstants>$(DefineConstants);ABA_{'BASELINE' if label=='A' else 'CANDIDATE'}</DefineConstants><AllowUnsafeBlocks>true</AllowUnsafeBlocks><TreatWarningsAsErrors>false</TreatWarningsAsErrors><RunAnalyzers>false</RunAnalyzers><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><PackageReference Include="BenchmarkDotNet"/><ProjectReference Include="../src/Dekaf/Dekaf.csproj"/></ItemGroup></Project>'''
    (fixture/'Probe.csproj').write_text(project,encoding='utf-8')
    (fixture/'Program.cs').write_bytes((root/'tools/ShareRetainedDiagnosis/Program.cs').read_bytes())
(work/'pins.json').write_text(json.dumps(pins,indent=2))
