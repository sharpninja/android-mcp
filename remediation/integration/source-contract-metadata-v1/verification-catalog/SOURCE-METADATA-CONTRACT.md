# Source metadata integration contract 1.0.0

This document specifies offline catalog production and a SQLite reference oracle.
It does not implement a Kotlin service, HTTP listener, callback runtime, group
activation, device probe or verified wire codec. Current design remains separate.

## Canonical representation

`Entry.extensions.source_contract_metadata` is a `SourceContractMetadataSet`:
`schema_version`, `entry_id`, and nonempty `variants`. Each variant is a
`SourceContractMetadata` envelope tied to one immutable declaration occurrence.
The optional addition keeps the Entry 1.1 envelope; the embedded contract has its
own 1.0.0 version and strict known fields. Unknown later contract versions must
remain preserved as opaque import evidence, explicitly unsupported for runtime
interpretation; do not silently normalize them into this version.

An exact variant carries source IDs and the SHA-256 of its original
`binding_declarations.document_json` bytes. `attach_occurrences` verifies those
bytes, identity and provenance before generating facts. No one-way flags, inherited
type shapes or argument roles are borrowed from the aggregate effective Entry.
Without exact occurrence context, `aggregate-unresolved` is retained with a
reason, unknown reply mode and no inferred inheritance. Duplicate exact contexts
are rejected. The helper is a build hook before effective Entry serialization,
record hashing and export; it does not mutate original declaration rows.

Source IDs depend on processing provenance. Join evidence using verified
`(repository, revision, path, sha256)` against the current build's source registry,
then bind its new source ID. Never carry release011 source IDs into a new build.
Do not match on a basename, owner name, descriptor literal or API level alone.
The importer must reject a join whose byte hash, repository or revision differs.
`evidence_lookup` is trusted importer code, not a request-supplied callback.

## Evidence and uncertainty

`interface_evidence` preserves source-interface-evidence/1 DTOs, ordered published
hash candidates and Soong/APEX availability selectors. A build module is not an
installed Android module identity. Published source candidates do not prove a
runtime interface hash, generated ABI equivalence or installed version range.
`hidl_source_constraints` preserves exact source-hash predicates OR explicit
unknown reasons. Translate a validated predicate into a DeviceResolution Rule
only through its defined clause; do not put the whole proof envelope into a Rule.
No platform API range is inferred from HIDL/AIDL version numbers.

`callback_facts` preserves type disposition, endpoint method, parameter and
lifecycle-pair candidates. AIDL reply mode is one-way if either explicit or
interface-inherited flag is true, synchronous only if both are explicitly false,
and otherwise unknown. HIDL uses its own explicit one-way flag. Other source
languages remain unknown here: Java `void` is not Binder one-way proof. Null
parameter direction stays null. Name heuristics never prove callback direction,
registration, cleanup, threading, return policy or stream semantics.

`functional_facts` preserves annotation facts and namespace search labels. Every
unclassified entry stays searchable; package names do not automatically become
functional groups. Source facts do not populate proven group memberships.

Native evidence includes ABI records, resolved/unresolved source-local type
references, function-pointer parameter candidates and five supported descriptor
declaration families. Preserve raw/computed/unclosed token forms, unresolved build
conditions and unknown runtime registration. ABI local keys/evidence IDs are not
MCP binding IDs. The evidence wrapper fixes confidence to `structural-only` and
runtime observation, wire proof and authority to false. Global native coverage is
not proven by these supported forms. Unmatched evidence remains in a schema-valid
sidecar indexed by source identity, not discarded for lack of an Entry join.

## Declarative resources and owner CRUD

`CallbackDescriptor` and `FunctionalGroup` are separate versioned collections,
not new Entry kinds and not inferred runtime grants. Their exact source contract
hashes pin catalog entities; final device-resolved transitive schema hashes are
computed later. Candidate/unknown callback roles require a reason; proven roles
and cleanup require evidence. Abstract classes require a reviewed, versioned,
hashed prebuilt adapter or explicit unsupported status. No runtime subclassing
or registration follows from saving a descriptor.

