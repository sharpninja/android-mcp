"""Freeze the reviewed round008 producer and stage a fresh Java generation."""
import pathlib,shutil,json,hashlib,datetime,sys,importlib.metadata,pyexpat
R=pathlib.Path(__file__).resolve().parent
D=R/'cache-build-round008'
if D.exists():raise SystemExit('Preserve existing generation; inspect before resuming')
free=shutil.disk_usage(R).free
if free<25_000_000_000:raise SystemExit('Insufficient task disk headroom')
shutil.copytree(R/'catalog-round008',D/'catalog',ignore=shutil.ignore_patterns('__pycache__','output'))
for name,source in [('stage_sdk372_sources.py','stage_sdk372_sources_contract2.py'),('stage_sdk372_index.py','cache-build-round007/stage_sdk372_index.py'),('cache_writer.py','cache-build-round007/cache_writer.py')]:
 shutil.copy2(R/source,D/name)
sdk=D/'sdk37.2';sdk.mkdir();(D/'snapshots/blobs').mkdir(parents=True)
for name in ['source-37.2_r01.zip','source-archive-receipt.json']:
 shutil.copy2(R/'sdk37.2'/name,sdk/name)
shutil.copytree(R/'sdk37.2/platform-metadata',sdk/'platform-metadata')
sys.path.insert(0,str(D/'catalog/tooling'))
import cache_fingerprint,java_source
jfp=cache_fingerprint.current('java-source');ifp=cache_fingerprint.current('sdk-api-index')
assert jfp=='a75ac91a0098e484029faae8a8c7b39e4bbff36a26f752f30935ae4fda86caf9'
assert ifp=='944d0c09f8673597fc3d043c4a0f88b67477d10931a885eb29ca590e57f60c2d'
sample='\n'*300+'package example; import java.lang.reflect.Method; public interface Sample { interface Inner {} Method value(); }'
rows=java_source.parse(sample,'Sample.java')['records']
assert len(rows)==4 and all(r['line']>256 for r in rows)
assert next(r for r in rows if r['kind']=='method')['return_type']=='java.lang.reflect.Method'
assert next(r for r in rows if r['name']=='Inner')['extras']['effective_visibility']=='public'
receipt=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),status='passed',records=len(rows),java_cache_fingerprint=jfp,index_cache_fingerprint=ifp,python=list(sys.version_info[:3]),tree_sitter=importlib.metadata.version('tree-sitter'),tree_sitter_java=importlib.metadata.version('tree-sitter-java'),expat=pyexpat.EXPAT_VERSION,disk_free_before=free)
(D/'preflight.json').write_text(json.dumps(receipt,indent=2))
items=[]
for p in sorted(D.rglob('*')):
 if not p.is_file() or '__pycache__' in p.parts or 'snapshots' in p.parts or 'sdk37.2' in p.parts:continue
 items.append(dict(path=str(p.relative_to(D)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'input-code-manifest.json').write_text(json.dumps(dict(at=receipt['at'],cache_contract=2,source_handoff='round008-source-actions.json',files=items),indent=2))
print(json.dumps(receipt,indent=2))
