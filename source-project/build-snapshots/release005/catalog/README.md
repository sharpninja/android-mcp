# Isolated contract remediation candidate

This directory is a design/schema/extractor candidate, not a new production catalog release. Historical release-002 facts below describe the separately owned source task. The current source task has retrieved SDK 37.2 source archive revision 1, verified its published SHA-1, and retained 16,910 files; it has not imported that Java archive into release-002. Source completeness remains incomplete. Current progress and exact hashes are frozen separately for each review. See API-CONTRACT.md for corrected authority, identity, state, ETag, CAS and cursor rules. No semantic namespace re-sharding is authorized or performed.

# Android binding and permission catalog — exact-version partial release

This is a populated, offline catalog of a pinned Android 17 release source snapshot. **It is not a complete catalog of the current SDK.** Google's repository metadata retrieved on 2026-10-26 lists non-preview SDK **37.2 revision 1** (extension 24). This catalog's public signature/annotation snapshot is **37.0**, from AOSP tag `android-17.0.0_r1` and `platform/prebuilts/sdk` commit `0f07e8d60ab0aba46129e427101f96ba459a95c1`. No equivalence to a downloaded SDK binary is claimed.

The primary runtime file is `output/release-002/catalog.sqlite`. `entries.jsonl` is the consolidated discovery export; `catalog.json` contains the same entry documents. Per-table JSONL preserves declaration variants, permission definitions, annotations and raw AIDL. `source-inventory.json` and `coverage-manifest.json` at the bundle root describe exactly which source paths were retrieved and which remain unavailable. All matching paths were enumerated in 102 declared repositories; individual APIs were not handpicked. Excluded repositories are listed in `project-coverage.json` and have unknown file coverage.

Use `bindings` for indexed package/owner/name lookup, `binding_declarations` for exact per-source evidence, `permission_relations` for permission ASTs, `permission_definitions` for manifest metadata, and `annotation_evidence` for external annotation XML. A public permission string constant in `permission_symbols` is **not** a complete manifest definition or proof that an OS build provides/grants it. Release-002 imported only 10 manifest definitions; the newer recovered core manifest contains 1,160 definitions and is not yet imported into that released database. `aidl_schemas` preserves complete acquired source text, imports and parse status. Method evidence retains directions, declared ordinal and explicit method IDs; **wire transaction codes, structured codecs, parcelable layouts, union tags and runtime availability remain unknown**.

`runtime_catalog` merges active local overlay documents with imported entries and excludes suppressed/deleted/conflict overlays. It includes source/test/frozen declarations; consumers must select the intended language, source surface and version using entry extensions and provenance. This is discovery metadata. It cannot authorize execution. OS authority and MCP client grants remain separate, unimplemented concerns. No Android bridge application, HTTP listener or CRUD service is included.

## Identity, versions and compatibility

Entry envelopes support documented `1.x` minor versions (base fields 1.0.0; additions confined to extensions/x-*); administration API is `1.1.0`; corrected catalog storage baseline is `1.2.0`, SQLite `user_version=3`. The additive storage migration adds annotation/evidence tables, declaration hashes and an effective-entry projection. `schema.sql` is the concatenation of the two ordered migrations. This task corrects the isolated baseline migrations; originals are preserved under `../evidence`. Existing databases use checked offline copy migration `tooling/migrate_v3.py` to a new file; never rerun baseline migrations over a release.

Stable entry IDs are SHA-256 of a language-prefixed source signature: `java:owner#method(normalizedType,...)`, `java:owner#<init>(...)`, `java:owner#field`, or `aidl:descriptor#method(...)`. These are **source signature identities**, not validated JVM descriptors. Generic spelling changes may produce a different ID. A declaration revision is its canonical JSON SHA-256, scoped by immutable source ID and locator; changes in permission metadata do not overwrite earlier declarations. Current entry ETags use the single cat1 formula in API-CONTRACT.md, including current registry generation, base hash, identity, overlay revision and state. All source occurrences and their potentially different permissions remain available. Conflicting or source-dependent permission expressions produce an unknown aggregate rather than an invented union or intersection.

