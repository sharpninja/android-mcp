# Android binding catalog — blocked corpus, versioned contract package

**Status: incomplete. This package is not the requested complete Google catalog.**
No Google signature, AIDL or permission-definition source bytes were retrieved.
No production binding records were generated. `contracts/schema-template.sqlite`
is an empty schema template, not an aggregated dataset. Tests use synthetic data
in temporary directories and never mix it into production output.

## Verified target and retrieval blocker

Google's June 16, 2026 release announcement identifies Android 17 and API 37:
https://android-developers.googleblog.com/2026/06/Android-17.html
Google's SDK setup page specifies compileSdk and targetSdk 37:
https://developer.android.com/about/versions/17/setup-sdk
The setup page still has preview wording in its install instructions; the older
SDK platform release-notes page lists API 36. Therefore use Android 17/API 37 as
the release target, but **exact current stable SDK package revision and AOSP
commit have not been verified**. Do not substitute an API 36 or main-branch
snapshot. No AOSP release tag or commit is fabricated in this package.

`output/coverage-manifest.json` records URLs, errors, counts and evidence. Shell
downloads from dl.google.com returned HTTP 403, including a require_escalated
execution attempt. android.googlesource.com connections failed at proxy CONNECT
with 403 Forbidden. No auto-review rejection was returned; this is an observed
network/proxy/HTTP failure, not a confirmed permission-policy denial. The precise
origin of dl.google.com's 403 was not established. Web access to a human-readable
AOSP directory works, but raw metadata access failed and is not a bulk byte
transfer route. No circumvention was attempted.

Needed setup: approved HTTPS GET/CONNECT access on port 443 to `dl.google.com`
and `android.googlesource.com` from this execution environment, or verified Google
SDK/AOSP source archives supplied locally with immutable revisions and hashes.
No configuration or allowlist was changed. Common local SDK locations under
/opt, /usr/local/lib, /usr/lib and workspace shared/cache areas were checked;
no usable android.jar, API signature set or SDK metadata was found there.

## Files and reproducibility

- `schema.sql`: SQLite contract, sources, declarations, AST relations, AIDL raw
  schemas, unresolved issues, immutable imports, local overlays and registry tables.
- `migrations/001-initial.sql`: initial migration; identical to schema.sql.
- `contracts/*.schema.json`: JSON Schema 2020-12 contracts.
- `contracts/openapi.json`: OpenAPI 3.1 CRUD/admin design. **No HTTP implementation**.
- `tooling/extract.py`: offline ingestion foundation, not a comprehensive extractor.
- `tooling/build_contracts.py`: reproducible contract document generator.
- `tests/`: synthetic parser, schema, ID, AST, SQLite and immutability checks.
- `output/validation.json`: actual fixture/contract validation results.

Run `python3 -m unittest discover -s tests -v`. JSON Schema tests require
`jsonschema`. Rebuild contracts with `python3 tooling/build_contracts.py`.
Run offline ingestion with `python3 tooling/extract.py inventory.json NEW_OUTPUT`.
It refuses an existing catalog and requires SHA-256, a 40-hex commit or an exact
`sdk-artifact:` revision string, URL, retrieval timestamp, API, channel and license
for every source. The `sdk-artifact:` string is a label, not proof: the importer
must independently verify it against Google repository metadata. It rejects
non-stable inputs; preview/development corpora need separate catalogs.

The foundation supports common metalava signature lines and manifest permission
elements. It retains unknown lines with raw text. AIDL files are retained intact
with simple package/descriptor identification; members, imports, transaction
numbers, wire codecs and enforcement mappings remain unresolved. There is no
automatic getter/setter property synthesis. It has not been tested against a real
current Google corpus and must not be described as production-complete. It does
not resolve Java generic type-variable erasure, complex annotation-type headers,
inherited members, multi-line declarations, or symbolic permission constants.
Use the source text and issues to finish those parsers before production import.
Duplicate canonical declarations are represented as source occurrences and full
`binding_declarations` documents. `bindings.raw_signature` is only the first seen
representative; never use it to select the current snapshot's declaration.

