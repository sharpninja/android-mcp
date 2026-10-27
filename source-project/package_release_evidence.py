"""Compact standalone contracts, permissions, reproducibility and evidence bundle.
No bulk Android source payloads or database are duplicated here.
"""
import pathlib,json,hashlib,zipfile,sys
R=pathlib.Path(__file__).resolve().parent;v=sys.argv[1];E=R/('release'+v+'-evidence');D=R/'deliverables';D.mkdir(exist_ok=True)
manifest=json.loads((E/'release-manifest.json').read_text());p=D/('android-catalog-PARTIAL-release'+v+'-schema-permissions-evidence.zip')
if p.exists():raise ValueError('Preserve existing delivery variant')
inv=json.loads((R/('source-inventory-release'+v+'.json')).read_text());cache_root=R/inv.get('cache_evidence_root','.')
cache_producer=cache_root/'catalog/tooling';java_report=json.loads((cache_root/'sdk37.2/java-extraction-report.json').read_text())
files={}
def add(source,name=None):
    source=pathlib.Path(source);name=name or str(source.relative_to(R));files[name]=source
for source in E.rglob('*'):
    if source.is_file():add(source)
for source in (R/('permission-export-release'+v)).rglob('*'):
    if source.is_file():add(source)
for source in (R/'build-snapshots'/('release'+v)).rglob('*'):
    if source.is_file() and '__pycache__' not in source.parts:add(source)
for name in ('source-inventory-release'+v+'.json','requirements-extraction.txt','repository2-3.xml','project-coverage.json','production_reconciliation.py','test_production_reconciliation.py','restore_java_caches.py','test_restore_java_caches.py','sdk37.2/source-archive-receipt.json','sdk37.2/platform-metadata/metadata-range-receipt.json','reproducibility-smoke-release'+v+'/result.json'):
    add(R/name)
for source in cache_producer.glob('*.py'):add(source,'historical-java-cache-producer/'+source.name)
for name in ('java-extraction-report.json','api-index-report.json'):add(cache_root/'sdk37.2'/name)
for source in inv['sources']:
    if source['kind']=='sdk-api-index':add(R/source['metadata_cache']['path'])
    if source.get('transport',{}).get('metadata_receipt'):add(R/source['transport']['metadata_receipt'])
attribution=[]
for source in inv['sources']:
    attribution.append({k:source.get(k) for k in ('repository','revision','path','url','license','sha256')})
    if source['kind']=='evidence' and source['status']=='downloaded':add(R/source['local_path'])
readme=f'''# Android catalog release {v} — validated PARTIAL evidence

The runtime database and complete entry exports are separate downloads. This
bundle contains versioned DDL/migrations, JSON Schemas, OpenAPI3.1 CRUD contract,
schema-validated permission metadata, complete pinned inventory, extractor,
fixture tests, source receipts, attribution and production validation reports.
It implements no HTTP service or Android bridge and grants no authority.

Read `release{v}-evidence/README.md`, the coverage manifest and reconciliation
reports before using the data. All {manifest['counts']['bindings']:,} entries are discovery metadata.
Unknown field types, wire codecs, OS authority and client grants stay unknown.
The next source candidate includes later AIDL parser fixes; this snapshot is
immutable and does not claim global all-current completeness or98/98 review.

To reproduce in a NEW writable directory, extract this bundle there. Install
the pinned Python requirements only in an environment you authorize; this task
did not change the system installation. The tested task environment used Python
3.14, tree-sitter0.25.2 and tree-sitter-java0.23.5. Indexed Point access avoids
the diagnosed getter defect. Review sources and commands before network use.

1. `python build-snapshots/release{v}/catalog/tooling/restore_snapshot.py source-inventory-release{v}.json`
   performs an offline dry run. Add `--download` explicitly to retrieve missing
   pinned public Google source bytes. It honors429, stops access denials, never
   installs SDKs or accepts licenses, and restores only source/metadata members.
2. `python restore_java_caches.py source-inventory-release{v}.json historical-java-cache-producer`
   restores missing historical Java cache bytes and verifies their frozen hashes.
   Existing differing caches or incomplete diagnostics are preserved and rejected.
   The exact API-index cache is included because its original gzip timestamp is
   part of its byte hash. Historical producer provenance is not relabeled.
3. `python build-snapshots/release{v}/catalog/tooling/build_catalog.py source-inventory-release{v}.json NEW-OUTPUT`
4. `python build-snapshots/release{v}/catalog/tooling/validate_catalog.py NEW-OUTPUT source-inventory-release{v}.json NEW-VALIDATION.json`
5. `python build-snapshots/release{v}/catalog/tooling/export_permissions.py NEW-OUTPUT NEW-PERMISSION-OUTPUT`

Use a new output path; published snapshots are never overwritten. Reproduction
checks semantic/source/record identity; wall-clock publication timestamps can
change SQLite bytes. The provided manifest hashes identify original output.

Google/AOSP retains original copyright and licensing. No blanket relicensing is
asserted. Complete source attribution is in source-attribution.json; evidence
blobs preserve selected license/NOTICE text. The original source-data archive,
source headers and pinned repositories are authoritative for applicable terms.
'''
hashes=[]
with zipfile.ZipFile(p,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for name,source in sorted(files.items()):
        data=source.read_bytes();z.writestr(name,data);hashes.append(dict(path=name,bytes=len(data),sha256=hashlib.sha256(data).hexdigest()))
    z.writestr('source-attribution.json',json.dumps(attribution,indent=2));z.writestr('README.md',readme);z.writestr('bundle-files.json',json.dumps(hashes,indent=2))
if p.stat().st_size>16_000_000:raise ValueError('Bundle exceeds safe attachment cap; preserve it and make logical variants')
receipt=dict(path=str(p),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),files=len(files))
(D/('release'+v+'-evidence-delivery.json')).write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt,indent=2))
