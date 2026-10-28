# NUKE migration progress

**INCOMPLETE: checkpoint002, draft PR for progress tracking.** One C# NUKE app now includes the initial shared registry/planner, canonical JSON/hashing, immutable stores/process services, native reference components, and mock/real bindings. Normal validation is local-only. No GitHub Actions are configured or planned. Full legacy parity and production workflow completion remain pending.

## Verified checkpoint

The copied snapshot passed pinned .NET SDK 10.0.403 locked restore, host build and the full currently implemented local C# suite: **16,800 passed, zero failed or unexecuted**. See [publication receipt](validation/nuke-publication-checkpoint.json) for exact TRX identity, scope and warnings. The source-owner gate passed the same count before sealing. Publication preserved the sealed C# sources/tests after the post-copy gate.

The root registry has eight initial definitions across Sources, Catalog, Design, Validate, Review and Publish. Only Validate/MockGate and Validate/Tests currently have execution adapters. A valid plan for another operation does not mean it can execute.

Native unit components cover source AIDL/signature and SDK XML, documentation projection/module checks, canonical JSON and reference catalog/policy behavior, shared runtime behavior, publication Check/Stage and pure rehydration planning, and review unit services with fake providers. Publication has 122 mock tests and 61 native cases (Check17, Stage20, Rehydration24). These counts prove the named cases, not complete old-producer parity or a deployed Android bridge.

## Remaining work

- Complete registry/CLI adapters and shared runtime composition, including cooperating leases and provider integration.
- Finish acquisition, remaining source formats and clean-path end-to-end rehydration.
- Finish catalog/storage/resolution/metadata/wire integration and complete parity evidence.
- Finish architecture rendering, browser QA, factoring, packaging and thin Gradle delegates.
- Finish registry-derived help, full legacy per-file disposition and caller/documentation migration.
- Resolve the known nonfatal xUnit1013 public-helper warning; maintain dependency audit visibility.
- Verify lean allowlist enforcement and complete reproducibility/cross-environment evidence through native targets.

Strict DRY applies: one authoritative typed registry, contracts/configuration and shared services; functional categories are targets and typed operation parameters select tasks. Android server/UI remain Kotlin. Gradle remains orchestration. Never execute Python or custom Node, directly or indirectly.

## Legacy retention and review order

Legacy Python stays dormant as a migration reference until each replacement or retirement has verified behavior and all callers are updated. Older documents and scripts are historical migration inputs; do not execute them. Full source corpora, caches, raw review streams, historical database copies and generated build outputs remain outside the lean publication tree.

Complete migration and normal local build/test/parity verification, then mark the draft PR ready. **Do not run HV before marking ready.** Review and remediation happen in the PR afterward. No 98/98, independent acceptance, final migration completion or merge authorization is claimed.

## Navigation

- [Sealed checkpoint scope](validation/nuke-migration-checkpoint.json)
- [Latest local publication evidence](validation/nuke-publication-checkpoint.json)
- [Native bootstrap usage](remediation/nuke-app/README.md) (initial checkpoint notes; latest gate/status are authoritative here and in the receipt)
- [Publication input mapping](remediation/nuke-planning/publication/INPUT-MAPPING.md)
- [Source parity plan](remediation/nuke-planning/source/SOURCE-PARITY-PLAN.md)
- [Catalog parity plan](remediation/nuke-planning/catalog/PARITY-PLAN.md)
- [Documentation port plan](remediation/nuke-planning/docs/design-tooling-port.md)
