# NUKE migration progress

**INCOMPLETE — draft PR for progress tracking.** This checkpoint publishes status and disables legacy Python CI. It does not publish the in-progress NUKE implementation or claim production parity.

## Required implementation

Replace all project Python tooling with one NUKE Build C# monolith. Functional categories become targets; a typed operation parameter selects each task. Use one authoritative registry, contracts and shared implementation throughout. Keep Android server/UI code in Kotlin and Gradle as orchestration. Never execute legacy Python, directly or indirectly.

C# scaffolding and mock gates exist in the working implementation, but they are not included in this status-only checkpoint. Mock assertions are not evidence of production parity. Future commits will carry coherent implementation increments and their verification evidence.

## Progress checklist

- [x] Create a branch checkpoint and progress-tracking draft PR.
- [x] Replace Python CI execution with explicitly tracking-only checks.
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

The checkpoint workflow checks only the presence of this status document and records the commit. A green tracking check does not mean the migration, generation, application, tests or parity are complete. No new HV, acceptance score, 98/98 result or merge authorization is claimed. Full source datasets, caches, historical snapshots and large review archives remain outside the lean publication scope.
