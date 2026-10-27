import pathlib,json,zipfile,hashlib,datetime
R=pathlib.Path(__file__).resolve().parent;out=R/'deliverables/android-catalog-SDK37.2-parser-recovery-evidence-release003.zip'
if out.exists():raise ValueError('Preserve existing evidence package')
files=[]
for folder in ('catalog/tooling','catalog/tests','catalog/contracts','catalog/migrations'):
 files.extend(p for p in (R/folder).rglob('*') if p.is_file() and '__pycache__' not in p.parts)
for name in ('source-inventory-release003.json','coverage-manifest-release003.json','build-release003-inputs.json','requirements-extraction.txt','parser-tests-recovery.txt','restore-release003-dry-run.txt','ATTRIBUTION.md','stage_sdk372_sources.py','stage_sdk372_index.py','merge_catalog_inventory.py','make_coverage_current.py','fetch_sdk372_metadata.py','fetch_sdk372_sources.py','repository2-3.xml','project-coverage.json','sdk37.2/java-extraction-report.json','sdk37.2/java-trial-report.json','sdk37.2/java-full-scan-exit.json','sdk37.2/source-archive-receipt.json','sdk37.2/api-index-report.json','sdk37.2/parser-recovery-evidence.json','sdk37.2/annotation-fixture-before.txt','sdk37.2/api-index-tests-before.txt','sdk37.2/jvm-link-tests-before.txt','sdk37.2/jvm-permission-link-before.txt','sdk37.2/aidl-unknown-tests-before.txt','sdk37.2/platform-metadata/metadata-range-receipt.json'):
 files.append(R/name)
manifest=dict(created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),purpose='SDK37.2 extraction/parser recovery evidence; release003 runtime import remains in progress at this snapshot',complete=False,files=[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(set(files))])
readme='''# SDK37.2 parser recovery evidence — PARTIAL release003 inputs

This package contains the completed SDK source scan, fixture/regression evidence,
frozen source inventory, provenance and extractor code. It is NOT a published
runtime database or a new hostile-validation verdict.

Verified scan: 16,893/16,893 Java files; 530,028 declaration records; zero grammar
error regions; process exit0. Current suite:34 tests passed. Exact API lifecycle
index:90,272 current declarations;3,409 historical/future entries separately
enumerated. Core AOSP17r1 permission manifest:1,160 definition elements.

All three Point.row accesses use indexed Point[0]. Original task-local binding
0.26.0 was pinned to0.25.2 before the later no-dependency-change instruction;
Java grammar remains0.23.5. No system package/setup changed. Invalid gzip caches
are quarantined; regenerated caches use validated atomic replacement.

Frozen combined inventory:36,893 downloaded /37,873 enumerated paths, including
all SDK37.2 source-data archive files and selected platform metadata. AOSP subset:
19,977/20,957 selected paths downloaded at freeze time. Collection continues in
the task workspace; this package does not relabel those gaps as complete.

Current design/schema/CRUD hostile-validation remediation is owned by a separate
task. These contracts retain their earlier versions plus the tested publication
gate; no98/98 score is claimed. The old prototype review cannot score this newer
dataset. This source/data task has not invoked a new Claude review.

Bulk source bytes remain preserved locally and pinned by the inventory; they are
not duplicated in this evidence package. Restore/rebuild scripts and hashes are
included. SDK source archives are source data, not SDK installs. No platform
android.jar/class/native payload, license acceptance or device operation occurred.
'''
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for p in sorted(set(files)):z.write(p,str(p.relative_to(R)))
 z.writestr('README.md',readme);z.writestr('evidence-manifest.json',json.dumps(manifest,indent=2))
assert out.stat().st_size<16000000
print(json.dumps(dict(path=str(out),bytes=out.stat().st_size,sha256=hashlib.sha256(out.read_bytes()).hexdigest()),indent=2))
