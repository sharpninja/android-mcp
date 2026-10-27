# Interface/module producer handoff v1

Pinned source facts, produced from 1,301 metadata paths (1,300 distinct objects).
24 fixtures pass before the full corpus pass. The repository-root aidl_api hash
linkage defect is fixed in v2; immutable v1 evidence remains available separately.

Consume source-interface-evidence.jsonl by exact repository/revision/path/SHA256.
The declared_module object is in the Soong build namespace. module_candidates
contain literal apex_available selectors, including special selectors; they are
NOT installed package IDs or package version intervals. Never map AIDL frozen
version N to an APEX version or HIDL @1.0 to Android API 1.0.

This DTO exposes published hash candidates and source-level version/import links.
It deliberately emits unknown applicability until a consumer has proven the exact
interface descriptor, compiler hash selection/source-schema equivalence, and any
required type dependencies. All AIDL .hash lines remain ordered; multiple lines
are not silently collapsed. HIDL current.txt lines remain distinct compatibility
facts. A useful next proof is exact .hal SHA256 equality with a matching published
identifier, then a descriptor checked against the parsed interface declaration.
Reference: https://source.android.com/docs/core/architecture/hidl/hashing

Required builder integration: retain this DTO in hashed Entry extensions with
all referenced evidence source IDs; derive DeviceResolution interface clauses only
after the obligations above are satisfied. Keep unresolved applicability with
named unknown reasons. A declaration/interface match is not runtime reachability,
wire-proof admission, OS authority, or a client grant. OEM/build-fingerprint and
installed module versions remain unknown without separately evidenced facts.

No canonical contract files, active release011, installed settings or devices
were changed. This is a local handoff, not an uploaded or globally complete corpus.
