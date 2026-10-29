# Standalone legacy receipt caller completion

This increment implements remaining FR-BUILD-005 / TEST-BUILD-005 caller responsibilities. It does not change the Core stage graph, its alias vocabulary, validator rules, receipt formats or default factory output.

## Existing gap and chosen boundary

The CLI opts into `DocumentationLegacyReceipts`, but the narrow module/UI recipe branch does not publish receipts. The captured model branch also omits prerequisite receipts when document completion is not selected. The already-tested formatter and its one route table remain authoritative. No second validator, file writer, stage graph or receipt mapping is introduced.

## Order and acceptance

1. Prove the narrow caller cases using the existing narrow-source fixture and scripted factory before production changes. Cover selected module/UI routes, storyboard alias, unions, no-receipt tools/code selections, authority errors and cancellation. Derive expected receipt contents from independent pinned original receipt fixtures, not the formatter under test. Negative controls must reject omitted, altered, misplaced, wrong-media and extra receipts. Existing default cases remain unchanged.
2. After the mock gate passes, bind those same assertions to the existing factory with its optional formatter enabled. Keep all unselected authorities inert, retain source bytes, propagate the same cancellation token, and declare every produced receipt before materialization.
3. Extend the same opt-in behavior to the captured model branch using the existing preflight results. Preserve source recheck hooks and the existing validation failure behavior. Test model/UI and combined module/model selection independently of renderer success.
4. Run focused old/default and new same-case native checks, then normal full verification before publication. Actual standalone CLI calls provide additional caller evidence; mock or service evidence alone is not labeled live execution.

Publication checkpoint 010 remains immutable and is independently verified by its existing owner. This working increment is separate. No Python, Node, Actions or HV is invoked. No legacy file is declared safe to delete by this plan.

## Observed normal evidence

The mock-first conjunction passed 131 cases, including 38 new standalone mock cases and unchanged default recipe cases. The subsequent native affected gate passed 315 cases, including the same 18 correct narrow/captured cases bound to `DesignCompositionRecipeFactory`. Both gates had zero failures, skips and build warnings. Exact source and TRX identities are in `evidence/standalone-receipt-mock-gate.json` and `evidence/standalone-receipt-native-gate.json`.

Actual canonical CLI calls then verified these responsibilities:

- `generateArtifactManifest`: 20 inputs, 273 committed outputs and 272 checksum entries. All five public receipts and all 21 canonical diagram SVG identities retained.
- `packageDesignDocs`: 21 inputs including explicit operator policy, 279 committed outputs, 277 checksum entries and 278 exact archive entries. Each package receipt alias matches the same public receipt bytes. This does not prove a complete portable C# source payload.
- Narrow union `validateModuleGraph,generateUiAssets,renderStoryboards`: 8 inputs, 224 outputs and only the selected module/UI public receipts.
- Captured `validateModelContracts`: 14 inputs, 225 outputs and only model/UI public receipts, plus the existing exact `validation/model-sources.json` observation.

Proofs are `evidence/canonical-documentation-manifest-cli-native-proof.json`, `evidence/canonical-documentation-package-cli-native-proof.json` and `evidence/standalone-documentation-cli-native-proof.json`. An initial operator postcheck wrongly excluded the required model-source observation; its precise correction is retained in the latter proof. No runtime rule or original assertion was weakened. The normal full gate after this increment is required separately; none of this is HV acceptance or a whole-migration completion claim.
