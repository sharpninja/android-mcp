# Legacy Gradle receipt compatibility inventory

Read-only source comparison at local 2026-10-28T11:23. This records five existing compatibility duties, not new validation engines or evidence that the receipts have been emitted. Old JavaScript and Gradle were read as text only. No application edits, builds, browser execution or HV were performed for this inventory.

`packageDesignDocs` copies exactly these five files from `build/receipts` into ZIP `validation/`. Therefore typed native validation success alone does not satisfy the original receipt publication contract. Native receipt adapters should consume the existing return values and shared canonical/hash services. Existing source input and generated-output union ownership remains with the Docs factory.

## module-graph.json

Original `validate-modules.cjs` returns the object below only after its collected errors are empty. The CLI writes `JSON.stringify(result,null,2)+'\n'`.

```json
{
  "class_count": "number of unique declared class/interface/enum names",
  "module_edges": "deduplicated sorted cross-module edges, formatted a -> b",
  "module_graph_acyclic": true,
  "scope": "Proposed design/source imports; no app runtime proof"
}
```

The first two quoted descriptions denote number/array values, not literal output strings. Native authority is `DocumentationModelPreflightResult.Modules`, the existing `ModuleValidationResult(ClassCount,ModuleEdges,Issues,DeclaredClasses)`. Emit only after `Issues.Count==0`; the result has no dedicated acyclic flag or scope string. The original scope literal and successful existing cycle validation provide those receipt fields. Do not infer success from the presence of `ModuleEdges`: validators may return issues. Preflight enforces issues only when `EnforceModuleGraph` is true, so the receipt adapter must respect the selected stage and validated result.

## model-contracts.json

Original `validate-contracts.cjs` returns and writes exactly `validated`, `scope`, `screens`, `states`, with two-space JSON indentation and one trailing LF. `validated` is true; scope is `Design consistency only; no Android implementation or Google data assessment`; screens is `blueprint.screens.length`; states sums each screen's `Object.keys(states).length`. CLI source-pin/canonical-copy checks precede validation.

Native authority is the existing `ModelContractValidationResult(Validated,Scope,Screens,States)` at `DocumentationModelPreflightResult.Contracts`. Its production validator returns that exact scope literal and uses the existing parsed model. Preflight constructs contract-copy observations from the one captured source snapshot; do not replace that admission with a new file scan or fixture model.

## ui-projection.json

Original `render-ui.cjs` writes these eight fields with two-space JSON indentation and a trailing LF supplied by its shared `write` helper:

```text
screens = m.screens.length
controls = sum Object.keys(screen.controls).length
states = ids.size, the checked globally unique state_specs keys
storyboards = m.storyboards.length
steps = sum storyboard.steps.length
state_svg_files = sorted state + storyboard step SVG relative paths, with docs/ removed
canonical_source = "docs/ui-blueprint.md"
implemented_android_ui = false
```

`stateFiles` is sorted before assignment to `m.state_svg_files` and before receipt serialization. The same array includes state and step captures; do not equate it with only state panels. Native authority is the existing parsed `DocumentationModel` (`Screens`, `Storyboards`, `Blueprint`) plus the already projected `docs/ui-asset-manifest.json` in `DocumentationModelPreflightResult.Ui.Artifacts`. The manifest is the existing projection's sorted `state_svg_files` authority. The native `DocumentationProjection` returns artifacts only, without this eight-field receipt object. Reuse the existing parsed model and admitted projected manifest; no second blueprint parser or hardcoded current counts are required.

## architecture-validation.txt

Original Gradle `validateArchitecture` runs only after code rendering, module validation and model validation. After the Structurizr CLI exits successfully it writes exactly:

```text
Structurizr validation exited0
workspace_sha256=<lowercase SHA256 of admitted architecture/workspace.dsl bytes>
```

