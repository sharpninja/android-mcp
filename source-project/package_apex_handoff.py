import pathlib,json,hashlib,shutil,datetime,zipfile
R=pathlib.Path(__file__).resolve().parent;D=R/'integration/apex-module-producer-v2';D.mkdir(exist_ok=False)
for name in ['apex_manifest_facts.py','test_apex_manifest_facts.py','apex_metadata_inventory.py','test_apex_metadata_inventory.py','interface_module_facts.py','audit_apex_metadata.py','integration/apex-source-facts-v2-tests.log','outside-corpus-audit/source-inventory-batchapex-metadata-v1-acquired002.json']:
 p=D/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(R/name,p)
shutil.copytree(R/'integration/apex-source-facts-v2',D/'facts')
inventory=json.loads((R/'outside-corpus-audit/source-inventory-batchapex-metadata-v1-acquired002.json').read_text())
for s in inventory['sources']:
 raw=(R/s['local_path']).read_bytes()
 if hashlib.sha256(raw).hexdigest()!=s['sha256']:raise ValueError('Source mismatch')
 p=D/'google-source'/s['repository']/s['path'];p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(raw)
(D/'README.md').write_text('''# APEX source/module metadata v2

Finite pinned Google source cohort:163 manifests and138 nearest build files,
301paths/300distinct Git objects; all downloaded and receipt/hash reconciled.
One transient503 retried successfully at the same pinned URL; no denial bypass.
The v1 strict JSON pass retained14 comment-bearing manifests as unsupported;
v2 handles comments without changing string literals and preserves raw sources.
All163 manifests now parsed;160 apex/apex_defaults declarations and114 explicit
build-to-manifest links retained.12 fixtures pass.

Manifest name and version are source declarations. Many explicit comments call
version0 a build placeholder. No installed-version range or product applicability
is inferred. Soong names, apex_available selectors, manifest name, AIDL frozen
version and Android API version remain separate domains. Defaults/expression
dependencies remain unresolved; a missing manifest property is not auto-linked.

Use raw_properties/raw_source_text and source identities for provenance; normalized
version codes are decimal strings (signed64 max) suitable for language-neutral
Kotlin/SQLite consumption. Unrecognized/out-of-range raw values remain unknown.
Producer metadata cannot grant OS/client access. Integrate into a fresh matched
builder/schema snapshot; running release011 is unchanged. No upload occurred.
''')
report=json.loads((D/'facts/report.json').read_text());m=dict(at=datetime.datetime.now(datetime.timezone.utc).isoformat(),counts=report['counts'],tests=12,files=[])
for p in sorted(D.rglob('*')):
 if p.is_file():m['files'].append(dict(path=str(p.relative_to(D)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'manifest.json').write_text(json.dumps(m,indent=2));Z=D.with_suffix('.zip')
with zipfile.ZipFile(Z,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(D.rglob('*')):
  if p.is_file():z.write(p,p.relative_to(D))
receipt=dict(at=m['at'],directory=str(D),archive=str(Z),bytes=Z.stat().st_size,sha256=hashlib.sha256(Z.read_bytes()).hexdigest(),counts=report['counts'],tests=12,uploaded=False)
(R/'integration/apex-module-producer-v2-actions.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt,indent=2))
