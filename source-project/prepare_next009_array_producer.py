"""Freeze reviewed array-boundary producer; do not relabel old Java caches."""
import pathlib,shutil,json,datetime,hashlib,sys,importlib.metadata,pyexpat
R=pathlib.Path(__file__).resolve().parent;D=R/'cache-build-next009';C=R/'catalog-next009v2';B=D/'before-array-producer'
if B.exists():raise SystemExit('Preserve prior array producer preparation')
B.mkdir();(D/'catalog').rename(B/'catalog')
shutil.copytree(C,D/'catalog',ignore=shutil.ignore_patterns('__pycache__','output'))
for name in ('input-code-manifest.json','java-cache-reuse-proof.json'):
 if (D/name).exists():(D/name).rename(B/name)
for name in ('source-inventory.json','java-extraction-report.json','java-stage-progress.json'):
 if (D/'sdk37.2'/name).exists():(D/'sdk37.2'/name).rename(B/name)
sys.path.insert(0,str(D/'catalog/tooling'));from cache_fingerprint import current
java_fp=current('java-source');index_fp=current('sdk-api-index')
if java_fp!='f8c7323d5733ff12256e200046e032926ffbc8561e8c8e8eb59e089965524e7c':raise ValueError('Unexpected reviewed Java producer')
if index_fp!='13c14950fa2bc2313bffe702497818c0809dcb58ca9ea32e05b1338e554cf9e9':raise ValueError('SDK-index producer changed; regenerate it too')
proof=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),java_fingerprint=java_fp,index_fingerprint=index_fp,new_java_cache_directory='sdk37.2/java-cache-v5',action='Full fresh Java extraction required by changed parsers.py fingerprint; old v4 caches retained',previous_reuse_proof='before-array-producer/java-cache-reuse-proof.json',sdk_index_regeneration_needed=False,dependency_changes=False,python=list(sys.version_info[:3]),tree_sitter=importlib.metadata.version('tree-sitter'),tree_sitter_java=importlib.metadata.version('tree-sitter-java'),expat=pyexpat.EXPAT_VERSION)
(D/'preflight-array.json').write_text(json.dumps(proof,indent=2));files=[]
for p in sorted(D.rglob('*')):
 if not p.is_file() or '__pycache__' in p.parts or any(part in p.parts for part in ('snapshots','sdk37.2','before-array-producer','before-all-declared-cache')):continue
 files.append(dict(path=str(p.relative_to(D)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'input-code-manifest.json').write_text(json.dumps(dict(at=proof['at'],cache_contract=2,source_handoff='round009 array boundary plus SDK history and startup contract',files=files),indent=2));print(json.dumps(proof,indent=2))