There is a trailing LF after the hash. Existing native authority is `IArchitectureRendererTransport.ExecuteAsync(..., ArchitectureRendererPhase.ValidateWorkspace, ...) -> ProcessResult`; `ArchitectureRendererTransport` rejects cancellation and nonzero exit before returning. Its `ProcessResult` carries exit code and stdout/stderr receipt paths. The same admitted DSL artifact and shared hash authority supply the hash. `ArchitectureProjectionComposer` currently awaits that result then discards it and returns only projected artifacts. A successful SVG or a generation manifest is not a replacement for this phase-specific receipt. A minimal factory/composer receipt binding must retain the existing successful validation result and exact admitted DSL identity; it must not execute Structurizr again.

## docs-validation.txt

Original `verifyDesignDocs` writes exactly, with a trailing LF:

```text
Vector SVGs=<docs SVG count>; Markdown relative links valid; no raster substitutions; no public rendering
```

The count is only `docs/**/*.svg`, excluding compatibility `ui-svg` copies. The task first enforces the exact expected SVG set, vector/text/no-raster/no-script/no-stale-source rules and selected Markdown links/Mermaid rules.

Native authority is the existing successful `GeneratedDesignValidationResult` at `BeforeManifest`, with `Counters.RenderedSvgCount`, `Issues`, `ParseDiagnostics`, and exact expected/actual SVG path sets. Current corpus has 221 docs SVGs; that is evidence for that input, not a default or fixed count. The one `GeneratedDesignValidator` already supplies these checks; no repository-wide profile is implied. `DesignCompositionRecipeFactory.RequireValid` currently discards a successful result, retaining the whole result only on typed failure. Receipt binding must retain the successful result/counter. Neither the compatibility text nor generated-design validation alone establishes a new network execution audit; preserve the existing renderer admission and actual process evidence behind the original text.

## Current publication boundary

An exact-name search of native production `.cs` files found no emitters for the five legacy receipt names at inspection time. `DesignCompositionRecipeFactory` returns the admitted source/projection union, source-observation receipt, optional checksums and package. Its private completion paths do not yet declare these five compatibility receipts. An adapter must declare and materialize them coherently before package construction; the intended ZIP destinations remain `validation/<name>`. Manifest/checksum ordering and package consumption belong to the existing Docs conjunction. This inventory does not authorize introducing receipts after checksums or recomputing a manifest.

Original JSON property order/whitespace is recorded above. Whether byte-identical formatting or semantic JSON compatibility is required must be explicit in the existing factory's mock contract; do not silently claim byte parity from canonical JSON output.

## Source pins

Original text sources:

- `remediation/design-c4/build.gradle.kts`: `877c30e3bf39f6254e8d9ca952e73fe68a04de60fa07a0a358e541616ac469d8`.
- `tooling/validate-modules.cjs`: `aec2dba1bb10212593331a01ef2aa3005a10c95c0894cce1a1109c6a11d239fd`.
- `tooling/validate-contracts.cjs`: `30956b3f90ac52dd47f980a14cca495d98c1d3286e7b6e32a2bcac8a338e77b5`.
- `tooling/render-ui.cjs`: `b1d515c91b4ada4aabf4b11a6b2ac66519dfbde7b6296d68d8434ebb091fe6b8`.

Native production sources:

- `DocumentationModelPreflight.cs`: `cde80f369315cb613eb56f2771062681a4a257b5171024b850c1d668325a8a19`.
- `ArchitectureRendererTransport.cs`: `f48cb9af4176ac490e6b8609225b4f6cc8e10368a96fd523f6bf3f0e3f7823c6`.
- `ArchitectureProjectionComposer.cs`: `50dc9634fe0d0952921836521abde57846418aab5d4f4d5e50e4e80ab27a1406`.
- `DesignCompositionRecipeFactory.cs`: `5d2d300a85eee2f4286b5994040861a61833487cef62913eff3546839a45bc81`.
- `GeneratedDesignValidationContracts.cs`: `1162c9170a9ac55da990419390cf52a4490882e6ceeef393ce0227d946472d34`.

No full producer, repository profile, Google corpus, Android runtime or HV completion is claimed.
