# Producer handoff to canonical schema/reference owner

This is a proposed field mapping for integration, not a new canonical contract.
Source code/data remain in the source workspace. The schema owner chooses the
canonical names/versions and hands back the tested mapping. Do not mutate frozen
release011 or treat these postfreeze DTOs as present in that release.

## Common source/provenance keys

Use the exact tuple `repository`, `revision`, `path`, `sha256` to join source
evidence to imported `source_id`. A source ID includes processing provenance and
changes on a corrected build; never reuse release011 IDs in a later registry.
Derived payloads must be hash-bound frozen inputs, with producer code hashes,
source/metadata/tree-membership witnesses and input reconciliation retained.
Untrusted client JSON containing `verified:true` is not a producer proof.

The proposed imported `Entry.extensions` keys are `source_interface_evidence`,
`source_callback_evidence`, and `source_functional_evidence`, each with a versioned
contract marker and occurrence-level source identity. They must remain distinct
from derived `device_resolution_variants`, codec/wire proofs and authorization.
Local metadata overrides must not forge positive imported availability facts.

## Interface/module evidence

The implemented `interface_applicability_evidence.produce` DTO has contract
`source-interface-evidence/1`, `source`, `declared_module`,
`interface_candidates`, `module_candidates`, `applicability`, `unknown_reasons`,
and explicit false runtime/OS/client authority flags. The actual 21,172-source
payload and24fixtures are in `interface-module-producer-v1`.

`declared_module` means a Soong build-domain name plus kind/source, frozen dump
version literal and declared versions/imports. `module_candidates` are
`apex_available` selectors; special selectors and Soong names must not become
installed package IDs. The APEX v2 payload additionally supplies manifest name,
raw/normalized source version code, raw comments/properties, and114 explicit
build-to-manifest links. Placeholder version0 is not an installed-version range.

`aidl_source_constraint.exact_constraint` and
`hidl_source_constraint.exact_constraint` produce a conservative `clause`:

```json
{"interface":{"descriptor":"source-proven descriptor","minimum_version":null,"accepted_hashes":["source-proven interface hash"]}}
```

The envelope also contains `source_evidence`, exact hash/selection proof and
false runtime/wire/authority flags. Map all proof identities to imported source
IDs and the existing DeviceResolution2.1 `Rule.evidence_ids`; do not copy the
envelope as a Rule. The existing `Rule.alternatives` may contain the clause only
after input/proof validation. Missing/unsupported proof remains `unknown_reason`.
Exact1199AIDL/592HIDL predicates are measured; their builder-to-registry mapping
and required type/dependency availability still need an end-to-end fixture.
The current AIDL source-snapshot/type lookup must not merge incompatible frozen
dump versions or imported versions merely because descriptor names match.

## Callback evidence

The current producer is `callback_source_facts.py`;13fixtures pass. Use the v2
baseline `callback-audit-release011-v2`, not v1 reply-mode fields. AIDL synchronous
replies require explicit and inherited oneway both known false. HIDL has its own
method flag handling. Missing information stays unknown.

Parameter candidate fields are `candidate_id`, `binding_id`, zero-based
`parameter_index`, nullable `type_id`, `type_spelling`, `parameter_name`,
`direction`, annotations, `owner`, `language`, `role`, `confidence`, evidence
reasons, permission AST, provenance, applicability and unresolved obligations.
`role` is `registration-candidate`, `unregistration-candidate` or
`callback-parameter-candidate`; it is never a guaranteed side effect.

Endpoint method evidence retains canonical signature, parameter/result type
references, HIDL results, explicit/inherited oneway, source language/reply mode,
permissions, source provenance and version evidence. Pair edges retain candidate
IDs and their heuristic basis; `lifecycle_pair_proven` is false. Names and Binder
interface parameter types alone do not prove registration, callback direction,
cleanup, executor/threading, cancellation, lifetime, ordering or backpressure.
The schema owner must define the streaming/subscription protocol and admission
requirements before these candidates can drive setup or automatic cleanup.

## Functional group evidence

`functional_source_facts.taxonomy` returns an owner Entry ID, explicit Android
SystemService annotation facts (literal/symbolic selectors kept separate),
source/import qualification confidence, and a searchable package/owner namespace
axis. It also states unclassified/searchable status and separate local overrides.
The011baseline scans62,808types, with204 explicit source annotation anchors.

Group IDs, descriptions, many-to-many member roles, small atomic size, dependency
closure and job-search ranking are canonical consumer policy. A package file or
namespace is not an automatic functional group. Unknown/unclassified inventory
must remain searchable, and descriptions absent from signatures remain unknown.

## Permission references and native metadata

`PermissionReferenceEvidence.schema.json`1.1 is a separate supplemental source
schema, not a replacement for canonical PermissionSummary1.1. It retains the two
reference-only names and12references, pinned to011registry ID. Canonical schema
integration must preserve their unknown definitions/protection/OS/client status;
the combined known-plus-referenced inventory has1723names.

Native ABI source facts are additional postfreeze evidence:48 acquired JSON dumps,
39,435records and1944 structural function-pointer parameter candidates. IDs/type
references remain source-file scoped. Architecture/library/version labels are
source context; no current Android bounds, Binder transactions, codecs,
registration lifecycle or runtime authority are inferred. Existing native macro
families retain conditional/computed forms; they are not shipping-service proof.

All outputs are UTF-8 JSON/JSONL/SQLite and independent of the Kotlin server/UI.
Python remains offline tooling. This handoff implements no Android, HTTP or MCP
runtime service, callback stream or tool-group activation.
