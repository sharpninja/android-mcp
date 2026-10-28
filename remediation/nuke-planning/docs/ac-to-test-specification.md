# Documentation port acceptance criteria and test specification

Planning specification only. DOC-AC IDs are local proposal anchors for reconciliation with the parent's MCP FR/TR/AC records. They are not persisted requirement IDs. All named cases run inside the sole NUKE app through the parent's test category/operation. The 58 source-pinned legacy case names are listed in [saved-fixture-inventory.json](saved-fixture-inventory.json), with source hashes. Preserve their inputs, assertions, boundaries, error expectations and interactions. Do not equate the case count with parity.

For every item below, the mock gate runs the same test body against deterministic correct and incorrect providers before production code is written. Negative controls are expected rejections in an overall passing harness. The real gate binds the exact same cases/assertions to the native service. Required cases never skip. A provider that bypasses an assertion or simply returns a pass boolean is invalid.

## DOC-AC-01: registry, invocation and single graph

Observable outcome: the `Design` category target dispatches documentation generator operations with singular `--operation`; the central `Validate` category owns public design/toolchain checks. One registry defines legal requests, aliases and prerequisites, produces help/docs/preflight, and rejects illegal combinations before effects. Both categories call the same validator services. Direct and legacy Gradle requests select the same graph closure. Multiple selected compatibility aliases execute shared steps once through one singular compatibility operation. No Python/Node/Marp process is launched.

Cases: `DesignOperationRegistryTests.EveryOperationHasOneBinding`, `UnknownOperationRejectedBeforeEffects`, `UnsupportedOptionsRejectedBeforeEffects`, `HelpAndAliasMetadataMatchRegistry`, `PlanHasNoCycleAndStableOrder`, `SharedPrerequisitesExecuteOnce`, `GradleAliasMatchesDirectPlan`, `CombinedGradleAliasesMatchUnion`, `ForbiddenRuntimeCannotLaunch`, `DryRunChecksInputsWithoutWriting`.

Mocks/negative controls: recording effect sink and process runner; unknown operation, duplicated alias, dependency cycle, missing pin, wrong category option, hidden Python/Node launcher, unexpected download, repeated node execution and an alias mapping to `All`. Assert zero file/process/network effects on rejection. Real evidence includes plan JSON plus recorded executable identities for every design operation.

## DOC-AC-02: canonical parsing and source pins

Observable outcome: canonical Markdown blueprint, module DAG, editable code blocks, DSL and API/schema pins are parsed once; invalid or stale inputs fail before projection and leave sources unchanged.

Cases: `DesignInputTests.ReadsExactlyOneBlueprintBlock`, `RejectsDuplicateKeysAndIds`, `RejectsUnsupportedSchema`, `RejectsMissingControlStateAndTransitionReferences`, `CopiedContractsMatchPinsAndPresentCanonicalSources`, `CodeViewNamesAreExact`, `GeneratedNavigationMarkersAreUnique`, `InputsChangedDuringRunAbortCommit`, `OutputRootOutsideWorkspaceRejected`.

Mocks/negative controls: missing/multiple blueprint fences; duplicate key/state/step; unknown closed-domain value; missing field; stale copied/canonical contract; missing or extra code view; absent/doubled navigation region; source changes after planning; symlink/path escape. Correct fixture is the pinned canonical snapshot, not generated manifest output. Fixture hash mismatches block both mock and real gates rather than refreshing expected values automatically.

## DOC-AC-03: module/model regression parity

Observable outcome: all original module and cross-artifact guards reject the same forbidden architecture and preserve current valid design contracts. These tests prove documentation consistency only.

Cases: retain all methods from `test_design_module_graph.py`, `test_design023_guardrails.py`, `tests_design018_contract.py`, `tests_design020_contract.py`, `tests_design022_contract.py`, `tests_design023_contract.py`, `test_design024_contract.py` and `test_design024_core_contract.py`. Add `PerViewImplicitEndpointRejected`, `MissingClassOwnerRejected`, `StaleComponentMappingRejected`, `DispatchAndRevocationOrderRejected`, `InvalidBearerContractRejected`, `ClosedOwnerAndLifecycleRulesPreserved`.

Mocks/negative controls: the original two-module cycle, concrete storage-to-application edge, non-domain interface, removed dispatch call, bypassed revocation gate, reordered release, missing callback/lease invalidation, placeholder states/actions, missing import-stage routes, wrong ARC enum/signature hash, wrong field spelling and stale metadata copy. Preserve all assertion tokens and route/field sets in source-pinned data-driven fixtures. A structural parser may improve detection but cannot drop the old negative fixture or relabel its failure as success.

## DOC-AC-04: complete UI and SVG projections