## Versioning and identity contract

Schema version `1.0.0` is independent from SQLite `user_version=1`, Android release
`17`, API `37`, source revision/hash, registry generation, and local overlay
revision. A schema major bump changes meaning or removes/renames required fields;
consumers reject unrecognized majors. Minor versions add optional non-semantic
data; patch versions clarify constraints without altering represented meaning.
Each released schema document is immutable and exact-version identified. A
consumer supporting a later 1.x document can accept older 1.x data using that
document. Older consumers preserve opaque `x-*` extensions and `extensions`
values, but do not interpret them as permission grants. Unsupported
`required_semantics`, unknown AST nodes, unresolved types/codecs or unknown
enforcement semantics make the record non-executable pending explicit resolution.
Minor changes cannot redefine existing fields or hide authorization changes in
extensions. Producers must include such changes in `required_semantics`.

ID v1 is lowercase SHA-256 of UTF-8 canonical qualified signature, independent
of source revision, annotations, API level and local overlay revision. Package
uses its qualified name; type uses qualified package plus source nested type name;
method uses `owner#name(comma-separated-types)`; constructor uses
`owner#<init>(types)`; field/property uses `owner#name`. Whitespace is normalized,
parameter names and annotations excluded. Preserve qualification, array rank,
varargs spelling and generic type text. Return type is in the declaration, not
the identity. Raw unresolvable signatures must remain issues instead of guessed
IDs. Canonicalization changes require an ID algorithm major change and explicit
alias/migration map; never silently change IDs during refresh. This initial
parser is a limited implementation of that algorithm, requiring full-corpus
verification before publication. AIDL overload IDs must wait for a correct
resolved member parser. Descriptor alone never proves service availability or
transaction numbering.

## Permission contract

AST nodes: `permission`, `all`, `any`, `conditional`, `access` (read/write),
`manual`, `explicit-none`, `unknown`. A conditional predicate missing from an
annotation is an unknown condition, not false or unconditional permission.
Manual enforcement is unresolved backend authorization. Symbolic references
are retained as unknown expressions until exact source resolution is available.
Missing annotation is unknown. `explicit-none` means an explicit annotation,
not proof that all runtime access is unrestricted. Preserve every annotation and
source line separately; multiple applicable requirements compose with `all`.
Class-level or inherited annotations are not automatically resolved by this
foundation. Never treat missing source relationships as absent requirements.

Permission definitions and protection levels are separate from binding relations.
Runtime AppOps, roles, UID/system/signature status, backend checks and user MCP
policy are independent gates. CRUD authorization is catalog administration only;
it never grants a client invocation permission. An overlay cannot silently weaken
an imported enforcement requirement: effective requirements compose imported
requirements with overlay requirements using `all`, preserving unresolved gates.

## CRUD, concurrency and effective view

The OpenAPI describes bounded read/search, local create, full local overlay
replace, suppression/delete, history, staged import, registry read and atomic
publish. Catalog scopes are `catalog:read` and `catalog:admin`. Authentication
issuer, transport and remote exposure are unconfigured; the placeholder token
URL is intentionally nonfunctional. No server is deployed.

Strong opaque ETags cover registry generation, imported declaration hash, overlay
revision and effective document hash. Every PUT/DELETE and publish requires an
exact If-Match: absent returns 428, mismatch 412. ID or canonical identity changes
return 409 and require a new entry. POST/import/publish require Idempotency-Key;
store canonical request hash and original result atomically. Same key/hash returns
the original result; different hash returns 409. Retain keys for the lifetime of
the registry/history, not a short retry window. Application validation, not SQL
alone, enforces these protocol requirements.

