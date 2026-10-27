"""Freeze the passing local source-owner integration handoff, with exact hashes."""
import pathlib,json,hashlib,datetime,shutil,zipfile,re
R=pathlib.Path(__file__).resolve().parent;W=R/'integration/source-contract-metadata-v1-work';D=R/'integration/source-contract-metadata-v1'
if D.exists():raise SystemExit('Immutable handoff already exists')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for name in ('verification-normal.txt','verification-optimized.txt'):
 text=(W/name).read_text()
 if not re.search(r'Ran 305 tests in ',text) or '\nOK\n' not in text or 'ResourceWarning' in text or 'Exception ignored' in text:raise ValueError('Validation not green '+name)
E=W/'evidence';E.mkdir(exist_ok=True)
for name in ('source-metadata-real-exports-validation.json','source-metadata-integration-before.txt','source-metadata-integration-after.txt','source-metadata-references-before.txt','source-metadata-references-after.txt','tests-regression-088.receipt.json','tests-regression-089.receipt.json'):
 shutil.copyfile(R/'evidence'/name,E/name)
report=json.loads((E/'source-metadata-real-exports-validation.json').read_text())
if report['total_failures']!=0:raise ValueError('Real producer exports not green')
summary=dict(completed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),normal_tests=305,optimized_tests=305,source_candidate_baseline_tests=272,additional_metadata_tests=33,resource_warnings=False,source_files_modified=False,production_modified=False,real_export_rows=sum(v['rows'] for v in report['results']),real_export_families=len(report['results']),real_export_failures=0,source_join_status='Default occurrence callback/functional facts hooked and tested; source owner must configure exact interface/native evidence_lookup and retain unjoined sidecars',not_runtime_implemented=['HTTP host/authenticated cursors','Kotlin server','callback subscriptions/streams','functional group activation','device availability probes/wire codecs'])
(W/'validation-summary.json').write_text(json.dumps(summary,indent=2)+'\n')
(W/'README.md').write_text('''# Canonical source metadata handoff 1.0.0

Passing offline integration candidate; no source-owner active files changed.
Canonical key: `Entry.extensions.source_contract_metadata`, a versioned occurrence
variant set. This replaces the three proposed ad-hoc source evidence keys in the
producer field proposal. Existing Entry envelope stays1.1; OpenAPI additive1.5.0.

## Integrate into an isolated source candidate

1. Verify `manifest.json` SHA-256 entries. Start from the latest source candidate,
   preserving its parser fixes. Compare the base builder hash in `preparation.json`.
2. Copy the NEW files under `files/` to the candidate. Existing helper files require
   an exact content comparison; never overwrite a newer producer silently. The
   callback helper is the source owner's v2, not the old false+sparse sync inference.
3. Run `python apply_schema_additions.py <isolated-catalog-root>` to merge only the
   new metadata definitions, extension and routes. Do not replace the whole Entry,
   OpenAPI or existing schemas. It preserves unrelated fields and paths.
4. Review/apply `build-hook.patch` only against its pinned base or merge equivalent
   lines manually. It fingerprints new schema/helper inputs and attaches validated
   original declaration occurrences BEFORE effective serialization/hash/export.
   `verification-catalog` is the exact passing source272+metadata33 candidate.
5. Configure the hook's trusted `evidence_lookup(source_id, occurrence)` for actual
   interface/AIDL/HIDL/native evidence. Join using repo/revision/path/sha256 and
   current source registry, never old release IDs or names alone. Supported keyword
   results are `interface_evidence`, `aidl_source_constraints`,
   `hidl_source_constraints`, `native_abi_evidence`, `resolved_types`,
   `lifecycle_pairs`. Missing proof remains unknown. Preserve unmatched sidecars.
   The default tested hook emits occurrence callback/functional facts only; it does
   NOT claim full auxiliary producer integration or positive device availability.
6. Apply `migrations/source_metadata_ddl.sql` only as the additive offline metadata
   schema; it is not a replacement for bridge-config.sql or a deployed service.
   Implement protected-owner HTTP middleware/serialization separately per contract.
7. Run full normal and optimized suites, then a new full immutable production build
   and schema/FK/hash/export validation. Do not modify existing published releases.

Raw producer DTO IDs inside retained evidence describe their original audit
registry. The authoritative current-build occurrence FK is source_context.source_ids;
the importer must record the exact source-tuple join and origin registry witness.
Do not reinterpret raw audit IDs as current IDs or rewrite raw evidence without
preserving its input hash and provenance.

## Evidence and limitations

305 normal +305 optimized tests pass without resource warnings. Eleven complete
real producer export families pass structural/schema validation, including unknown
AIDL/HIDL branches, nullable Java direction and computed native descriptor forms.
Counts/hashes are in validation-summary.json and the export report. This is neither
Google API completeness nor runtime/codec/registration proof.

The initial isolated run's seven errors were missing historical SQL fixture files;
the single failure was a fixture expecting attachment always to change a hash even
when the builder already attached metadata. Support files are now present and the
fixture checks both hash change from an unextended Entry and idempotent reattachment.
The original failed log is retained. No requirement/test was dropped.

The original review015/017 frozen artifacts predate this handoff. Latest user scope
now requests Claude DESIGN ONLY. Active018 excludes actual Google surface,
extractor and production coverage; this handoff has not received external acceptance.
No evidence/source/catalog ZIP upload is requested or performed.
''')
W.rename(D)
rows=[dict(file=str(p.relative_to(D)),size_bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(D.rglob('*')) if p.is_file()]
(D/'manifest.json').write_text(json.dumps(dict(created_at=summary['completed_at'],files=rows),indent=2)+'\n')
archive=R/'integration/source-contract-metadata-v1.zip'
with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED) as z:
 for p in D.rglob('*'):
  if p.is_file() and 'verification-catalog' not in p.relative_to(D).parts:z.write(p,str(p.relative_to(D)))
actions=dict(created_at=summary['completed_at'],handoff_path=str(D),archive_path=str(archive),archive_bytes=archive.stat().st_size,archive_sha256=sha(archive),manifest_sha256=sha(D/'manifest.json'),normal_tests=305,optimized_tests=305,real_export_rows=summary['real_export_rows'],real_export_families=11,real_export_failures=0,canonical_key='Entry.extensions.source_contract_metadata',source_active_files_modified=False,producer_join_pending=summary['source_join_status'],instructions=str(D/'README.md'),review_acceptance_claimed=False)
(R/'integration/source-contract-metadata-v1-actions.json').write_text(json.dumps(actions,indent=2)+'\n')
for p in D.rglob('*'):
 if p.is_file():p.chmod(0o444)
for p in sorted([p for p in D.rglob('*') if p.is_dir()],key=lambda p:len(p.parts),reverse=True):p.chmod(0o555)
D.chmod(0o555)
print(json.dumps(actions))
