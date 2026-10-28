# Catalog acceptance and test binding

This is allocation evidence, not a completion claim. Requirement group FR-BUILD-004 maps through TR-BUILD-TOOL-004 to TEST-BUILD-004. FR-BUILD-001/ac-3 covers retained originals and distinct native fingerprints; FR-BUILD-003 covers the shared engine and canonical contract reuse. Clean fixture reconstruction later contributes to FR-BUILD-007/ac-1 and TEST-BUILD-007.

## Increment 1: entry state and policy algebra

- Stable canonical signature SHA256: CAT-01. Pin `java:a.A#x()` to `5d65b8acc70bb725dd3e6966688038b149e6125fa8996dd37e3f9adafdaa7c4c` independently through the platform SHA256 implementation.
- Duplicate creation: CAT-02 and CAT-11, both collision orders retained.
- Strong If-Match error ordering: CAT-03 (428), CAT-04 and CAT-10 (400), CAT-05 (404), CAT-07 (412).
- Local tombstone, current-tag idempotence and restore: CAT-06. Each response asserts key, ETag, state, revision, history count, registry generation, overlay generation and canonical document.
- Rejected import atomicity: CAT-08 re-reads state after422. Expected registry and overlay generations must remain unchanged.
- Overlay cursor invalidation: CAT-09 captures before update and checks410 afterward.
- Imported suppression: CAT-12. Local import collision: CAT-13, including prohibited delete409. Explicit rebase: CAT-14 (unreviewed409, then exact selected base accepted). Removed base without overlay: CAT-15. Removed suppressed base with retain-local: CAT-16.
- Canonical policy algebra: 36 cross-product cases over binding/entry effects (deny, ask, allow), authority, emergency. Additional13 cases cover bool/string/double/negative/over-limit item inputs, empty plan, missing entry/rule, stale binding/entry schema, target, entry bound, unknown effect. The binding schema and entry schema are distinct inputs. OS authority is evaluated only after policy bounds accept the candidate.
- Source-pinned origins: `test_contract_models.py` methods recorded in PARITY-PLAN.md; `contract_reference.py` and `policy_reference.py` define the offline reference, not HTTP or Android execution.

Mock evidence is from `CatalogMockGateTests` and `PolicyMockGateTests`, trait `Gate=Mock`. Every response field is corrupted separately and must cause the same assertion function to reject. Every other decision in the closed vocabulary is supplied separately and rejected. Scripted providers assert receipt of the exact input object, and catalog providers assert all calls consumed. Real test classes later use the same `CatalogCases.AssertCase` and `PolicyCases.AssertCase`.

Initial receipt `evidence/entry-mock-gate.json` covers the first10 catalog scenarios only,30 test instances. The expanded16 catalog scenarios plus49 policy cases need their own subsequent receipt before native implementation. No old Python test execution or old receipt counts as native proof.

## Remaining increments

The complete original inventory in `original-test-inventory.json` has23 files and204 test methods, including cross-cutting parser and historical regression cases. It intentionally records unallocated assertions as source inventory rather than claiming they are all catalog-only. Before each next increment, expand its concrete mock fixtures with the original inputs, output fields, exception, rollback and interaction assertions. In particular:

- Resolver: all16 methods in `test_device_resolution.py`, plus5 effective-inventory methods. Reasons must be compared exactly and unknowns remain represented. View lifecycle includes scope, complete membership, stale epoch/generation/catalog pins and input-copy isolation.
- Storage: all8 `test_device_view_storage.py` methods and relevant schema regression cases. SQL enforcement is tested through real SQLite only after schema-operation mocks discriminate expected outcomes. Validate normalized fields against stored JSON and append-only invariants.
- Metadata:19 `test_source_metadata_crud.py` methods and17 `test_source_metadata_contract.py` methods. Source producer heuristics and their classification/evidence fixtures must not be replaced by runtime authority. The owner capability comes from the trusted caller seam, not JSON.
- Wire proof: both `test_postfreeze12_wire_evidence.py` methods, including unchanged immutable identity, verified-to-unsupported revocation, stale revision and malformed verified codec rollback. Current proof selection and semantic client grants remain independent.
- Remaining regression file methods are allocated to source/catalog/registry owners by inspecting their assertions, not by filename or matching test counts.

## Increment 2 mock allocation: resolver state and type graph

`ResolverContractCases.cs` has27 scenarios and108 mock test instances. These port the pure resolver assertions for major/minor/removal, extension alternatives, independent module/OEM/build axes, reachable versus absent/denied/unreachable interfaces, compatible and ambiguous variants, missing/wrong/unavailable/unknown type dependencies, recursive components, and preserved unknown source lifecycle. Additional cases distinguish unverified device identity, nonexecutable inventory nodes, malformed source intervals and catalog exposure state from existence.

Each row assertion includes exact reason vocabulary, selected contract, complete dependency list, pinned effective document, exposure state/revision, graph digest and closure3 schema identity. Golden byte/hash vectors are independent literal fixtures in `evidence/resolver-golden-hash-vectors.json`; no production resolver generated them. Header pins and complete ordered membership are asserted. Negative controls corrupt each header and row field individually and verify input mutation rejection.

The OriginalTest labels identify relevant original assertions. They do not claim that resolver projection alone ports the complete storage or policy integration method. Atomic activation, invalidation, generation/epoch/scope CAS, bounded status and current-view ETags, SQL enforcement, effective inventory capture and known-route policy composition remain in their later increments. No production resolver exists before this mock gate.

## Additional canonical identity compatibility gate

Historical `callback_source_facts.py::registration_candidates` uses sorted compact JSON with ASCII escaping for candidate identity, while normal contract hashing uses literal UTF8. Root approved `JsonSerializationProfile` and additive `IProfiledCanonicalJson`. Profile mocks reuse the same canonical assertion seam for both profiles and literal legacy expectations, including supplementary surrogate pairs, scalar key sorting before escaping, no normalization, DEL, line separators and callback identity fields. Wrong profile routing, bytes, hashes, error codes, omitted exceptions and unknown enum values are rejected inside passing mock tests. Production gains one escaping branch only after that gate; no second formatter or hash implementation is authorized.

All verification is local NUKE. No GitHub Actions are created. Hostile review is deferred until the migration PR is marked ready, per current operator direction.
