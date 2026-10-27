"""Freeze next local candidate with new cache provenance and explicit source gaps."""
import pathlib,json,hashlib,copy,collections,datetime,sys
def source_locator(s):return tuple(s[k] for k in ('repository','revision','path'))
def scoped_path(prefix,path):
    path=pathlib.PurePosixPath(path)
    if path.is_absolute() or '..' in path.parts:raise ValueError('Path escapes task root')
    return str(pathlib.PurePosixPath(prefix)/path)
def relocate(source,prefix):
    s=copy.deepcopy(source)
    if s.get('local_path'):s['local_path']=scoped_path(prefix,s['local_path'])
    if s.get('metadata_cache'):s['metadata_cache']['path']=scoped_path(prefix,s['metadata_cache']['path'])
    if s.get('transport',{}).get('archive_local_path'):s['transport']['archive_local_path']=scoped_path(prefix,s['transport']['archive_local_path'])
    if s.get('transport',{}).get('metadata_receipt'):s['transport']['metadata_receipt']=scoped_path(prefix,s['transport']['metadata_receipt'])
    return s
def normalize_api(source):
    s=copy.deepcopy(source);platform=str(s.get('platform_api_version',s['api_level']))
    s.update(platform_api_version=platform,api_level=int(platform.split('.')[0]),channel='stable');return s
def replace_index_cache(source,proof):
    if not proof['payload_identical_to_frozen_generation'] or not proof['byte_reproducibility_verified'] or source['sha256']!=proof['cache']['source_sha256'] or source['platform_api_version']!=proof['platform_api']:raise ValueError('Deterministic cache lacks matching source/platform/payload proof')
    result=copy.deepcopy(source);result['metadata_cache']=copy.deepcopy(proof['cache']);result['metadata_cache']['serialization']='jsonl-gzip-mtime0-v1';return result
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def run(root,version,outside_inventory,deterministic_index=False):
    root=pathlib.Path(root).resolve();cache_root=root/'cache-build-round005';out=root/('source-inventory-release'+version+'.json')
    if out.exists():raise ValueError('Preserve existing immutable inventory')
    old=json.loads((root/'source-inventory-release005.json').read_text());fresh=json.loads((cache_root/'sdk37.2/source-inventory.json').read_text());report=json.loads((cache_root/'sdk37.2/java-extraction-report.json').read_text());index=json.loads((cache_root/'sdk37.2/metadata-source-inventory.json').read_text())
    if fresh.get('parse_status') or report['java_files']!=16893 or report['parse_error_regions']:raise ValueError('New complete Java cache generation required')
    sources=[s for s in old['sources'] if not s['repository'].startswith('google-sdk-')]
    sdk=[normalize_api(relocate(s,'cache-build-round005')) for s in fresh['sources']+index['sources']]
    proof_path=root/'sdk-index-cache-deterministic/attempt002/reproducibility-proof.json'
    if deterministic_index:
        proof=json.loads(proof_path.read_text());sdk=[replace_index_cache(s,proof) if s['kind']=='sdk-api-index' else s for s in sdk]
    stats=copy.deepcopy(report['source_reports'])
    for s in stats:s['cache_path']=scoped_path('cache-build-round005',s['cache_path'])
    sys.path.insert(0,str(cache_root/'catalog/tooling'));from java_context import attach
    attach(sdk,stats);sources+=sdk;outside=root/outside_inventory;sources+=json.loads(outside.read_text())['sources']
    locators=set()
    for s in sources:
        locator=source_locator(s)
        if locator in locators:raise ValueError('Duplicate source locator '+str(locator))
        locators.add(locator)
        if s['status']!='downloaded':continue
        if digest(root/s['local_path'])!=s['sha256']:raise ValueError('Source hash mismatch '+s['path'])
        if s.get('metadata_cache'):
            cache=s['metadata_cache']
            if digest(root/cache['path'])!=cache['sha256']:raise ValueError('Cache hash mismatch '+s['path'])
            cache['source_sha256']=s['sha256']
    old['catalog']['registry_version']=int(version);old['sources']=sources;old['frozen_at']=datetime.datetime.now(datetime.timezone.utc).isoformat();old['complete']=False
    old['coverage_summary']='PARTIAL: pinned AOSP17r1 original corpus plus explicitly enumerated additional repositories and complete SDK37.2 source-data/index extraction; local-only checkpoint preserves unavailable raw paths and global semantic gaps. No OS/client authority inferred.'
    old['catalog']['coverage']=old['coverage_summary'];old['cache_generation']='round005';old['cache_evidence_root']='cache-build-round005';old['inventories']=[dict(path=p,sha256=digest(root/p)) for p in ('source-inventory-release005.json',str(outside.relative_to(root)),'cache-build-round005/sdk37.2/source-inventory.json','cache-build-round005/sdk37.2/metadata-source-inventory.json','cache-build-round005/input-code-manifest.json')]
    if deterministic_index:old['inventories'].append(dict(path=str(proof_path.relative_to(root)),sha256=digest(proof_path)));old['index_cache_serialization']='jsonl-gzip-mtime0-v1'
    out.write_text(json.dumps(old,indent=2));print(json.dumps(dict(path=str(out),sha256=digest(out),sources=len(sources),by_status=dict(collections.Counter(s['status'] for s in sources))),indent=2));return old
if __name__=='__main__':run(pathlib.Path(__file__).resolve().parent,sys.argv[1],sys.argv[2],'--deterministic-sdk-index' in sys.argv)
