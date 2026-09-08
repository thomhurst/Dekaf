import hashlib, json, subprocess
from pathlib import Path
work=Path('.artifacts/retained-codegen')
proof={}
for name in ['src/Dekaf/ShareConsumer/ShareConsumeResult.cs','diagnostic/ShareConsumerParsingBenchmarks.cs']:
    data=[(work/('product-'+p)/name).read_bytes() for p in ('A','B')]
    proof[name]=dict(source_equal=data[0]==data[1],sha256=[hashlib.sha256(d).hexdigest() for d in data])
method='private List<ShareConsumeResult<TKey, TValue>> ParsePartitionRecords('
texts=[]
for p in ('A','B'):
    text=(work/('product-'+p)/'src/Dekaf/ShareConsumer/KafkaShareConsumer.cs').read_text()
    pos=text.index(method); start=text.index('{',pos); depth=1; end=start+1
    while depth:
        if text[end]=='{': depth+=1
        elif text[end]=='}': depth-=1
        end+=1
    text=text[pos:end]; texts.append(text)
    (work/f'ParsePartitionRecords-{p}.txt').write_text(text,encoding='utf-8')
proof['ParsePartitionRecords']=dict(source_equal=texts[0]==texts[1],sha256=[hashlib.sha256(t.encode()).hexdigest() for t in texts])
binary=[]
for p in ('A','B'):
    original=json.loads(subprocess.check_output(['git','show',f'472f37f9dfdc349f580cd53154802915a42226e8:docs/performance-evidence/ubuntu-aba-2026-09-07/pr-3116/raw/binaries-{p}.json']))
    (work/f'hosted-binary-hashes-{p}.json').write_text(json.dumps(original,indent=2))
    for name in ('Dekaf.dll','Dekaf.Abstractions.dll','Dekaf.Benchmarks.dll'):
        path=work/('product-'+p)/'diagnostic/bin/Release/net10.0'/name
        h=hashlib.sha256(path.read_bytes()).hexdigest()
        binary.append(dict(phase=p,name=name,rebuilt_sha256=h,original_hosted_sha256=original[name],matches_original=h==original[name]))
proof['binaries']=binary
(work/'source-and-binary-proof.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print(json.dumps(proof,indent=2))
