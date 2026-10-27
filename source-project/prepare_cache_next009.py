"""Reuse byte-verified unchanged Java output; regenerate only changed SDK index."""
import pathlib,shutil,json,hashlib,datetime,sys
R=pathlib.Path(__file__).resolve().parent;old=R/'cache-build-round008';D=R/'cache-build-next009'
if D.exists():raise SystemExit('Preserve existing producer generation')
if shutil.disk_usage(R).free<25*1024**3:raise SystemExit('Insufficient task disk headroom')
shutil.copytree(R/'catalog-next009',D/'catalog',ignore=shutil.ignore_patterns('__pycache__','output'))
shutil.copytree(old/'sdk37.2',D/'sdk37.2',ignore=shutil.ignore_patterns('java-trial-report.json','java-cache-trial'))
shutil.copytree(old/'snapshots',D/'snapshots')
for name in ('stage_sdk372_sources.py','cache_writer.py'):shutil.copy2(old/name,D/name)
stage=(old/'stage_sdk372_index.py').read_text()
needle="sdk_api_index.parse(b,target='37.2')"
if stage.count(needle)!=1:raise ValueError('Unexpected SDK index staging producer')
stage=stage.replace(needle,"sdk_api_index.parse(b,target='37.2',preserve_history=True)")
needle='result.update(current_declarations=len(rows),source_sha256=s[\'sha256\'])'
if stage.count(needle)!=1:raise ValueError('Unexpected index report layout')
stage=stage.replace(needle,"result.update(declared_inventory_records=len(rows),current_declarations=sum(r['extras']['active_at_target'] is True for r in rows),source_sha256=s['sha256'])")
(D/'stage_sdk372_index.py').write_text(stage)
sys.path.insert(0,str(D/'catalog/tooling'));from cache_fingerprint import current
java_fp=current('java-source');index_fp=current('sdk-api-index')
old_report=json.loads((old/'sdk37.2/java-extraction-report.json').read_text())
if java_fp!=old_report['parser_sha256']:raise ValueError('Java producer changed; full regeneration required')
files=0
for p in (D/'sdk37.2/java-cache-v4').glob('*'):
 if p.is_file():
  if p.read_bytes()!=(old/'sdk37.2/java-cache-v4'/p.name).read_bytes():raise ValueError('Cache copy differs')
  files+=1
proof=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),java_producer_unchanged=True,reused_from='cache-build-round008',java_fingerprint=java_fp,index_fingerprint=index_fp,cache_files_copied_and_byte_verified=files,java_files=old_report['java_files'],java_records=old_report['declaration_records'],fresh_java_extraction_claimed=False,index_action='Regenerate all declared lifecycle nodes with explicit active-at-target metadata',no_dependency_change=True)
(D/'java-cache-reuse-proof.json').write_text(json.dumps(proof,indent=2))
manifest=[]
for p in sorted(D.rglob('*')):
 if not p.is_file() or '__pycache__' in p.parts or 'snapshots' in p.parts or 'sdk37.2' in p.parts:continue
 manifest.append(dict(path=str(p.relative_to(D)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'input-code-manifest.json').write_text(json.dumps(dict(at=proof['at'],cache_contract=2,source_handoff='preserve-sdk-history-source-v1',files=manifest),indent=2));print(json.dumps(proof,indent=2))