Groups contain at most 128 explicit members, required types, pinned dependencies
and atomic registration/cleanup units. Unassigned catalog entries remain available
to discovery. All referenced catalog IDs and hashes must be checked against a
trusted current catalog index. Missing index/reference, wrong hash, non-type type
reference, unknown dependency or cyclic group graph is 422. Dependency revisions
remain pinned; changing a dependency invalidates activation of dependents until a
new coherent revision is selected. Administrative `active` does not mean invocable.

The offline `MetadataCatalog` implements immutable base imports and revision
history, local overlays, imported suppression, local tombstones and explicit
conflict rebasing. Owner/importer capabilities are trusted fixture inputs, never
JSON request fields. A future host must enforce protected owner authorization
and read visibility before invoking it; it is not an authentication provider.

POST requires `If-None-Match: *`; existing identities return 409. PUT and DELETE
require one exact strong ETag. Missing precondition is 428, malformed is 400,
stale is 412, unauthorized owner is 403, missing identity is 404, invalid document
or reference is 422. PUT replaces the whole declarative document, never PATCH.
DELETE preserves history and returns the suppressed/deleted representation. A
repeat DELETE with the current ETag is idempotent. PUT of a tombstone restores
it. A conflicting newer import requires explicit resolution against its current
base hash; merely sending a fresh ETag does not resolve an overlay conflict.

`"meta1:<generation>:<kind>:<sha256(resource_id UTF8)>:<base_hash|local>:<revision>:<state>:<content_hash>"`
is the strong metadata ETag. A successful head change increments the global
metadata generation atomically, invalidating all old metadata cursors and ETags.
The content hash is SHA-256 of sorted-key compact UTF-8 JSON, with non-finite
numbers forbidden. Base/import hashes and local history cannot be edited or
deleted. SQL null cannot satisfy schema-version or identity checks. SQLite FK,
JSON checks and immutability triggers supplement full JSON Schema/semantic
validation; SQL alone does not prove every descriptor field or reference.

Read/list/history HTTP routes are specifications in OpenAPI, not a deployed API.
Metadata list cursors must pin authenticated reader, collection, registry and
metadata generation; bounded keyset pagination, maximum 100 resources, rejects
replayed/cross-reader/stale cursors with 410. Non-active/history visibility is
owner-only. Source metadata reads follow Entry read visibility; absence is an
explicit `not-produced` result with reason, never an empty proof of no callbacks.
This offline oracle currently exercises generation/reader cursor invalidation;
HTTP serialization, opaque authenticated cursors and middleware remain host work.

## Integration and validation

1. Merge the exact canonical schemas/helpers into an isolated source candidate.
   Preserve the source owner's newer parser changes. Copy helper hashes exactly.
2. Add the occurrence hook before `content=js(doc); digest=sha(content)`. Query
   original declaration JSON plus its stored hash; do not use projected or
   aggregate JSON as the immutable hash input. A trusted occurrence projection
   may supply additional source-specific facts only with separately bound proof.
3. Retain source sidecars for unjoined native/interface evidence. Unknown records
   must remain visible, including 173 unresolved HIDL constraints in the tested
   export. Do not fabricate runtime Rule predicates from source selectors.
4. Validate Entry and metadata, re-hash the effective document and regenerate all
   catalogs/SQLite/export manifests together. Keep old published releases immutable.
5. Run normal and optimized suites, real-export structural validation and full
   publication/schema/FK/hash reconciliation on the rebuilt candidate. Exact
   producer/contract hashes must accompany the next review snapshot.

Reference fixtures cover the actual builder occurrence -> metadata -> Entry
schema -> new effective hash path, malformed immutable hash rejection,
unknown preservation, identity checks, source/authority separation, CRUD/CAS,
immutable imports/history, rollback, reference pins and dependency/lifecycle
constraints. They do not demonstrate Kotlin runtime callbacks or production
semantic completeness. Combined review017 intentionally uses the earlier frozen
baseline; these later changes require a fresh review before acceptance.
