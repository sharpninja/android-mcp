# NUKE migration progress

**INCOMPLETE — draft PR for progress tracking.** This checkpoint includes the initial NUKE host, typed contracts, mock fixtures, initial native implementations and migration plans. It disables legacy Python CI and does not claim production parity.

## Required implementation

Replace all project Python tooling with one NUKE Build C# monolith. Functional categories become targets; a typed operation parameter selects each task. Use one authoritative registry, contracts and shared implementation throughout. Keep Android server/UI code in Kotlin and Gradle as orchestration. Never execute legacy Python, directly or indirectly.

The included host exposes Validate --operation MockGate. Native AIDL/signature extractors and a pure documentation projector are present with real test bindings, but this checkpoint runs the mock gate only; production behavior and full parity remain unverified. The category/operation enums and interfaces are scaffolding for the pending registry/planner. Mock assertions are not evidence of production parity.

## Progress checklist

- [x] Create a branch checkpoint and progress-tracking draft PR.
- [x] Remove Python CI execution; add locked native .NET build and NUKE mock checks.
- [x] Preserve upstream main changes, including FUNDING.
- [ ] Publish the shared NUKE category/operation registry and configuration.
- [ ] Port acquisition, rehydration, source parsing and extraction.
- [ ] Port catalog, storage, permissions and resolution operations.
- [ ] Port design generation, rendering, visual QA and packaging.
- [ ] Port review/publication orchestration with the required review-order safeguard.
- [ ] Map every legacy Python file to a verified replacement or justified retirement.
- [ ] Update all callers, Gradle/CI integration and usage documentation.
- [ ] Verify real C# behavior and parity with representative fixtures and native test receipts.
- [ ] Validate the lean deliverable allowlist, reproducibility and cross-environment usage.
- [ ] Complete migration and normal build/test/parity verification, then mark this PR ready for review.
- [ ] Perform review and remediation in the PR after it is ready; do not run HV before marking ready.

## Legacy retention and validation limits

Legacy Python files remain tracked as dormant migration references. They are not approved execution paths. Existing Python commands in older documents and scripts are historical instructions pending replacement; do not run them. Removal requires a per-file replacement/retirement mapping, verified behavior and updated callers.

The checkpoint workflow performs a native locked restore, C# host build and NUKE mock gate. A green mock gate does not mean production generation, application behavior, or migration parity is complete. No new HV, acceptance score, 98/98 result or merge authorization is claimed. Full source datasets, caches, historical snapshots and large review archives remain outside the lean publication scope.

## Checkpoint evidence

See [initial sealed handoff](validation/nuke-migration-checkpoint.json), [native publication verification](validation/nuke-publication-checkpoint.json), [NUKE usage](remediation/nuke-app/README.md), and the [source](remediation/nuke-planning/source/SOURCE-PARITY-PLAN.md), [catalog](remediation/nuke-planning/catalog/PARITY-PLAN.md) and [documentation](remediation/nuke-planning/docs/design-tooling-port.md) port plans. The sealed handoff receipt is historical; the publication receipt records the fresh gate. Transitive NuGet security warnings remain to be resolved before final migration readiness.