Imports append immutable source snapshots and declarations. Local changes live
in overlays and append-only history, never rewrite Google records. The effective
view joins active registry sources to declarations by source occurrence, then
applies the matching local overlay. Exactly one source declaration must be
resolved for an ID or the record is conflicted, not chosen arbitrarily. Normal
search excludes suppressed/deleted/conflicted records. Admin inspection/history
can expose them with their current ETag. Deleting imported entries creates a
suppression; deleting local entries creates a tombstone. Both retain history and
provenance. Restore is explicit PUT against the tombstone/history ETag. No
physical cascade deletes. References to suppressed records remain resolvable as
tombstones; clients cannot infer that the referenced type is usable. Reject
new references to missing IDs (422); preserve unresolved external types with
explicit status/reason instead of inventing foreign records.

Refresh stages a new registry, validates all hashes/references/count reconciliation,
then checks each overlay's base hash. Unchanged base carries the overlay forward;
changed base marks it conflicted; disappeared imported records retain overlay
history but are unavailable/conflicted. Suppressions persist across refresh and
reappearance unless explicitly restored. Resolve conflicts by admin review and
PUT rebased on the new imported record; record before/after and actor. No automatic
last-writer-wins. Imported enforcement AST cannot be weakened by this operation.

Publish uses one `BEGIN IMMEDIATE` transaction: check staged ETag and expected
active pointer, insert validated immutable records, rebase/mark conflicts, append
history, switch active pointer and commit. Failure rolls back all effects.
Readers use a read transaction and pinned view version; pagination tokens bind
registry, overlay view generation, normalized filters, last ID and expiry.
Limit defaults to 100 and is capped at 500. Stale or unavailable pinned views
return 410 `cursor_expired`, never silently resume against changed data.

SQLite JSON columns store canonical UTF-8 JSON validated against the matching
schema by the application. Foreign keys must be enabled per connection. There
is no reliance on SQLite JSON1 or FTS for basic queries. Use indexed keyset
pagination, parameterized commands and bounded result sizes in .NET SQLite.
Migrations run transactionally with backup, schema-version checks and post-migration
FK/integrity validation. Unknown schema majors block reads for execution and all
writes; preserve source bytes for a supported migrator. Schema changes never
rewrite source provenance. Initial DDL is supplied; no speculative future
migrations are claimed. A migration must validate registry/overlay semantics
before publishing any converted effective view.

## Coverage completion work remaining

Retrieve Google SDK repository metadata and select exact highest stable API 37
artifact/extension revision, hashes and source package. Reconcile SDK jar public
and protected symbols against complete release signature/annotation artifacts,
including java/javax, modular SDK and optional public SDK libraries. Enumerate
the pinned release manifest's projects and all relevant .aidl files (framework,
modules, native and hardware); include versioned AIDL snapshots without silently
deduplicating incompatible versions. Obtain release permission manifests,
external annotations, licenses and compiler/version metadata. Correctly parse
all retained failures, cross-reference permission constants, enumerate exclusions,
and prove transaction metadata only from matching compiler/generated sources.
Validate real allOf/anyOf/manual/unknown mappings and every source declaration.
Only then produce and publish the actual catalog/JSONL corpus. No OEM/private,
runtime service-registration or universal authorization coverage is claimed.

## Attribution

No Google/AOSP source code is redistributed in this package. Google web pages
above are attribution/evidence links; summaries record release facts, not source
files. Retrieved source licensing must be retained per file and NOTICE before a
future corpus is distributed; AOSP is not uniformly Apache-2.0 (e.g. libcore).
Synthetic fixtures are newly generated and are not Android API samples or real
permission names. Generated tooling/contracts are provided for the requested
project without claiming Google's endorsement.

## Official mirror assessment

The GitHub connector successfully read metadata for the official
`aosp-mirror/platform_frameworks_base` mirror. It is archived (public page gives
2023-11-08), so it does not establish current API 37 coverage. The tags collection
URL was rejected by the connector endpoint allowlist with HTTPError 400
INVALID_ARGUMENT; that operation was not retried. No mirror source data was
imported. See the coverage manifest for exact URL and error.
