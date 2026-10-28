# Sources bounded first increment

This is an execution ledger, not a completion claim. Parent lineage is MCP session `Codex-20261028T005928Z-plugin-session`, request `req-20261028T011209Z-turn-1f08`. Own implementation-turn linkage is session `Codex-20261028T010322Z-plugin-session`, request `req-20261028T013208Z-turn-6f2d`.

Current effective MCP requirements were read live through installed plugin1.121.0 at query request `req-20261028T013837Z-2153`: FR-BUILD-004, TR-BUILD-TOOL-004, TEST-BUILD-004 map in layer-1 and remain pending. The FR's authoritative AC1 covers prior fixtures/boundaries/unknown/source-version semantics; AC2 covers acquisition/cache pins; AC3 requires correct mocks/negative controls before each production slice, then identical assertions against real services.

The first increment addresses AIDL lexical/structural and signature extraction within S-AC015 and lexical annotation-scope subset of S-AC020. Tests live only under `nuke-app/Tests/Sources`, namespace `AndroidMcp.BuildTool.Tests.Sources`. No Modules/Sources production extractor exists yet. No shared contracts/registry/project files were edited by this helper.

At2026-10-27T20:37:52, read-only parsing with the existing PowerShell-shipped Roslyn assemblies reported0syntax errors for both frozen files:

- SourceExtractionContractCases.cs SHA256 `0d9c066971931038f3c542d82730d4abab8568d24df15ef0dc9881cd2880198d`.
- SourceExtractionMockGateTests.cs SHA256 `09ce40a712305cb7fde266e69d3705ec323e45f7b620a3d4de68f279d882e885`.

Earlier shared compilation caught an extra closing parenthesis in my SIG-04 fixture. It was corrected before the parse receipt above. An attempted SDK-Roslyn assembly load in PowerShell failed due already-loaded assembly version mismatch; using PowerShell's matching bundled assemblies succeeded. This was parse-only inspection, not a build or mock-gate pass. Root owns the next shared NUKE run and immutable TRX; helpers must not run competing builds or edit source tests during that run.

Fixture denominator:24 source documents,20 AIDL and4 signature. Expected data is manually authored from existing legacy fixture inputs/assertions read as text; no Python baseline was run. Correct scripted ISourceExtractor providers exercise the full reusable assertions. Each asserted property gets individual missing-property and wrong-value mutants; additional negative controls cover record omission/duplication/order, structural status, issue membership/reason/range, input mutation and provider failure. Gate traits are Mock, TEST-BUILD-004, FR-BUILD-004. The exact discovered/executed test denominator and TRX remain pending root's run.

Scope includes overloaded/generic signature members, constructor identity, parameter versus callable annotations, quoted comment markers, signature owner reset, unknown signature retention; AIDL directions/declared method IDs/unknown transaction codes, field annotations/arrays/constants, enum shifts, union ordinals without tags, dotted/external declarations, quoted decoy bodies, root/nested declaration order/parent/depth, repeated physical lines, comment-safe statement reconciliation, inherited versus explicit oneway, nested unsupported statements, malformed bodies and declaration-only status. No wire codecs or runtime authority is inferred.

Current shared contract suffices for record/issue/status tests. A future parent-approved summary seam may be needed for independent lexical reconciliation facts and raw full-schema metadata rather than hiding these in copied module-specific fields. Public source issues use legacy category spellings `aidl-semantic-gap` and `aidl-reconciliation`; `signature-unsupported` is an explicit port issue code. No shared contract change has been made.

Pending first-increment gates: compile test harness; execute correct-mock and every negative-control case through root-owned NUKE Validate/MockGate; persist immutable TRX and full count with Failed0/Skipped0; root acknowledgment of that receipt; only then production extraction. No red unit-suite substitute is accepted.

## Acknowledged mock gate and subsequent production binding

Root acknowledged the immutable gate `remediation/nuke-planning/catalog/evidence/entry-mock-gate.trx`:8706passed/0failed/0notExecuted. Helper independently verified its SHA256 `b602552cc38bfb035d036b0d0b9f2415808549295f70428241dae1be9d460e25` and rechecked both frozen source fixture/mock file hashes above before production edits. Root authorized real source extractors only after this acknowledgment. This is a passing mock gate, not evidence of real extraction parity.

After that authorization, the helper authored `Modules/Sources/SourceSyntax.cs`, `AidlSourceExtractor.cs`, and `SignatureSourceExtractor.cs`, plus `Tests/Sources/SourceExtractionRealTests.cs`. SourceSyntax is the single shared annotation/comment/type/delimiter/callable lexical utility for both formats. Providers receive an injected byte-content-hash delegate, avoiding a second production hashing implementation while root owns the shared hash service. No shared contracts or registry were edited. The real test class binds all24cases to the original `SourceExtractionContractCases.AssertContract` without modifying either frozen fixture/assertion or mock file.

Read-only Roslyn parsing at2026-10-27T20:47:30 reported0syntax errors across all6source test/production files. This is not semantic compilation or a real gate. New file hashes at that receipt:

- AidlSourceExtractor.cs `cab3403f63b7111e03af8dfbba9a6dca713b8d32f451332bbd043e2ca28bf8e0`.
- SignatureSourceExtractor.cs `c4a2cd247613c3038e2d8c04d41ab6e9f62570030e0d28627c0b699f8cf28a01`.
- SourceSyntax.cs `9c7cac1707949a6773d4bd239ca4bd217cafa1a3871206f60b0d38a294a5c07b`.
- SourceExtractionRealTests.cs `a2bf01503957fd3059daba069dc3b3792f425d5b330f8a7cdcdc163a79717ad5`.

Current freeze: root owns the next compile and Real gate; no competing helper build. Source real providers have not been claimed passing. Subsequent increments remain mock-first. Confirmed DRY boundary: Sources owns pure callback/functional/interface applicability producers exactly once; Catalog owns source metadata validation/attach/CRUD/store and consumes those producer seams. Shared seam additions require root approval.

Remaining Sources work is pending, never implied by this bounded gate: S-AC001 registry binding (root-owned); S-AC002-013 recipes/path/acquisition/archives/ranges/metadata/seed/resume/cache/partial receipts; S-AC014 Java; S-AC016 HIDL; S-AC017 SDK XML; S-AC018 annotations/JVM linking; S-AC019 manifest; remaining S-AC020 permission semantics/HTML/scoped references; S-AC021-022 Soong/interface/hash constraints; S-AC023 APEX; S-AC024-025 native ABI/descriptors; S-AC026 source metadata/callback/taxonomy; S-AC027 profile integration; S-AC028 complete offline integration; S-AC029 native Java interop. Each requires its own prior green mock gate and unchanged real-provider assertions. Acquisition/native source fetch/build/install/device/deployment actions have not occurred.
