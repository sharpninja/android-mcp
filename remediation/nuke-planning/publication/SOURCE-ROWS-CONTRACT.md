# Authoritative rehydration source projection

Publication owns this contract and the one recipe parser. Acquisition consumes the typed projection and must not parse a recipe or plan JSON to recover omitted fields.

Contract source: `Modules/Publication/RehydrationSourceProjectionContracts.cs`, namespace `AndroidMcp.BuildTool.Publication`. Entry point: `RehydrationPlan.SourceProjection : RehydrationSourceProjection?`. The optional property preserves construction compatibility for older fixtures; production must populate it once its mock gate and same-case real gate pass. The planner implementation has not yet been changed for this increment.

## Model and authority

- `Rows : IReadOnlyList<RehydrationSourceRow>` preserves every original selection in zero-based original order, including identical duplicates. `RowIndex` never becomes a deduplicated-file ordinal. Each row retains its logical path/kind, physical-source index, optional cache-alias index, typed context and detached immutable `JsonElement OriginalRow`.
- `OriginalRow` retains every field, unknown extension, null/absent distinction and heterogeneous release-context value. Context includes repository/revision/surface/scope/license, detached release context, selection origins and reasons. Batch receipts may serialize these values directly; they must not reparse raw recipe JSON.
- `PhysicalSources` is a first-occurrence index of unique local target paths. Each entry reuses Acquisition's `SourcePin`, lists all original selection indexes, preserves required `SnapshotRequirement` entries and carries typed prerequisite records. Content-pin/size/cache conflicts fail validation rather than silently using the last row.
- `CacheAliases` preserves each shared cache target and all source-row aliases. Complete cache evidence uses Acquisition's `HistoricalCachePin`, including cache count, gzip/producer/source/recipe pins. Missing kind/count remains an explicit prerequisite and a null executable cache pin, with its cache path and row indexes retained; it must never become invented zero-count evidence.
- `SelectedPhysicalSourceIndices` selects physical work independently of full original rows and context. A limit changes the selected work denominator, not the preserved recipe row denominator. Empty selection is valid planning and performs no effects.
- `Profile` is Acquisition's existing `AcquisitionCompatibilityProfile`, default `CanonicalRestore` in `RehydrationPlanRequest`. Generic mode requires an explicit request. Every source and archive pin must match it. Release names, missing archive hashes and missing prerequisites never switch profiles.

## Missing proof and execution boundary

Missing canonical range ETag, explicit platform, exact repository XML/archive size and missing immutable snapshot bundle are typed prerequisites. All known pins and unknown container facts are retained. Planning remains valid and effect-free while prerequisites are unresolved.

Acquisition preflights all selected prerequisites before any batch network or output write. It may reuse a pre-existing target only after exact SHA256, optional size and Git identity verification; that reuse does not prove an unknown whole archive. A required immutable snapshot remains represented even with a null bundle path and must never fall back to HTTP. Existing conflicts retain their bytes.

Six known release metadata rows lack canonical range proof. Canonical acquisition cannot fetch absent targets for them. Explicit generic mode remains bounded to allowlisted metadata and mandatory member SHA, without claiming complete container identity. No new download is authorized by this pure plan.

## Current mock increment

`Tests/Publication/RehydrationProjectionMockTests.cs` defines twelve fixture cases, each run through correct scripted responses and individual wrong-response controls: ordered aliases and full audit, gzip, limit one, limit zero, explicit generic, Gitiles encoding, bundled snapshot, exact duplicate, missing cache count, invalid profile, bad Git pin and conflicting cache count. The same cases/assertions will bind to `RehydrationPlanner` after a successful mock gate. Mock success is not production or end-to-end evidence.

The assertions inspect every original row, context/provenance, source/cache index, source/archive pin, snapshot and prerequisite, selected/full denominators and no-effect/completeness flags. Negative controls independently change values or omit required evidence; no test computes its expected projection from the production parser.

Requirement mapping remains the assigned `FR-BUILD-007`, `TR-BUILD-TOOL-007` and `TEST-BUILD-007`. A fresh effective-store query was denied before execution; no refresh, new satisfaction state or acceptance is claimed.

Status: contract/mock files frozen; root mock gate requested; no production projection parser, duplicate recipe parser, network acquisition, Python, custom Node, Actions or HV introduced.
