"""Freeze the verified current SDK artifacts and pinned AOSP subset for import.
Does not change earlier inventories or published databases. No network activity.
"""
import pathlib,json,hashlib,collections,datetime,sys,re
R=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(R/'catalog/tooling'))
from java_context import attach
output=R/(sys.argv[1] if len(sys.argv)>1 else 'source-inventory-release003.json')
if output.exists():raise ValueError('Preserve existing frozen inventory; select a fresh release name')
base=json.loads((R/'source-inventory.json').read_text())
sdk=json.loads((R/'sdk37.2/source-inventory.json').read_text())
if sdk.get('parse_status'):raise ValueError('SDK source extraction not complete')
meta=json.loads((R/'sdk37.2/metadata-source-inventory.json').read_text())
sources=base['sources']+sdk['sources']+meta['sources']
attach(sources,json.loads((R/'sdk37.2/java-extraction-report.json').read_text())['source_reports'])
for source in sources:
 version=source.get('platform_api_version')
 if source['repository'].startswith('google-sdk-'):
  version=str(source.get('api_level','37.2'));source['api_level']=37;source['channel']='stable';source['platform_api_version']=version
 elif source['repository']=='platform/prebuilts/sdk' and source['path'].startswith('37.0/public/'):
  source.update(api_level=37,channel='stable',platform_api_version='37.0')
 else:
  source['api_level']=None;source['channel']='unknown';source['source_release_context']='Pinned AOSP android-17.0.0_r1 commit; per-API applicability and shipping channel not established'
 if source['kind']=='java-source' and source['size_bytes']==0:source['x-empty-source-member']=True
keys=set()
for s in sources:
 key=(s['repository'],s['revision'],s['path'])
 if key in keys:raise ValueError('Duplicate source locator '+str(key))
 keys.add(key)
 if s['status']=='downloaded':
  p=R/s['local_path'];data=p.read_bytes()
  assert hashlib.sha256(data).hexdigest()==s['sha256'],s['path']
  if s['kind'] in ('java-source','sdk-api-index'):
   cache=s.get('metadata_cache');assert cache,s['path']
   assert hashlib.sha256((R/cache['path']).read_bytes()).hexdigest()==cache['sha256'],s['path']
   cache['source_sha256']=s['sha256']
doc=dict(schema_version='2.1.0',frozen_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),baseline='android-17.0.0_r1',sdk_api_level='37.2',sdk_package_revision=1,sdk_extension_level=24,complete=False,coverage_summary='PARTIAL: full official SDK37.2 source archive and current API lifecycle index; complete pinned SDK37.0 public signatures retained as earlier evidence; bounded AOSP17r1 AIDL/manifest/API corpus with exact gaps. SDK source-spelled declarations and exact JVM identities are separate until equivalence is proven. Metadata alone grants no authority.',inventories=[dict(path=p,sha256=hashlib.sha256((R/p).read_bytes()).hexdigest()) for p in ('source-inventory.json','sdk37.2/source-inventory.json','sdk37.2/metadata-source-inventory.json')],sources=sources)
match=re.search(r'release(\d+)',output.name)
if not match:raise ValueError('Output filename must identify the registry generation')
doc['catalog']=dict(registry_version=int(match.group(1)),android_version='AOSP android-17.0.0_r1 with SDK API37.2 artifact evidence',api_level=37,coverage=doc['coverage_summary'],sdk_api_level='37.2',current_sdk_metadata_api_level='37.2',sdk_package_revision=1,sdk_extension_level=24,binding_identity_scheme=2,source_identity_scheme=3)
output.write_text(json.dumps(doc,indent=2))
print(json.dumps(dict(path=str(output),sha256=hashlib.sha256(output.read_bytes()).hexdigest(),sources=len(sources),statuses=dict(collections.Counter(s['status'] for s in sources)),downloaded_by_kind=dict(collections.Counter(s['kind'] for s in sources if s['status']=='downloaded'))),indent=2))
