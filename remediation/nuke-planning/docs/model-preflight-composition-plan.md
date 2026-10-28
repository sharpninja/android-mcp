# Model preflight source routing

This is staged contract work, not an implemented generation prerequisite. Architecture002 remains the bounded 6-input render proof recorded separately.

The existing required architecture sources are workspace DSL, code Markdown, module contract, render skin, toolchain lock and renderer lock. Add canonical UI blueprint, design Markdown and `contracts/source-pins.json`, then the three required snapshot copies identified by that pin file. There are 12 required project inputs. The three canonical catalog originals are optional observations whose absence must be explicit.

The source planner validates the pin file through the shared StrictJsonInspector. Admit only these exact mappings:

- OpenApi: snapshot `contracts/openapi.json`; pinned canonical expression `../catalog/contracts/openapi.json`; catalog-store path `contracts/openapi.json`.
- ApiContract: snapshot `contracts/API-CONTRACT.md`; pinned canonical expression `../catalog/API-CONTRACT.md`; catalog-store path `API-CONTRACT.md`.
- SourceMetadataContract: snapshot `contracts/SOURCE-METADATA-CONTRACT.md`; pinned canonical expression `../catalog/SOURCE-METADATA-CONTRACT.md`; catalog-store path `SOURCE-METADATA-CONTRACT.md`.

The parent-relative strings are pin values to compare exactly, never paths passed into a project-root store. The host binds the project and canonical-catalog stores separately and routes only descriptors with the verified role and artifact ID. Canonical observations use relative paths within the already-bound catalog root. Unknown IDs, role substitutions, extra/missing/duplicate pin entries, invalid hashes, path substitutions, duplicate JSON keys and non-HTTPS concerns from unrelated profiles do not expand this scope. No prefix rule, arbitrary traversal or extension to ArtifactPaths is permitted.

Before tools or output writes, the read-only collection must produce every required source and explicit present/absent status for each canonical source. The shared receipt/hash authority checks observed bytes against the expected source pins. Present canonical content must match the same pin as its snapshot. Missing optional canonical content remains null; missing required snapshots fail. No missing source is silently skipped.

The model preflight then uses DocumentationModelReader once, ModuleGraphValidator's existing declared-class metadata, and ModelContractValidator's original clauses. A selected ModelContracts stage must obtain class metadata without importing a ModuleGraph failure prerequisite; the authoritative DesignStagePlanner determines whether ModuleGraph validation itself is selected. Avoid a second class regex, control table, source parser, hash algorithm or read/write loop.

The shared immutable-generation runtime remains the only final commit protocol. The host-owned routed source collector must retain source identities/absence observations for preflight and commit-time consistency rather than letting a Docs adapter access arbitrary files. Final manifest input records must disclose which optional canonical observations were absent. A missing or changed source after preparation must fail before final manifest commit.

Mock gates must cover all 12 required source omissions, all three optional absences and presences, each hash mismatch, each pin-path/ID/role substitution, malformed and duplicate JSON, missing/extra/duplicate entries, canonical absence becoming present or vice versa, source changes, cancellation, zero tool/output effects on preflight failure, and reuse of the same parsed model/declared-class authority. The same assertions later bind the real planner/collector/composition. The exact host collector interface and manifest representation require root ownership agreement before production integration.