Observable outcome: one canonical blueprint produces all 11/88/76/6/107 entities and exact current path sets. Each of 76 state SVGs partitions the screen's controls into exact enabled/disabled sets, exposes correct health data and retains vector text. Each of 107 focused panels has the named state/control/actor/branch and preserves transitions. All 17 compatibility `ui-svg` copies match the corresponding docs SVG bytes.

Cases: retain all `test_design023_ui.py` cases and UI-specific legacy assertions. Add `UiProjectionTests.ExactArtifactSet221`, `EveryStateHasExactControlPartition`, `EveryStepMatchesPinnedOrderedTuple`, `ExternalEventIsObservation`, `HealthOverrideAndRevisionParity`, `StoppedAndOwnerChallengeFixtures`, `OverviewAndCompatibilityCopyParity`, `SvgEscapingAndWrappingPreserveText`, `DesignOutsideNavigationRegionUnchanged`, `RepeatProjectionIsByteIdentical`, `PartialProjectionIsNotAccepted`.

Mocks/negative controls: provider returns one missing/extra SVG, enabled disabled overlap, control omitted, wrong focus, main success forced to error, unresolved branch, noninteger revision, Stopped labeled Running, missing OWNER_CHALLENGE, malformed escaping, embedded raster/script, changed unmarked design paragraph and partial write failure. Case generation comes from all pinned IDs/tuples; no representative sample substitutes for the full denominator. UI renderer byte parity is required where escaping/formatting stays stable; any approved harmless serialization difference has a named structural comparison and a newly pinned deterministic profile.

## DOC-AC-05: Structurizr and PlantUML workflow

Observable outcome: preserve the editable DSL and six Markdown class sources, six image views and 15 native C4 outputs. The selected local JDK/CLI/jars/image pins are checked before process execution. All native and class SVGs remain vector text with padded white canvas and no stale renderer metadata. No remote renderer or upload occurs.

Cases: `ArchitectureTests.ExtractsSixExactBlocks`, `ImageViewsResolveToRenderedCodeAssets`, `VerifiesRendererIdentityBeforeInvocation`, `ValidateThenExportThenRenderOrder`, `SkinInjectionIsIdempotent`, `ExpectedNativeSetIsExact`, `RenderFailureStopsPackage`, `CanvasAndMetadataNormalizationParity`, `NoExternalRendererRequest`, `RepeatedPinnedRenderIsByteIdentical`.

Mocks/negative controls: jar/JDK/CLI mismatch, missing image source, missing exported native view, nonzero child exit, child succeeds with truncated output, changed class mapping, repeated skin include, external `!include`/URL and renderer producing raster/script content. Mock runner asserts exact argv and no shell-string execution. Real gate uses the approved available local toolchain and captures full process outputs. A mock subprocess exit 0 alone is insufficient evidence.

## DOC-AC-06: native standalone slideshow

Observable outcome: editable Markdown and HTML share one ordered deck model. Preserve cover plus 107 panels, actor/state/control/action/branch/success/error/cancel/next text and exact SVG bytes. All assets are embedded. Keyboard/hash/previous/next/home/end and first/last bounds follow the approved native deck runtime contract. Old Marp HTML byte parity is explicitly not claimed.

Cases: retain `tests_design022_contract.py` image/embedding assertions. Add `DeckTests.Exact108SectionsAnd107Images`, `EveryEmbeddedSvgEqualsPinnedStepBytes`, `EveryTextFieldAndBranchPreserved`, `MarkdownAndHtmlHaveSameOrder`, `NoRawMarkdownOrExternalAssets`, `NavigationFirstNextPreviousLastBounds`, `HashRestoresSelectedSlide`, `PrintIncludesEverySlide`, `RepeatedNativeRenderIsByteIdentical`.

Mocks/negative controls: wrong slide/image order, missing cover, raw Markdown image, truncated base64, rewritten decoded SVG, missing cancel/actor field, external font/resource, off-by-one slide bounds and a navigation event that shows two active panels. Correct browser fake returns structured CDP events and DOM states, never a precomputed pass flag. Navigation/print cases remain blocked until the parent approves the delivered HTML runtime contract.

## DOC-AC-07: complete native browser QA

Observable outcome: native .NET CDP validates all 108 slides, 76 state panels and 21 architecture diagrams in the approved browser/profile. Capture each slide, test navigation, record image/text bounds, verify no unexpected network requests, retain browser identity and hashes. Contact sheets supplement individual captures. PNG evidence does not enter canonical docs.

Cases: `BrowserQaTests.CdpResponsesMatchRequestIds`, `RejectsMissingOrOutOfOrderResponses`, `FailsOnBrowserExitAndProtocolError`, `CapturesEveryExpectedSlide`, `ReportsEveryOverflowAndBrokenImage`, `RecordsUnexpectedRequests`, `ChecksAllStateAndArchitectureAssets`, `DisposesOnlyOwnedBrowserResources`, `MissingBrowserFailsRequiredQa`, `ContactSheetReferencesEveryCapture`.

