"""Freeze a focused post-v1 contract handoff, preserving source owner's active files."""
from pathlib import Path
import datetime,json,hashlib,difflib,zipfile,shutil
R=Path(__file__).resolve().parent;C=R/'catalog';OLD=R/'integration/source-contract-metadata-v1';OUT=R/'integration/source-contract-metadata-v2'
if OUT.exists():raise SystemExit('Immutable v2 exists; inspect instead')
OUT.mkdir();now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
rows=[];patch=[]
for before in sorted((OLD/'files').rglob('*')):
    if not before.is_file():continue
    rel=before.relative_to(OLD/'files');current=C/rel
    if not current.is_file():raise ValueError('Missing current file '+str(rel))
    if sha(before)==sha(current):continue
    dest=OUT/'files'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(current,dest)
    rows.append(dict(file=str(rel),expected_v1_sha256=sha(before),candidate_sha256=sha(current),bytes=current.stat().st_size))
    patch.extend(difflib.unified_diff(before.read_text().splitlines(True),current.read_text().splitlines(True),fromfile='a/'+str(rel),tofile='b/'+str(rel)))
for rel in ('API-CONTRACT.md','contracts/openapi.json'):
    # No source-owned current file is overwritten; metadata merge additions are provided below.
    current=C/rel;dest=OUT/'context'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(current,dest)
(OUT/'post-v1.patch').write_text(''.join(patch))
entry=json.loads((C/'contracts/Entry.schema.json').read_text());api=json.loads((C/'contracts/openapi.json').read_text())
oldadd=json.loads((OLD/'schema-additions.json').read_text())
addition=dict(entry_defs={k:v for k,v in entry['$defs'].items() if k in oldadd['entry_defs']},entry_extension=oldadd['entry_extension'],openapi_components={k:v for k,v in api['components']['schemas'].items() if k in oldadd['openapi_components']},openapi_paths={k:v for k,v in api['paths'].items() if k in oldadd['openapi_paths']},openapi_info_version='1.6.0')
(OUT/'schema-additions.json').write_text(json.dumps(addition,indent=2)+'\n')
shutil.copyfile(OLD/'apply_schema_additions.py',OUT/'apply_schema_additions.py')
for name in ('build_source_metadata_contracts.py','update_source_metadata_api.py'):
    shutil.copyfile(R/name,OUT/name)
for name in ('tests-regression-090.receipt.json','tests-regression-091.receipt.json','design018-http-create-after.txt','design018-materialization-after.txt'):
    p=R/'evidence'/name
    if p.is_file():
        q=OUT/'evidence'/name;q.parent.mkdir(exist_ok=True);shutil.copyfile(p,q)
readme='''# Focused canonical metadata followup v2

Apply after immutable source-contract-metadata-v1, in an isolated source-owned candidate. No source active files or production data were modified by this handoff. v1's builder hook and exact occurrence/source evidence joins remain unchanged; do not replace newer source parsers with this copy.

Changes: CallbackDescriptor1.0 now admits role-aware none/unknown and unsupported concrete-subclass strategies; unknown roles cannot assert positive proxy/Binder materialization. Role/materialization constraints are embedded in Entry and OpenAPI too. No new evidence can become runtime wire/authority proof. CRUD creation moves to item PUT with If-None-Match:* (201 new,412 exists); replacement uses If-Match (200), both/missing headers400/428. Collection POST is removed. OpenAPI1.6 documents bounded history pagination. Existing precondition revision/invalidation remains conservative global metadata generation by design. Cursor retries under the same reader/generation are allowed; DELETE retry412 after prior success does not repeat effects. Canonical profile is named meta-json-v1 with literal Unicode/numeric vectors. No JCS equivalence is claimed. Public representation strips internal document_json.

files/ contains only files differing from v1, and post-v1.patch has exact v1 bases. Check every expected_v1_sha256 before applying; if the source owner has newer changes, merge the hunks preserving them. schema-additions.json is a selected-component merge, not a whole Entry/OpenAPI overwrite. context/openapi.json and context/API-CONTRACT.md are reference context only; keep newer unrelated paths/definitions and set the combined spec version deliberately. The unchanged v1 merge helper does not apply openapi_info_version automatically, so set info.version to1.6.0 after merging these paths. Generator scripts are included as provenance; do not blindly run them on source owner files.

Full own candidate suites:307 normal and307 optimized, ResourceWarning errors enabled, no warnings. v1's isolated305/305 and207427row structural audit remain historical; these are narrower contract followups, not evidence of a new production build or Kotlin runtime. Two additional role/canonicalization cases are in the changed tests. Re-run source integrated normal+optimized tests and real export validation; fingerprint every changed schema/helper before the next production build. Do not relabel existing cache/catalog bytes. Design018's ADB ingress-only correction is separately handed off in integration/design018-ingress-profile-actions.json and is not a source metadata payload change.

Independent design acceptance is pending. Claude review019 freezes the newer design, not an assertion that a running server or production catalog implements it.
'''
(OUT/'README.md').write_text(readme)
manifest=dict(created_at=now(),base_handoff_manifest_sha256=sha(OLD/'manifest.json'),changes=rows,files=[dict(file=str(p.relative_to(OUT)),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(OUT.rglob('*')) if p.is_file()],source_active_files_modified=False,normal_tests=307,optimized_tests=307)
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
archive=OUT.with_suffix('.zip')
with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(OUT.rglob('*')):
        if p.is_file():z.write(p,p.relative_to(OUT))
action=dict(created_at=now(),handoff_path=str(OUT),archive_path=str(archive),archive_bytes=archive.stat().st_size,archive_sha256=sha(archive),manifest_sha256=sha(OUT/'manifest.json'),changed_files=len(rows),normal_tests=307,optimized_tests=307,source_active_files_modified=False,instructions=str(OUT/'README.md'),production_rebuild_claimed=False)
(R/'integration/source-contract-metadata-v2-actions.json').write_text(json.dumps(action,indent=2)+'\n')
for p in sorted(OUT.rglob('*'),reverse=True):p.chmod(0o555 if p.is_dir() else 0o444)
OUT.chmod(0o555);archive.chmod(0o444)
print(json.dumps(action,indent=2))
