"""Deterministic next-generation cache; preserves all frozen earlier caches."""
import pathlib,json,gzip,hashlib,sys,copy,datetime
from restore_java_caches import write_cache
R=pathlib.Path(__file__).resolve().parent;D=R/(sys.argv[1] if len(sys.argv)>1 else 'sdk-index-cache-deterministic');D.mkdir(parents=True)
P=R/'cache-build-round005/catalog/tooling';sys.path.insert(0,str(P));import sdk_api_index
inv=json.loads((R/'cache-build-round005/sdk37.2/metadata-source-inventory.json').read_text());source=next(s for s in inv['sources'] if s['kind']=='sdk-api-index');platform=str(source.get('platform_api_version',source['api_level']));raw=(R/'cache-build-round005'/source['local_path']).read_bytes()
parsed=sdk_api_index.parse(raw,target=platform);records=parsed['records'];a=D/'first'/'api-index-current.jsonl.gz';b=D/'second'/'api-index-current.jsonl.gz'
sha=write_cache(a,records);other=write_cache(b,records)
if sha!=other:raise ValueError('Deterministic cache hashes differ')
old=R/'cache-build-round005'/source['metadata_cache']['path'];compared=0
with gzip.open(old,'rt') as stream:
 for compared,(row,line) in enumerate(zip(records,stream,strict=True),1):
  if row!=json.loads(line):raise ValueError('Index semantic payload changed')
cache=dict(path=str(a.relative_to(R)),sha256=sha,parser_sha256=hashlib.sha256((P/'sdk_api_index.py').read_bytes()).hexdigest(),records=len(records),source_sha256=source['sha256'])
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),platform_api=platform,record_count=compared,payload_identical_to_frozen_generation=True,byte_reproducibility_verified=True,gzip_mtime=0,cache=cache,historical_cache_sha256=source['metadata_cache']['sha256'],earlier_snapshots_modified=False,usage='Next inventory only; retain original cache provenance in release006')
(D/'reproducibility-proof.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