Mocks/negative controls: response ID swap, dropped event, browser dies mid-run, HTTP asset request, image width 0, clipping/overflow at one late slide, omitted final capture and stale HTML hash. Correct fake protocol proves the assertions and interaction order. Real QA must use the actual generated C# HTML/SVG hashes; the saved round024 receipts are baseline fixtures only. Visual inspection reports all findings and denominators, including any uninspected captures; no silent sample-based pass.

## DOC-AC-08: lossless factoring and reconstruction

Observable outcome: preserve original bytes, substitute only exact known canonical SVG URIs and reconstruct exact original HTML SHA256. Unknown, malformed and noncanonical URI text stays literal. No network/model call or input write occurs.

Cases: preserve both `test_factor_design_evidence.py` cases. Add `FactoringTests.Utf8AndRepeatedAssetsRoundTrip`, `AdjacentMalformedUriStaysLiteral`, `UnknownAndNoncanonicalDataRemainLiteral`, `ChangedInputViewMapOrAssetRejected`, `PathEscapeAndReservedTokenCollisionRejected`, `DuplicateReplacementMappingRejected`, `ExistingOutputRejected`, `EveryOriginalByteAccountedFor`.

Mocks/negative controls: the exact known/unknown repeated UTF-8 fixture and adjacent URI fixture from the original tests; corrupt manifest, altered asset/map/view, unknown review token, collision in literal HTML and exception during output. Expected offsets and original bytes are constructed independently from fixture literals. Hashes alone do not excuse omitted reconstruction comparisons.

## DOC-AC-09: bootstrap, environment and offline isolation

Observable outcome: native dependency acquisition accepts only approved pins, verifies bytes before use, extracts safely into task-local cache and never changes host settings. Offline paths make zero acquisition calls. Gradle wrapper cache readiness is distinct from a downloaded extracted distribution. No npm/Node/Marp requirement survives.

Cases: `DesignToolchainTests.AcceptsExactAvailableProfile`, `RejectsMissingAndMismatchedPins`, `OfflineNeverCallsHttp`, `PublishedChecksumCannotOverrideCommittedPin`, `RejectsTraversalSymlinkAndPartialArchive`, `PreservesExistingVerifiedCache`, `ReportsWrapperCacheSeparately`, `RejectsForbiddenRuntimeDependency`.

Mocks/negative controls: tampered ZIP/jar, mismatched publisher checksum, missing wrapper distribution, malicious archive paths/link targets, truncated download, interrupted cache write and adapter attempting npm. Do not execute acquisition during planning. Real acquisition tests require the approved network lane and local caches; every optional/unavailable tool is explicitly identified, and a required gate cannot pass by skipping it.

## DOC-AC-10: deterministic manifest and portable package

Observable outcome: the same artifact catalog drives verification, manifest and package. Required editable sources and 221 SVGs are included; caches, source catalog data, reviewer streams, historical generated output, screenshots, Word/PDF and credentials/machine paths are excluded. Ordinal paths and fixed archive metadata make repeated packages identical for the same approved profile and inputs. Packaging rejects failed/stale prerequisites.

Cases: `DesignPackageTests.ManifestCoversEveryApprovedFileExactlyOnce`, `ManifestExcludesItself`, `ArchiveContainsExactAllowlist`, `NoMachinePathsOrSecretsInPortableReceipts`, `StableOrderAndTimestamp`, `RepeatedZipIsByteIdentical`, `ExtractedManifestHashesMatch`, `DuplicateCaseCollisionAndPathEscapeRejected`, `FailedOrStaleQaBlocksPackage`, `InterruptedPackagingLeavesOriginalIntact`.

Mocks/negative controls: one omitted/extra file, stale hash, duplicate/case-colliding path, path traversal, cache/secret/machine-path intrusion, current receipt pointing to old HTML, timestamp/order drift and write interruption. Reopen the real ZIP and verify every entry and checksum. Preservation of the original portable execution contract depends on the parent's source-versus-binary distribution decision.

## Gate evidence contract

The registry generates the required AC/case/negative-control manifest. A receipt names every case and provider, exact input/fixture hash, native NUKE command, head, host/profile, start/end timestamps, assertion and interaction results, mock/real/live classification, and TRX/log paths. It rejects unmapped ACs/cases, duplicate IDs, missing cases, failures, skips or an empty denominator. Chronological receipts show green mocks and expected negative-control rejection before the associated production edits. Real gates reuse the frozen cases and expectations. The parent's full unit suite and design integration/browser TRX files accompany the final independent review. No existing legacy output or this planning inventory is an implementation pass.
