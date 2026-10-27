import pathlib,json,sys,gzip,collections,datetime,hashlib
R=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(R/'catalog-integrated012-sourcev2/tooling'))
from scoped_permission_alias import Scope,resolve
from parsers import permission
sources=json.loads((R/'source-inventory-release011.json').read_text())['sources']
target=next(s for s in sources if s['kind']=='java-source' and s['path']=='src/android/app/admin/DevicePolicyManager.java')
manifest=next((s for s in sources if s['kind']=='java-source' and s['path']=='src/android/Manifest.java'),None)
if manifest is None:
 report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),status='same-snapshot constant witness unavailable; no production AST changed',source={k:target[k] for k in ('repository','revision','path','sha256')},reason='Google SDK sources omit generated android/Manifest.java. Existing constants from other AOSP/API or documentation snapshots were not silently relabeled as the same SDK snapshot.',tests=7,helper_state='fixture-only, not integrated',cache_producer_change=False,active011_unchanged=True)
 (R/'integration/scoped-permission-alias-device-policy-v1.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2));raise SystemExit(0)
if any(target[k]!=manifest[k] for k in ('repository','revision','platform_api_version')):raise ValueError('Not the same source snapshot')
def records(source):
 with gzip.open(R/source['metadata_cache']['path'],'rt') as stream:return [json.loads(line) for line in stream]
symbols={}
for r in records(manifest):
 if r['kind']!='field':continue
 raw=r['extras'].get('constant_raw')
 if not isinstance(raw,str):continue
 try:value=json.loads(raw)
 except ValueError:continue
 if isinstance(value,str) and value.startswith('android.permission.'):symbols[r['owner']+'.'+r['name']]=value
rows=records(target);payload=(R/target['local_path']).read_bytes();scope=Scope(payload,rows);changes=[]
for r in rows:
 aliases=scope.aliases(r.get('owner',''),symbols);before=permission(r['annotations']);after=resolve(before,aliases)
 if before!=after:changes.append(dict(canonical_signature=r['canonical'],line=r['line'],before=before,after=after,aliases=aliases))
report=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source={k:target[k] for k in ('repository','revision','path','sha256')},constant_source={k:manifest[k] for k in ('repository','revision','path','sha256')},same_source_snapshot=True,known_permission_constants=len(symbols),changed_declaration_permission_asts=len(changes),changes=changes,tests=7,producer_sha256=hashlib.sha256((R/'scoped_permission_alias.py').read_bytes()).hexdigest(),cache_producer_change=False,active011_unchanged=True,full_corpus_measurement_pending=True)
(R/'integration/scoped-permission-alias-device-policy-v1.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='changes'},indent=2))
