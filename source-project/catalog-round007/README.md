# Android binding catalog contracts and executable artifacts

This deliverable contains the bridge design, catalog extractors, SQLite schemas, JSON contracts and offline policy/CRUD fixtures. It contains no Android application, HTTP administration service or deployed MCP bridge. Catalog metadata never grants OS authority or MCP client permission. No device operations or software installation were performed.

Production acquisition/import belongs to the source task. Release005 completed its import with37,873 sources,772,733 binding entries and1,384 permission definitions; independent validation and exports are recorded only when completed. Its build snapshot predates later remediation fixes. A complete selected corpus is not global coverage:985 excluded repositories and unresolved SDK/type/wire semantics remain explicit. Historical release002 validation and old review scores remain historical. No overall98/98 acceptance is claimed here.

## Identity and contracts

Entry schemas use1.1.0 identifiers with a1.0 envelope baseline. New1.1 vocabulary (type, aidl-parcelable, aidl-union, aidl-enum) is rejected in1.0 documents and requires supported1.1+ minors, including minor10+. Other additive metadata uses extensions/x-*. Unknown majors and malformed known ASTs reject. permission_reader.read quarantines unknown semantic values with retained parsed JSON; original lexical bytes remain immutable evidence. Unknown is never permission-free.

Canonicalizationv2 hashes the complete canonical string with SHA-256:

| Prefix | Forms |
| --- | --- |
| java: | owner type; owner#method(normalizedTypes); owner#<init>(types); owner#field; package:qualified.package |
| java-source: | The same source-spelled forms, including package:, from Java AST evidence |
| aidl: | descriptor type; descriptor#method(normalizedTypes); descriptor#field |
| jvm: | binary/Owner; binary/Owner#name(argumentDescriptors)returnDescriptor; binary/Owner#fieldName where the index omits field type |

Namespaces remain distinct until explicit equivalence is proven in extensions.proven_current_sdk_jvm_identity. Spelling changes may change source IDs. Equivalence never silently retargets grants. Source identity scheme3 combines pinned acquisition facts and processing/code/cache fingerprints; older schemes remain historical. Per-source declaration hashes and permission variants are retained. Absence of an annotation is unknown, not explicit-none. Positive discovery aggregates cannot widen authority.

## Storage and administration

Catalog storage is1.2.0 / SQLite user_version3. schema.sql concatenates the ordered baseline migrations; originals remain under evidence. migrate_v3.py performs a checked offline v2 copy to a new file, preserves the original and marks semantic rebuild false. It loads memberships while staged, restores terminal states, then installs pointers through collision/base guards. Existing v3 candidates are not upgraded in place.

Only staged registries accept membership/source links. Validation seals them; publication is single-published and generation-monotonic. runtime_catalog excludes suppressed/deleted/conflict overlays. Changed/missing imported bases and local/import collisions, including deleted local tombstones, require explicit conflicts. Overlay/history FKs, append-only reason sidecars and CAS constraints are executable; full document validation and transactions are specified in API-CONTRACT.md and contracts/openapi.json.

The single cat1 ETag composition is shared by views and the offline oracle. Cursors pin registry/overlay generations. Owner reasons are retained in entry_history_reasons and returned as HistoryEvent.reason; legacy/system absence is not fabricated. Registry.api_level can be null. bridge-config.sql is a separate executable fixture for future app-private client configuration; runtime projections never replace immutable catalog evidence.

## Extraction and limits

Inputs include SDK/source signatures, AIDL, manifests, annotation ZIPs, Java-source caches and SDK lifecycle-index caches. Source SHA-256 and local Git-format addresses are checked. Cache bytes/counts and producer fingerprints are retained. Source owners must regenerate changed parser caches, never relabel old records. Platform context selects Java/index/annotation equivalence; explicit mismatches reject and unknown applicability is not guessed. The SDK index parser requires an explicit target version.

Java AST metadata retains imports, type variables, record components and compact-constructor parameters. Wildcard/inheritance/type-variable resolution stays conservative; record-component annotation propagation is not invented. Point positions use tuple indexing. The confirmed binding getter defect is distinct from the inferred connection to the earlier exact crash. No source is executed or compiled.

AIDL inventory retains methods/constants, structured fields and enumerants, raw initializers and unsupported syntax. Declaration order is not a verified wire tag. Imports/runtime types, generated Binder transactions and codecs remain separate unknowns. SDK index type presence is known even when class/interface/annotation kind or field types are unknown. Source visibility alone is not public SDK membership.

Signature reconciliation is a no-drop source-line guard, not independent grammar correctness proof. A mutant extractor dropping a fixture method fails publication. Targeted golden fixtures cover grammar behavior; the official current SDK index is separate evidence. Unknowns and acquisition exclusions remain visible with reasons.

## Reproduce

Use Python satisfying requirements-extraction.txt; this task reused an existing runtime. From the bundle root:

```sh
python3 -m unittest discover -s catalog/tests -v
python3 catalog/tooling/build_catalog.py source-inventory.json NEW_OUTPUT
python3 catalog/tooling/validate_catalog.py NEW_OUTPUT source-inventory.json EXTERNAL_REPORT.json
```

Output must be empty. Catalog metadata declares registry/version/coverage and per-source applicability. Publication follows schema/AST/integrity/FK gates and completed exports; failed staging and failure receipts remain preserved. Reports and permission exports use new paths outside immutable releases. Input/source ordering and hashes are deterministic; timestamps vary. Current execution evidence is identified by the latest progress/ledger. Frozen review inputs and complete visible JSONL receipts are retained under reviews. Fixtures do not establish Android runtime behavior or global completeness.
