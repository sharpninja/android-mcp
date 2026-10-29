# Local original receipt audit discovery and compiled caller plan

Approved private scope only. Review/Assess performs original local receipt integrity audit; Review/Prepare remains unchanged. No provider, reviewer, MCP, readiness/audit fallback or HV is reachable.

Reuse Root's exact shared Core ArtifactWorkspaceContracts.cs SHA256cbbe571ae560029b402bace04d0321c58f85169a4b3a59e246853b182819d58a and NativeArtifactWorkspace.cs SHA2562cdd4ac4b2cde423e852b3eaa78f620f57fb7f7621947eaae57b9f7b4a6064e7. The private015 clone lacks these already-implemented authorities. Import them unchanged by hash and exclude them from new implementation claims. No Directory/GetFiles/glob/path/hash/IO engine is added to Review.

Discovery scope and testable order:

1. Select explicit project and separate output roots through existing AdmitWorkflowRoot/ArtifactPaths. Use the shared native tree at fixed project-relative reviews, nonrecursive only. An absent reviews root behaves like the original empty glob: produce an empty-round audit. Names beginning round- are selected in ordinal order. A matching regular file or link/nonregular round fails before content reads. Unrelated child directories are never descended.
2. Enumerate only each selected direct round directory, nonrecursive. Fixed request.jsonl, input-manifest.json and response.visible.jsonl are required regular files. Completion is optional. The four derived report names use existing exact priority; only the first present report is read. Unknown files, nested directories and private markers are not read. A blocked required filename is reported exactly; no broad fallback scan follows.
3. Observe bytes through the shared store to make actual hash/length pins. Reuse collector request/manifest validation helpers for prompt-first, manifest and frozen inputs. Shared ArtifactPaths/public-path admission precedes reads. Collector consumes the observed pins again, detecting changed bytes without mutating inputs. Existing tree authority detects directory changes.
4. Run the existing collector, not a second parser. Preserve original rounds/metadata/priority shape. Current immutable stores require a fresh output root for changed audits; conflicting originals remain retained. IClock supplies standing local time. Typed error/privacy/link boundaries are explicit compatibility replacements. Output never establishes accepted review or actual model capability.

Private compiled route: append ReviewReceiptAudit executor; register existing Review/Assess with required project-root/output-root and no optional parameters; add one dispatch arm and thin Build.ReviewAssess.cs composition. Preserve all existing arms and full Prepare. No new operation/parameter/model. Stage a minimal proposal for Root's current Snapshot registry, not an old full-registry replacement.

Mock-first packets cover empty scope, sorted rounds, ignored nested/unrelated/private files, first report priority, missing files, matching-file/link rejection, selected-file link rejection, malformed request, manifest escape/private path, changed input, no partial output, output conflicts and registry/CLI parameter failures. SAME packets use native temp trees/shared store and ordinary compiled `--target Review --operation Assess`. All files are synthetic/test-owned; no actual receipt or marker read. Existing27 collector/file cases remain regressions.

Normal caller proof is not current-project corpus verification, official audit authority, full Prepare completion, hostile acceptance or deletion permission. No canonical code or inventory edit is authorized.