Unknown JSON semantics must fail closed for invocation; `x-*`/`extensions` retain additive metadata. The permission AST preserves `value`, `allOf`, `anyOf`, `conditional`, read/write scopes, explicit no-permission and manually enforced annotations. Unsupported annotation attributes and unresolved symbols remain unknown with raw text. Parameter annotations are retained separately and do not become callable-wide permission claims. AIDL interface annotations are retained as context without inventing compiler inheritance behavior. An absent annotation always remains unknown.

Imported tables are immutable. A future administration service uses local overlays and append-only history. `contracts/openapi.json` is an **OpenAPI 3.1 specification only**, covering entry CRUD, history, import and atomic registry publication. Its ETag/If-Match, conflict, idempotency and authorization rules are not an implemented HTTP service. Administrative catalog edits never grant Android or MCP permissions.

For updates, stage a new immutable catalog/registry, validate it, then publish its database by atomic rename or switch a same-filesystem pointer to the complete versioned directory. Readers pin one registry/view generation. Do not replace files inside an open release directory. A future overlay rebase compares base-record hashes; changed/deleted upstream entries become explicit conflicts requiring resolution. Tombstones remain auditable. Imported source IDs and provenance FKs may not be retargeted. `runtime_catalog` is a projection, not a substitute for these service transaction rules.

## Reproduce and validate

The extractor, publication gate, validation and tests need Python 3.10+ and `jsonschema==4.26.0` (the tested environment was Python 3.14 / SQLite bundled with Python). From the bundle root:

```sh
python3 -m unittest discover -s catalog/tests -v
python3 catalog/tooling/build_catalog.py source-inventory.json /tmp/android-catalog-fresh-output
python3 catalog/tooling/validate_catalog.py /tmp/android-catalog-fresh-output source-inventory.json /tmp/android-catalog-validation-report.json
```

The output directory must be empty. Input bytes must match both the pinned Git blob SHA-1 and SHA-256. Fixture tests ran before real parsing. Failed imports do not publish `catalog.sqlite`. Record/source ordering and hashes are deterministic for the same inventory; registry creation and validation timestamps vary. Source acquisition scripts and tree inventories are included for audit/reproduction. They must not be used to bypass a denial or silently retry unavailable sources. The original bulk downloader was stopped after sustained HTTP 429; a subsequent bounded first retrieval of the previously unreached SDK snapshot stopped on any error and succeeded. No denied path was retried. A future collection attempt requires fresh permitted access evidence and should honor rate limits.

See `output/release-002/validation-report.json` for executed schema/integrity/FK/count/reconciliation checks and exact limitations. Tests validate catalog behavior; they do not establish Android runtime behavior or Binder wire correctness. See the separate `hv-preliminary` receipt for the frozen hostile-validation scope; its initial snapshot predates the populated database.


Builder inputs now require explicit catalog metadata (`catalog.registry_version`, `android_version`, `api_level`, `coverage`) and per-source `api_level`/`channel`. Use null for a source's unknown API applicability rather than inventing API37/stable for arbitrary evidence or history. See API-CONTRACT.md. Permission exports take a separate new output directory: `python3 catalog/tooling/export_permissions.py RELEASE_DIR NEW_EXPORT_DIR`; they never edit an immutable release.

Validation writes its report to an explicitly supplied new path outside the immutable release directory; DB and existing exports remain unchanged.


PermissionSummary exports use their own1.1.0 contract and are validated against PermissionSummary.schema.json. Catalog storage remains1.2.0. Positive declared permission evidence is aggregated separately from absent annotations; mixed variants and their source/locator associations remain in extensions.permission_variants. Absence never means explicit-none. See API-CONTRACT.md for publication/history constraints and policy entry-point aliases.
