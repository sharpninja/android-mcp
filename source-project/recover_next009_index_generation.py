"""Preserve inherited current-only caches; generate a distinct all-declared cache."""
import pathlib,json,shutil,hashlib,datetime,sys
R=pathlib.Path(__file__).resolve().parent;D=R/'cache-build-next009';B=D/'before-all-declared-cache';C=R/'catalog-next009'
if B.exists():raise SystemExit('Inspect previous recovery before retrying')
B.mkdir()
for name in ('api-index-current.jsonl.gz','api-index-report.json','metadata-source-inventory.json'):
 p=D/'sdk37.2'/name
 if p.exists():p.rename(B/name)
for name in ('input-code-manifest.json','java-cache-reuse-proof.json','sdk-index.log'):
 p=D/name
 if p.exists():p.rename(B/name)
for name in ('tooling/sdk_api_index.py','tooling/build_catalog.py','tests/test_sdk_history_annotation_guard.py'):
 p=D/'catalog'/name;target=B/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target);shutil.copy2(C/name,p)
p=D/'stage_sdk372_index.py';shutil.copy2(p,B/'stage_sdk372_index.py');code=p.read_text();code=code.replace("out=D/'api-index-current.jsonl.gz'","out=D/'api-index-all-declared.jsonl.gz'");needle=" if kind=='sdk-api-index':\n"
if code.count(needle)!=1:raise ValueError('Unexpected stage source layout')
code=code.replace(needle,needle+"  s['sdk_api_index_view']='all-declared'\n");p.write_text(code)
sys.path.insert(0,str(D/'catalog/tooling'));from cache_fingerprint import current
old=json.loads((B/'java-cache-reuse-proof.json').read_text());java_fp=current('java-source')
if java_fp!=old['java_fingerprint']:raise ValueError('Java producer changed; regeneration needed')
proof=dict(old,at=datetime.datetime.now(datetime.timezone.utc).isoformat(),index_fingerprint=current('sdk-api-index'),recovery='Inherited copied target-current metadata archived before separate all-declared output; failed first attempt retained',fresh_java_extraction_claimed=False)
(D/'java-cache-reuse-proof.json').write_text(json.dumps(proof,indent=2));manifest=[]
for p in sorted(D.rglob('*')):
 if not p.is_file() or '__pycache__' in p.parts or 'snapshots' in p.parts or 'sdk37.2' in p.parts or 'before-all-declared-cache' in p.parts:continue
 manifest.append(dict(path=str(p.relative_to(D)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'input-code-manifest.json').write_text(json.dumps(dict(at=proof['at'],cache_contract=2,source_handoff='preserve-sdk-history source v2, explicit cache inventory surface',files=manifest),indent=2));print(json.dumps(proof,indent=2))
