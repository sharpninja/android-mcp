"""Prepare an isolated additive integration candidate; never touch source owner files."""
import pathlib,json,shutil,hashlib,difflib,datetime
R=pathlib.Path(__file__).resolve().parent
SOURCE=R.parent.parent/'task/android-catalog-20261026'
BASE=SOURCE/'catalog-integrated012-sourcev2'
D=R/'integration/source-contract-metadata-v1-work'
if D.exists():raise SystemExit('Staging exists; inspect instead of replacing')
D.mkdir();F=D/'files';F.mkdir();V=D/'verification-catalog'
sha=lambda b:hashlib.sha256(b).hexdigest()
files=['SOURCE-METADATA-CONTRACT.md']
files+=['contracts/'+name for name in ('SourceContractMetadata.schema.json','SourceContractMetadataSet.schema.json','CallbackDescriptor.schema.json','FunctionalGroup.schema.json','NativeAbiEvidence.schema.json','SourceInterfaceEvidence.schema.json')]
files+=['tooling/'+name for name in ('source_metadata_reference.py','source_metadata_store.py','callback_source_facts.py','functional_source_facts.py','interface_applicability_evidence.py')]
files+=['migrations/source_metadata_ddl.sql','tests/test_source_metadata_contract.py','tests/test_source_metadata_crud.py']
for name in files:
 p=F/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(R/'catalog'/name,p)
entry=json.loads((R/'catalog/contracts/Entry.schema.json').read_text())
api=json.loads((R/'catalog/contracts/openapi.json').read_text())
addition=dict(entry_defs={k:v for k,v in entry['$defs'].items() if k.startswith('Meta')},entry_extension=entry['$defs']['Entry']['properties']['extensions']['properties']['source_contract_metadata'],openapi_components={k:v for k,v in api['components']['schemas'].items() if k.startswith(('Meta','SourceMetadataRead'))},openapi_paths={k:v for k,v in api['paths'].items() if any(s in k for s in ('callback-descriptors','functional-groups','source-metadata'))})
(D/'schema-additions.json').write_text(json.dumps(addition,indent=2)+'\n')
apply='''"""Apply additive schemas to an isolated catalog; preserve unrelated fields."""
import pathlib,json,sys
D=pathlib.Path(__file__).resolve().parent;P=pathlib.Path(sys.argv[1]).resolve();a=json.loads((D/'schema-additions.json').read_text())
e=json.loads((P/'contracts/Entry.schema.json').read_text());e['$defs'].update(a['entry_defs']);e['$defs']['Entry']['properties'].setdefault('extensions',{'type':'object'}).setdefault('properties',{})['source_contract_metadata']=a['entry_extension'];(P/'contracts/Entry.schema.json').write_text(json.dumps(e,indent=2)+'\\n')
o=json.loads((P/'contracts/openapi.json').read_text());o['components']['schemas'].update(a['openapi_components']);o['components']['schemas']['Entry']['properties'].setdefault('extensions',{'type':'object'}).setdefault('properties',{})['source_contract_metadata']={'$ref':'#/components/schemas/MetaSet'};o['paths'].update(a['openapi_paths']);o['info']['version']='1.5.0';(P/'contracts/openapi.json').write_text(json.dumps(o,indent=2)+'\\n')
'''
(D/'apply_schema_additions.py').write_text(apply)
for p in BASE.rglob('*'):
 if p.is_file() and '__pycache__' not in p.parts and p.suffix in ('.py','.json','.sql','.md'):
  target=V/p.relative_to(BASE);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
for p in F.rglob('*'):
 if p.is_file():target=V/p.relative_to(F);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
import subprocess,sys
subprocess.run([sys.executable,str(D/'apply_schema_additions.py'),str(V)],check=True)
path=BASE/'tooling/build_catalog.py';before=path.read_text();after=before
anchor="'contracts/PermissionExpression.schema.json')}))"
extra="'contracts/PermissionExpression.schema.json','contracts/SourceContractMetadata.schema.json','tooling/source_metadata_reference.py','tooling/callback_source_facts.py','tooling/functional_source_facts.py')}))"
if after.count(anchor)!=1:raise ValueError('Fingerprint anchor changed; no patch applied')
after=after.replace(anchor,extra)
anchor="  doc['extensions']['occurrence_count']=len(rows);content=js(doc);digest=sha(content)"
replacement="""  doc['extensions']['occurrence_count']=len(rows)
  from source_metadata_reference import attach_occurrences
  immutable_metadata_rows=db.execute('SELECT source_id,document_json,record_sha256 FROM binding_declarations WHERE binding_id=? ORDER BY source_id,line',(bid,)).fetchall()
  # Source owner supplies a hash-bound evidence_lookup for joined interface/native
  # sidecars. Default retains callback/functional occurrence facts and unknowns;
  # it does not fabricate imported predicates, memberships or runtime authority.
  doc=attach_occurrences(doc,immutable_metadata_rows)
  content=js(doc);digest=sha(content)"""
if after.count(anchor)!=1:raise ValueError('Build hook anchor changed; no patch applied')
after=after.replace(anchor,replacement)
(V/'tooling/build_catalog.py').write_text(after)
(D/'build-hook.patch').write_text(''.join(difflib.unified_diff(before.splitlines(True),after.splitlines(True),fromfile='a/tooling/build_catalog.py',tofile='b/tooling/build_catalog.py')))
record=dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_candidate=str(BASE),source_builder_sha256=sha(before.encode()),patched_builder_sha256=sha(after.encode()),files={str(p.relative_to(F)):sha(p.read_bytes()) for p in F.rglob('*') if p.is_file()},source_files_modified=False,production_modified=False,canonical_key='Entry.extensions.source_contract_metadata',version='1.0.0',source_enrichment_join_pending=True)
(D/'preparation.json').write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps(dict(staging=str(D),verification=str(V),files=len(files),source_modified=False)))
