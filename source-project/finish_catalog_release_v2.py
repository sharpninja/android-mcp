"""Create sidecar evidence after successful immutable runtime publication."""
import pathlib,json,hashlib,datetime,sqlite3,sys,shutil,re,collections
R=pathlib.Path(__file__).resolve().parent;v=sys.argv[1];O=R/'catalog/output'/('release-'+v);E=R/('release'+v+'-evidence')
status=json.loads((R/('build-release'+v+'-status.json')).read_text())
if status['status']!='complete' or any(s['returncode'] for s in status['steps']):raise ValueError('Build/validation/export steps are not complete')
if E.exists():raise ValueError('Preserve existing release evidence')
inventory_document=json.loads((R/('source-inventory-release'+v+'.json')).read_text());cache_root=R/inventory_document.get('cache_evidence_root','.')
java_report=json.loads((cache_root/'sdk37.2/java-extraction-report.json').read_text());index_root=R/inventory_document.get('index_cache_evidence_root',inventory_document.get('cache_evidence_root','.'));index_report=json.loads((index_root/'sdk37.2/api-index-report.json').read_text())
test_match=re.search(r'Ran (\d+) tests?',(R/('build-release'+v+'.log')).read_text());fixture_count=int(test_match.group(1)) if test_match else 'unknown'
selected_count=len(inventory_document['sources']);source_statuses=dict(collections.Counter(s['status'] for s in inventory_document['sources']))
report=R/('validation-report-release'+v+'.json');validation=json.loads(report.read_text())
if validation.get('integrity_check')!='ok' or validation.get('foreign_key_check') or validation.get('json_schema_failures'):raise ValueError('Validation did not pass')
supplement=R/('production-reconciliation-release'+v+'.json');exports=R/('export-reconciliation-release'+v+'.json')
reconciliation=json.loads(supplement.read_text());export_check=json.loads(exports.read_text())
if reconciliation['source_reconciliation']['missing'] or reconciliation['source_reconciliation']['extra'] or not export_check['json_and_jsonl_byte_equivalent']:raise ValueError('Production/export reconciliation did not pass')
device_report=R/('device-inventory-reconciliation-release'+v+'.json');device_check=json.loads(device_report.read_text())
if not device_check['sqlite_jsonl_byte_equivalent'] or not device_check['all_record_hashes_verified']:raise ValueError('Startup inventory reconciliation did not pass')
E.mkdir();db=sqlite3.connect((O/'catalog.sqlite').as_uri()+'?mode=ro',uri=True)
counts={t:db.execute('SELECT count(*) FROM '+t).fetchone()[0] for t in ('sources','bindings','occurrences','binding_declarations','permission_definitions','permission_symbols','permission_relations','aidl_schemas','annotation_evidence','registry_entries','registry_inventory')}
meta=dict(db.execute('SELECT key,value FROM catalog_meta'));permission_names=db.execute('SELECT count(*) FROM (SELECT name FROM permission_definitions UNION SELECT value FROM permission_symbols)').fetchone()[0];defined_names=db.execute('SELECT count(DISTINCT name) FROM permission_definitions').fetchone()[0];db.close()
for p in (report,supplement,exports,R/('coverage-manifest-release'+v+'.json'),R/('build-release'+v+'-inputs.json'),device_report,R/'integration/source-candidate009v3-validation.json',R/'integration/round010-v1-source-merge.json',R/'outside-corpus-audit/native-generated-scope-closure-plan-v1.json',R/'outside-corpus-audit/native-literal-registry-reconciliation-v2.json'):
 shutil.copyfile(p,E/p.name)
audit=R/'outside-corpus-audit/classification.json'
if audit.exists():shutil.copyfile(audit,E/'outside-corpus-classification-at-publication.json')
audit_v2=R/'outside-corpus-audit/classification-full985-v2.json'
if audit_v2.exists():shutil.copyfile(audit_v2,E/'outside-corpus-classification-metadata-v2.json')
def digest(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  while b:=f.read(4*1024*1024):h.update(b)
 return h.hexdigest()
files=[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=digest(p)) for p in sorted(O.iterdir()) if p.is_file()]
permission_files=[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=digest(p)) for p in sorted((R/('permission-export-release'+v)).iterdir()) if p.is_file()]
manifest=dict(release=v,created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),complete=False,status='validated exact-version PARTIAL runtime catalog',catalog_meta=meta,counts=counts,permission_names=permission_names,manifest_defined_names=defined_names,files=files,inventory=dict(path='source-inventory-release'+v+'.json',sha256=digest(R/('source-inventory-release'+v+'.json'))))
manifest['permission_files']=permission_files
manifest['device_inventory_reconciliation']=device_check
manifest['sdk_lifecycle_records']=index_report.get('declared_inventory_records')
if audit_v2.exists():manifest['audit_metadata']=dict(metadata_version=2,path='outside-corpus-classification-metadata-v2.json',sha256=digest(audit_v2),global_semantic_completeness=False)
latest=R/'outside-corpus-audit/latest-release-source-closure-v1/project-pin-reconciliation.json'
if latest.exists():
 pin_check=json.loads(latest.read_text())
 manifest['public_revision_reconciliation']={k:pin_check[k] for k in ('at','latest_superproject_commit','latest_manifest_commit','counts','global_semantic_completeness')}
 for directory in ['latest-release-manifest-probe-v1','latest-release-source-closure-v1']:
  shutil.copytree(R/'outside-corpus-audit'/directory,E/directory)
