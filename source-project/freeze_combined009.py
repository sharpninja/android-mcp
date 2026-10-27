"""Freeze acquired, locator-reconciled corpora without inferring semantic completeness."""
import pathlib,json,copy,hashlib,datetime,collections,sys,re
from freeze_next_inventory_v2 import normalize_api,relocate,source_locator
RAW_SCOPES={'aosp-pinned-hidl-source-evidence','aosp-native-binder-candidate-evidence','aosp-native-binder-search-candidate-evidence','aosp-interface-version-evidence'}
def merge_sources(groups):
 by={}
 for origin,rows in groups:
  for source in rows:
   key=source_locator(source);s=copy.deepcopy(source);evidence=dict(inventory=origin,scope=s.get('scope'),kind=s['kind'])
   if key not in by:
    s.setdefault('selection_origins',[])
    if evidence not in s['selection_origins']:s['selection_origins'].append(evidence)
    by[key]=s;continue
   previous=by[key]
   if previous.get('sha256')!=s.get('sha256') or previous['status']!=s['status']:raise ValueError('Conflicting immutable source locator '+str(key))
   previous['selection_origins'].append(evidence)
   if previous['kind']=='evidence' and s.get('scope') in RAW_SCOPES:previous['scope']=s['scope']
   for name in ('linked_aidl_evidence','discovery_evidence','discovery_path'):
    if name in s:previous.setdefault(name,s[name])
 for source in by.values():
  if 'api_level' not in source:source['api_level']=None
  if not isinstance(source.get('channel'),str) or not source['channel']:
   source['channel']='unknown';source.setdefault('acquisition_normalizations',[]).append('Channel not established by acquired source inventory; unknown explicitly retained')
  level=source['api_level']
  if level is not None and (not isinstance(level,int) or isinstance(level,bool) or level<1):raise ValueError('Invalid source api_level '+str(source_locator(source)))
 return list(by.values())
def digest(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  while chunk:=f.read(4*1024*1024):h.update(chunk)
 return h.hexdigest()
def run(root,java_generation,index_generation,version='009'):
 if not re.fullmatch(r'\d{3}',version):raise ValueError('Three-digit release identifier required')
 root=pathlib.Path(root).resolve();target=root/('source-inventory-release'+version+'.json')
 if target.exists():raise FileExistsError('Preserve previous immutable inventory')
 old=json.loads((root/'source-inventory-release008.json').read_text());groups=[('release008-non-sdk',[s for s in old['sources'] if not s['repository'].startswith('google-sdk-')])]
 fresh=json.loads((root/java_generation/'sdk37.2/source-inventory.json').read_text());report=json.loads((root/java_generation/'sdk37.2/java-extraction-report.json').read_text())
 if fresh.get('parse_status') or report['java_files']!=16893 or report['parse_error_regions']:raise ValueError('Complete fresh Java generation required')
 sdk=[normalize_api(relocate(s,java_generation)) for s in fresh['sources']]
 index=json.loads((root/index_generation/'sdk37.2/metadata-source-inventory.json').read_text());sdk += [normalize_api(relocate(s,index_generation)) for s in index['sources']]
 stats=copy.deepcopy(report['source_reports'])
 for stat in stats:stat['cache_path']=str(pathlib.PurePosixPath(java_generation)/stat['cache_path'])
 sys.path.insert(0,str(root/java_generation/'catalog/tooling'));from java_context import attach
 attach(sdk,stats);groups.append(('fresh SDK37.2 producer',sdk))
 provenance=[]
 for name in ['source-inventory-batchhidl1087-acquired001.json','source-inventory-batchnative-manual-v2-acquired002.json','source-inventory-batchnative-search303-acquired002.json']:
  p=root/'outside-corpus-audit'/name;groups.append((str(p.relative_to(root)),json.loads(p.read_text())['sources']));provenance.append(dict(path=str(p.relative_to(root)),sha256=digest(p)))
 receipt=json.loads((root/'permission-reference-source/retrieval-receipt.json').read_text());raw=root/'permission-reference-source/Manifest.permission.html';data=raw.read_bytes();assert digest(raw)==receipt['sha256']
 oid=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest();local=root/'snapshots/blobs'/oid
 if not local.exists():local.write_bytes(data)
 doc=dict(repository='google-android-reference',revision='http-snapshot:sha256-'+receipt['sha256'],path='reference/android/Manifest.permission.html',git_blob=oid,git_object_origin='computed local address, not upstream Git object',kind='evidence',scope='official-sdk-permission-reference',api_level=None,channel='unknown',license='Google Android reference content; original notices retained; no license agreement accepted',status='downloaded',url=receipt['url'],local_path=str(local.relative_to(root)),sha256=receipt['sha256'],size_bytes=len(data),retrieved_at=receipt['retrieved_at'],transport=dict(kind='bundled-http-snapshot',immutable_url=False,required_local_snapshot=str(raw.relative_to(root)),retrieval_receipt='permission-reference-source/retrieval-receipt.json'),source_surface='Unversioned permission documentation snapshot; not compiled manifest definitions or OS authority')
 groups.append(('official permission documentation snapshot',[doc]));sources=merge_sources(groups)
 for i,s in enumerate(sources,1):
  if s['status']!='downloaded':raise ValueError('Selected source not acquired '+s['path'])
  if digest(root/s['local_path'])!=s['sha256']:raise ValueError('Source digest mismatch '+s['path'])
  if s.get('metadata_cache'):
   c=s['metadata_cache'];assert digest(root/c['path'])==c['sha256'];c['source_sha256']=s['sha256']
  if i%5000==0:print('source admission hashes',i,'/',len(sources),flush=True)
 old.update(sources=sources,frozen_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),complete=False,cache_generation=java_generation,cache_evidence_root=java_generation,index_cache_evidence_root=index_generation,stage_source_driver='stage_sdk372_sources_contract2.py',inventories=provenance)
 old['catalog']['registry_version']=int(version);old['coverage_summary']='PARTIAL: full declared selected sources acquired; SDK37.2 lifecycle inventory retained; HIDL/native bodies retained as unsupported evidence; native/global semantic coverage remains unresolved. Metadata grants no runtime authority.';old['catalog']['coverage']=old['coverage_summary']
 target.write_text(json.dumps(old,indent=2));summary=dict(created_at=old['frozen_at'],sources=len(sources),by_kind=dict(collections.Counter(s['kind'] for s in sources)),by_status=dict(collections.Counter(s['status'] for s in sources)),source_inventory_sha256=digest(target),selected_download_complete=True,semantic_complete=False)
 (root/('coverage-manifest-release'+version+'.json')).write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
if __name__=='__main__':run(pathlib.Path(__file__).resolve().parent,*sys.argv[1:])
