# Original receipt collector and actual local caller scope

Original remediation/audit_receipts.py SHA2563b69d627376276984876fc0125be6e541f0365d8a29f2e00ef6cd005e0ef4864 is a module body plus a SHA lambda, not a set of named functions. The collector was missing before this increment. Existing Launch/replay supports the current native call schema; it did not implement this historical batch collector.

ReviewReceiptCollector now consumes independently pinned original request/manifest/frozen-input/visible/completion/derived-report bytes through IPinnedArtifactReader and the existing canonical/hash/artifact/privacy authorities. Its local CollectRequestAsync caller admits a strict pinned ReviewReceiptCollectRequest. The SAME27 packets run through memory and actual FileArtifactStore, without process/provider/MCP/audit boundaries. This is real compiled C# caller behavior with test-owned files, not an actual registered NUKE CLI or live historical receipt scan.

Normally proved original body responsibilities:

- Read only the first request.jsonl line, hash exact UTF8 prompt, independently check input-manifest bytes and every declared frozen file (file key, or path only when file is absent). Duplicate declared input rows remain counted/read as the original loop does.
- Preserve exact batch directory ordering. Validate every selected round before writing the aggregate output; a later round failure produces no partial audit artifact.
- Parse visible JSONL and reject hidden reasoning through the existing assessor privacy authority. Retain all inputs unchanged. Missing or malformed selected artifacts fail explicitly, without a fallback read or provider invocation.
- Optional completion absence means unfinished with null exit code; an empty existing completion object means finished with null exit code and no response-hash requirement, matching the original truthiness branch. Nonempty completion checks exact visible-response bytes and records the exit code even when nonzero.
- Select the first init event for recorded model/tools metadata. Preserve requested model/effort as historical recorded values; none of these labels verifies a current reviewer capability or changes the registered requested model/effort.
- Exact report priority is review-report.json, reassembled, parsed, repaired. The first present report wins even if report_complete is false or the later one is true. Its complete flag is a local derived-report observation, never accepted review status.

The three extra caller packets prove independently pinned outer-request hash, unknown-field rejection and private-path admission before reading. Core services and existing full Prepare contracts remain unchanged. No new local-only Prepare result/registry is introduced.

Remaining caller/compatibility responsibilities, not hidden as missing proof:

- The typed local request explicitly selects round descriptors and input/report pins. The original module's implicit pathlib glob of every round-* directory under its project-local reviews root has no native discovery/registry wrapper here. No claim is made that a caller-selected list establishes the complete filesystem denominator or current head scope. Root must bind that exact project-local discovery using an admitted directory authority before whole original module/CLI retirement.
- The existing immutable artifact store preserves conflicting originals instead of overwriting a prior fixed evidence/review-receipt-audit.json as Python write_text did. A new selected output/generation is required for changed audit content. This follows the current retention contract but is an explicit caller disposition difference.
- Current IClock supplies the standing local-time contract rather than the original fixed UTC call. Typed ReviewException/pinned-reader errors replace Python assertion/JSON/file exceptions. Private paths and all hidden block variants recognized by the current assessor are rejected; the original only checked direct thinking/redacted_thinking blocks. These stronger typed/privacy boundaries require explicit compatibility disposition, not incidental Python-object emulation.
- Original publication-plan/audit_review_receipts.py SHA2566c9b88241c5742abc7c41f380f57d59844bfc9687db2f67faf99ac0fac71f89e remains separate: its recursive privacy-category walk, changed-during-scan test, active-round exclusion, full stable-seal hash and publication-copy policy are not implemented by this collector.
- Actual current-head full Prepare CLI, genuine required-plugin prerequisites, reviewer-owned audit/runtime/cumulative proof and provider/HV verification remain outside this local collector. Denied operations are not retried. PREPARE-PRODUCTION-BOUNDARY-018.md pins the concrete production adapter composition and fail-closed tests Root must reconcile.

No canonical authority inventory or closure state is edited. Additional whole-original-file, named-original-function and published198 closure contribution is0; existing coverage_signature normal proof remains unchanged. These normal contracts provide concrete reusable implementation and evidence for Root's later scoped disposition decision.