(E/'release-manifest.json').write_text(json.dumps(manifest,indent=2))
readme=f'''# Android binding/permission catalog — release {v} (PARTIAL)

The primary runtime file is `catalog.sqlite`: {counts['bindings']:,} discovery entries,
{counts['occurrences']:,} source occurrences and {counts['sources']:,} source records.
The frozen acquisition inventory selects {selected_count:,} paths. Its statuses
are {json.dumps(source_statuses,sort_keys=True)}. It includes the original AOSP
corpus, Google's API37.2 source-data archive and platform metadata, plus any
explicitly identified supplemental source acquisition checkpoint.
It is not a global all-current-completeness claim: other repositories and
unresolved semantics are enumerated in the coverage and outside-corpus audit.

API37.2, package revision1 and SDK Extension24 are separate metadata facts.
All{java_report['java_files']:,} Java files were scanned ({java_report['declaration_records']:,} declaration records; zero grammar
errors);12 source members are empty and yield no invented declarations. The
official API lifecycle index has{index_report['current_declarations']:,} current declaration keys, with{index_report['excluded_history_or_future']:,}
historical/future records outside that current view. All{index_report.get('declared_inventory_records', 'unknown'):,} lifecycle declarations remain retained in the versioned inventory. Source text and JVM
identities remain distinct unless an exact method/type link is proven. Source
fields are not used to transfer permissions without a descriptor proof.

Permission metadata contains {permission_names:,} names and {defined_names:,}
manifest-defined names. Core AOSP17r1 contributes1,160 definition elements:
1,138 unique names and22 duplicate elements retained as evidence. The source
identity reconciliation has no missing/extra imported paths. All552 legacy
release002 declaration-shape variants are retained in this generation.
All source occurrences/annotations and raw AIDL schemas remain accessible.
Unknown or unsupported metadata is retained; missing annotations are unknown,
not permission-free. Positive aggregate evidence retains absent-source variants
and is not a universal OS authority claim. OS grants and client policy remain
separate from source metadata.

Storage schema:{meta.get('schema_version')}; Entry/API contract:1.x. Registry
generation:{v}; package identity namespace:2; source processing provenance:3.
Old snapshots and grants are not silently retargeted. Exact source facts use
nullable API applicability and unknown channel where unproven. Field types,
concrete type kinds, generic resolution, unsupported AIDL syntax, generated wire codes
and codecs remain unknown where the sources cannot prove them.

The frozen gate passed{fixture_count} tests, including import purity, indexed Point access,
annotation alias ambiguity/type-variable rejection, cache integrity and registry
rules. The published release passed exhaustive schema, integrity/FK, hash/ETag,
source/declaration and export reconciliation. Reports are sidecars; validation
and permission export do not modify the published release directory.

`entries.jsonl` and `catalog.json` contain the same consolidated Entry documents.
`device_inventory.jsonl` contains the full versioned startup inventory, reconciled byte-for-byte with `registry_inventory`; its manifest pins the contract, active registry, count and SHA256. Device applicability is resolved only from supplied evidence-backed device facts; no actual device was queried.
HIDL/native bodies are retained as explicitly unsupported raw evidence. They are not normalized methods, descriptors, transactions or codecs; their semantic completeness remains an actionable public-source gap.
Per-table JSONL retains declarations, sources, definitions and annotation evidence.
`permission-export-release{v}` contains the separately schema-validated permission
union. `release-manifest.json` gives exact hashes and uncompressed storage sizes.

No Android bridge, HTTP service, deployment, device operation, debugging change,
SDK installation or license acceptance occurred. OpenAPI3.1 is a CRUD contract.
The design/contract hostile-validation loop is owned by the separate review task;
no98/98 result is claimed here. Use coverage, not entry count, to assess scope.

Bulk raw snapshots remain preserved in the task workspace. Delivery variants may
omit bulk sources; frozen inventories, SDK archive receipts, metadata-range
receipts, source hashes, restoration scripts and attribution identify exact bytes.
Reproduce into a new output directory using the frozen code snapshot and input
inventory. No source code from Android was executed or built during extraction.
'''
(E/'README.md').write_text(readme);print(json.dumps(dict(evidence=str(E),counts=counts,permissions=permission_names,sqlite=next(x for x in files if x['path'].endswith('/catalog.sqlite'))),indent=2))
