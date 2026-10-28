# NUKE migration progress

**INCOMPLETE: checkpoint003, draft PR for progress tracking.** One C# NUKE application hosts the tools. Validation is local-only; no GitHub Actions. Full legacy parity and end-to-end production workflow completion remain pending.

## Verified checkpoint

The copied snapshot passed locked restore with pinned .NET SDK 10.0.403, host/test builds with zero warnings/errors, and the full currently implemented C# suite: **24,517 passed, zero failed or skipped**. See [publication receipt](validation/nuke-publication-checkpoint.json) for exact scope, hashes and chronology. The prior four missing-fixture failures remain recorded there; the portable seven-row correction passed 48 focused cases before the full run, restoring all expected cases without changing assertions.

The authoritative registry currently contains 9 definitions. Its executable adapters are Validate/MockGate, Validate/Tests, Design/Assets, Sources/NativeParser. Planning another registered operation does not make it executable. Kotlin remains the Android server/UI language and Gradle remains orchestration.

Native unit services now include the shared runtime, canonical JSON/hash profiles, source formats and acquisition/tool transport, catalog policy/resolution/SQLite/metadata, documentation preparation/package services, publication Check/Stage/rehydration planning/help, and review services tested with fake provider/audit collaborators. Unit bindings, CLI adapters and live workflows are distinct evidence levels. No Android application or deployed bridge is implemented here.

## Remaining migration work

- Complete registry/CLI dispatch and runtime composition for acquisition/batch rehydration, catalog production, publication and review.
- Finish source HTML/Soong/hash production coverage and typed recipe-row projection without a second recipe parser.
- Finish actual architecture renderer admission, browser runtime/visual QA, bootstrap, validators, packaging and thin Gradle caller migration.
- Complete catalog generation/wire-proof producers, long-lived process runtime, ancestry hardening, full per-file disposition and clean-path end-to-end parity.
- Complete native deliverable-policy adapter enforcement, reproducibility and cross-environment evidence.

The [sealed component status](validation/nuke-migration-checkpoint.json) records the individual component scope. Current test counts establish only the named C# scope, not complete old-producer parity or acceptance.

## Legacy retention and review order

Legacy Python and custom Node files remain dormant migration references until each replacement or retirement and all callers have verified parity. Never execute them. Corpora, caches, raw reviewer streams, historical database copies, generated build trees and native binaries remain outside the lean tracked tree. Only six small editable pinned class-view goldens are retained as test inputs.

Strict DRY applies: one authoritative typed registry, shared contracts, recipe parser and services; functional categories are NUKE targets and parameters select operations.

Complete migration and normal local verification, then mark the draft PR ready. **Do not run HV before marking ready.** Review/remediation happen in the PR afterward. No 98/98, final migration completion or merge authorization is claimed.

## Navigation

- [Current copied-gate receipt](validation/nuke-publication-checkpoint.json)
- [Native bootstrap usage](remediation/nuke-app/README.md)
- [Publication input mapping](remediation/nuke-planning/publication/INPUT-MAPPING.md)
- [Source parity plan](remediation/nuke-planning/source/SOURCE-PARITY-PLAN.md)
- [Catalog parity plan](remediation/nuke-planning/catalog/PARITY-PLAN.md)
- [Documentation port plan](remediation/nuke-planning/docs/design-tooling-port.md)
