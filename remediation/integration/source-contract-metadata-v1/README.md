# Canonical source metadata handoff 1.0.0

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
