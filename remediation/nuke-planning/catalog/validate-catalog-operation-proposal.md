# One Validate/Catalog operation

This is a bounded caller proposal. No app implementation, registry edit or acceptance claim is made by this document.

The authoritative operation vocabulary already contains `Operation.Catalog`. `OperationRegistry` currently has no `Validate/Catalog` definition, and `Build.Dispatch` has no Catalog validation executor. The only native invocation is the direct service binding in `CatalogFullValidationRealTests`. The existing six cases passed in the 916-test gate at `artifacts/tests/20261028T1225375590281-b4c2fe0ac9e34b7ebeaff1041587f3f3/tests.trx`, SHA256 `99b68976552358bcca597625bd4105e24309e3a414f01426ef8424b98b78e5af`.

## Legacy identity and caller boundary

`remediation/catalog/tooling/validate_catalog.py`, SHA256 `d70110d5e3c24df2ce7a0c52bed2c3cdb1f25e3d6d72bb7629680392402e20ed`, contains both callers: positional release arguments dispatch to `run/_run`; `--database <db> <report>` dispatches to `run_database`. No `validate_database.py` was found in the working workspace or retained SourceTask. The published `rehydration/producers/release-011/tooling/validate_catalog.py` is a second path in the sole inventory, with the same SHA256. Equal bytes establish identity only. This proposal does not close frozen-profile callers or historical variants by basename/hash.

## Proposed public route

Use the existing `Validate` category target with `--operation Catalog`. Root owns the enum executor, definition, parameter collection and dispatch. Reuse the four existing required parameters `project-root`, `pipeline-request`, `pipeline-request-sha256`, `pipeline-request-length`. There are no optional CLI parameters and no target per legacy script or mode.

The caller-pinned request uses one native DTO with unknown-member rejection:

`CatalogValidationWorkflowFile(Mode, ReportPath, OutputRoot?, SourceManifest?, DatabasePath?)`, where `Mode` is the explicit enum `Release` or `Database` serialized by its name. Release requires OutputRoot and SourceManifest and forbids DatabasePath. Database requires DatabasePath and forbids OutputRoot and SourceManifest. Both require a nonblank ReportPath. Unknown/missing mode, unknown members and mixed or missing mode-specific fields reject before any validation/report effects. There is no inferred default mode.

Host composition reuses `InputRelative`, `AdmitWorkflowRoot`, `PinnedArtifactReader`, `FileArtifactStore`, `ContentHash`, `CanonicalJson`, and native DTO deserialization, following current workflow host conventions. ProjectRoot is the contract/schema root; request paths resolve explicitly relative to it, including explicitly authored external roots where already admitted by the shared path engine. No raw inventory self-pin, new parser, schema engine, filesystem integrity engine or artifact writer is introduced.

After admission, the thin adapter constructs the one `CatalogValidator` and selects exactly `ValidateAsync(output,inventory,report)` or `ValidateDatabaseAsync(database,report)`. It prints the resulting structured report and its StoredArtifact receipt; no report success is printed before the service returns. Exceptions retain failure exit behavior through existing `NukeExitCode`. A report retained before a schema/database validation failure is evidence of the failed validation, not a successful result. Strict create-new remains in the validator via existing `ICreateNewArtifactStore`; neither host nor reader writes a second report.

The already-proved shared AIDL lexical service may be injected directly after the corresponding explicit consumer fixtures pass, or through the single factory concern after its own gate. Missing/absent reconciliation stays unreconciled. The host must not manufacture a matched claim, add a kind map, or initialize a Java parser for these offline validation modes.

## Bounded mock and actual caller gate

Before the thin reader/adapter is implemented, prove a finite mock harness for both successful modes and each new admission assertion. Reuse the SAME six original full-validator cases and assertions for release, database-only, raw-source tamper, export tamper, report inside release, and preexisting report. Add database-mode preexisting report, wrong outer request SHA, wrong outer length, unknown mode, missing mode-specific fields, mixed mode-specific fields, and unknown member rejection. Mutation controls must discriminate every asserted report/pin/exit/nonmutation field and that the other validator mode was not invoked. No production behavior may precede this gate.

Then bind those assertions to actual monolithic CLI child calls using the existing NativeProcessRunner/ToolIdentity fixture seam and current DLL. Retain the original generated release, copied DB, request bytes and preexisting report, and compare byte hashes before and after. Positive modes must verify the actual report artifact pin, ten original entries, read-only DB, original SQL integrity/FK/schema values, Complete=false and native producer identity. Negative cases must verify a nonzero normalized exit, the expected specific error, retained originals, no unexpected report, and retained existing report bytes. A successful DB-only child must run with no export/inventory availability, proving the mode does not call the release validator.

## Exact remaining clauses after the current six native cases

The original Build composite suppression/restore/history/immutable/FK/read-only validator assertions and Round3 DB-only nonmutation assertions have normal proof. The other four cases add source SHA, export SHA, outside-release report and create-new report controls. They do not establish every validator branch or complete report-value compatibility.

Release `_run` still needs explicit discriminating fixtures for all schema-file checks; schema-failure report-before-error details; canonical identity, document digest and current ETag rejection; parameter/relation/scoped-annotation AST failures; exported/source/downloaded/expected-declaration count mismatches; matched AIDL reconciliation and its negative count/lexical branches; duplicate occurrences; nonempty FK/integrity failure; detailed signature/scoped permission/reference-name counts; and all report fields with their original type/null/ordering expectations. Existing report field construction in code alone is not fixture evidence. Missing AIDL metadata remains unreconciled.

Database `run_database` still needs explicit fixtures for storage version rejection, canonical/document/ETag negatives, invalid ASTs, multiple declaration shapes without variants, omission sample cap 20, active-entry-only omission accounting, rebuilt=true omission rejection after report persistence, absent/false/true rebuild marker distinctions, and invalid-schema/integrity/FK report-before-error paths. Current DB validation reuses a helper that checks all contract schemas while legacy database mode checks its two schemas; exact compatibility for an unrelated invalid schema remains an explicit boundary to resolve, not a proven equal behavior. Shared formatter output and native producer fingerprint are deliberately native receipts; historical Python producer identity or byte-identical legacy report encoding is not claimed.

Whole-file CLI activation remains pending until Root registers and binds this operation and the actual child gate passes. No legacy corpus/profile, HV acceptance, removal or physical deletion claim follows from these six cases.
