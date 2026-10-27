import pathlib,json,hashlib,zipfile,datetime,sqlite3
R=pathlib.Path(__file__).resolve().parent;C=R/'catalog';O=C/'output/release-002';D=R/'deliverables';D.mkdir(exist_ok=True)
validation=json.loads((O/'validation-report.json').read_text());assert not validation['json_schema_failures'] and validation['integrity_check']=='ok' and not validation['foreign_key_check']
milestone=dict(title='Android 17 SDK37.0 catalog PARTIAL validated milestone',created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),complete=False,entries=validation['entries_validated'],source_paths=validation['counts']['sources'],latest_official_sdk='37.2 revision 1; not included',sdk_snapshot='37.0 at android-17.0.0_r1',core_permission_manifest='Not included in this frozen release; subsequently recovered for a later release',limitations=['Only 10 manifest definition records in this release; 1172 permission names include constant-only evidence','16824 enumerated source paths absent in this release; later recovery continues','875 external annotation records have unresolved exact targets','Structured AIDL layouts/import resolution/generated wire codecs and transaction codes unknown','Frozen AIDL path scopes in this baseline may be classified test-or-example; use source paths as authoritative','OpenAPI CRUD contract only; no implemented service','No runtime OS authority or MCP client grants inferred','HV was launched against an earlier immutable snapshot and is not yet a completed review of this release'],raw_source_snapshots='Preserved locally on LAB-OMARCHY; excluded from this runtime milestone bundle. Source inventory has exact immutable URLs, hashes and original paths for supported retrieval.',original_library_inputs=json.loads((R/'input-library-identities.json').read_text()))
(D/'milestone.json').write_text(json.dumps(milestone,indent=2))
files=[(D/'milestone.json','milestone.json'),(R/'source-inventory.release002.json','source-inventory.json'),(R/'coverage-manifest.json','coverage-manifest.json'),(R/'project-coverage.json','project-coverage.json'),(R/'ATTRIBUTION.md','ATTRIBUTION.md'),(R/'input-library-identities.json','input-library-identities.json'),(R/'repository2-3.xml','metadata/repository2-3.xml')]
for p in C.rglob('*'):
 if p.is_file() and 'output' not in p.relative_to(C).parts and '__pycache__' not in p.parts:files.append((p,'catalog/'+str(p.relative_to(C))))
for name in ['catalog.sqlite','entries.jsonl','catalog.json','permissions.jsonl','permission-coverage.json','permission-summary-validation.json','permission-expression-samples.json','validation.json','validation-report.json']:
 files.append((O/name,'catalog/output/release-002/'+name))
for name in ['acquire_trees.py','acquire_corpus_trees.py','acquire_sources.py','acquire_sdk_only.py','finalize_inventory.py']:
 files.append((R/name,'acquisition/'+name))
readme='''# PARTIAL runtime milestone\n\nOpen milestone.json first. This frozen release has 135,508 validated entries from SDK37.0/AOSP17r1 source evidence. It does not claim current SDK37.2 coverage. Permission manifest coverage is limited to ten records; the later recovered core manifest is not included.\n\nPrimary database: catalog/output/release-002/catalog.sqlite. JSON and JSONL exports are adjacent. Versioned DDL, JSON Schemas, OpenAPI3.1 CRUD specification, extractor, tests and validation evidence are included. No bridge/HTTP service is implemented. Raw upstream snapshots remain preserved on LAB-OMARCHY and are deliberately excluded from this runtime milestone. Reproduction requires materializing exact source-inventory.json blobs at their listed local_path, using supported source transfer and honoring rate limits. The extractor itself is offline.\n\nThis milestone is useful progress, not task completion. Later source recovery and hostile validation continue separately.\n'''
zip_path=D/'android17-sdk37.0-catalog-PARTIAL-milestone-20261026.zip'
hashes=[]
with zipfile.ZipFile(zip_path,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as z:
 z.writestr('README.md',readme)
 for p,name in files:
  h=hashlib.sha256()
  with p.open('rb') as f:
   while chunk:=f.read(1024*1024):h.update(chunk)
  hashes.append(dict(path=name,sha256=h.hexdigest(),size_bytes=p.stat().st_size));z.write(p,name)
 z.writestr('SHA256-MANIFEST.json',json.dumps(hashes,indent=2))
result=dict(path=str(zip_path),size_bytes=zip_path.stat().st_size,sha256=hashlib.sha256(zip_path.read_bytes()).hexdigest(),files=len(files)+2);(D/'milestone-bundle.json').write_text(json.dumps(result,indent=2));print(json.dumps(result),flush=True)
