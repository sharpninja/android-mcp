"""Offline exact-byte cache reproduction smoke; does not claim exhaustive replay."""
import pathlib,json,shutil,sys,datetime,hashlib
R=pathlib.Path(__file__).resolve().parent;D=R/'reproducibility-smoke-release009'
if D.exists():raise FileExistsError('Preserve existing reproduction evidence')
D.mkdir();spec=json.loads((R/'source-inventory-release009.json').read_text());java=[s for s in spec['sources'] if s['kind']=='java-source'][:3]
for s in java:
 destination=D/s['local_path'];destination.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(R/s['local_path'],destination)
(D/'sample-inventory.json').write_text(json.dumps(dict(sources=java),indent=2))
from restore_java_caches_contract2 import run,write_cache
producer=R/'cache-build-integrated009/catalog/tooling';sample=run(D/'sample-inventory.json',producer)
index=next(s for s in spec['sources'] if s['kind']=='sdk-api-index');index_producer=R/'cache-index-integrated009v3/catalog/tooling'
# Fresh import process is unnecessary for SDK parser: it has not yet been imported.
sys.path.insert(0,str(index_producer));import sdk_api_index
raw=(R/index['local_path']).read_bytes();assert hashlib.sha256(raw).hexdigest()==index['sha256'];parsed=sdk_api_index.parse(raw,target='37.2');rows=parsed['records'];target=D/index['metadata_cache']['path'];write_cache(target,rows,index['metadata_cache']['sha256'])
result=dict(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),java=sample,sdk_lifecycle_records_regenerated=len(rows),sdk_cache_sha256=index['metadata_cache']['sha256'],sdk_cache_byte_reproducibility_verified=True,network=False,devices=False,limitations='Java replay is a deterministic three-file smoke sample; full Java generation separately scanned all16,893 files; no global source completeness inferred')
(D/'result.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
