# Gradle compatibility conversion

The canonical Gradle file still contains legacy generators and remains to be converted. The current tooling README/model migration is already applied and verified; its original documentation bytes remain in `fixtures/pre-nuke-tooling.*`. Native model-contract validation passed all 1,169 real cases in run `20261028T0302529522864-c3e395fb41ad45b0816e8f453e075b40`, TRX SHA256 `68bc01c0479541d379d116750e26afee8fb589f7988f27c00e7efc4c88d84424`. The shared collector, parsed-model preflight and commit hook also have native tests. Their outer CLI composition and Gradle delegation remain pending.

## One registry and execution plan

Use `Design --operation GradleCompatibility` with a validated alias list once that operation is implemented. The root-owned `Core/DesignStagePlanner.cs` now owns the 12-stage graph and 13-alias selection metadata. Public category operations and Gradle alias requests select leaves of that same graph. The planner forms their union once and executes each selected private step once. Validators are the existing services owned by Validate; generation uses those same services as prerequisites. This document is not another authoritative graph.

The current canonical file declares **13 custom aliases**, not 14: `verifyToolchain`, `validateModuleGraph`, `validateModelContracts`, `extractCodeViews`, `renderCodeViews`, `validateArchitecture`, `exportArchitecture`, `generateArchitectureDiagrams`, `generateUiAssets`, `renderStoryboards`, `verifyDesignDocs`, `generateArtifactManifest`, and `packageDesignDocs`. Gradle's base plugin also supplies lifecycle tasks; their existence must not be confused with another explicitly authored generation target. Decide their forwarding behavior explicitly rather than silently calling them migrated.

Alias selections must preserve their observable stage boundaries:

- `verifyToolchain`: native tool admission, with Python/Node/Marp requirements retired by the approved stack migration.
- `validateModuleGraph`: the shared module validator.
- `validateModelContracts`: native model validation and the legacy UI-generation side effect, using one parsed blueprint authority.
- `extractCodeViews`: the six editable class PlantUML files only.
- `renderCodeViews`: class extraction and six class SVGs with their admitted renderer prerequisites.
- `validateArchitecture`: class renders, shared module/model validation and Structurizr DSL validation. Preserve its historical UI-generation prerequisite as alias selection data.
- `exportArchitecture`: the preceding validation plus Structurizr export and the local font profile.
- `generateArchitectureDiagrams`: the preceding stages plus 15 native C4 SVGs.
- `generateUiAssets`: native canonical blueprint/UI projection.
- `renderStoryboards`: the same native projection and self-contained HTML deck; no external Marp execution.
- `verifyDesignDocs`: the full generated corpus plus native document/vector/link checks.
- `generateArtifactManifest`: validated source/artifact checksums excluding the manifest itself.
- `packageDesignDocs`: the validated portable source/configuration/Markdown/SVG/deck package, excluding caches, binaries and obsolete executable tooling.

Do not map every architecture-related alias to the complete 27-artifact path. The same native composer now also supports extraction-only, validation-only and export-only selections through `PrepareSubset`, with 25 native cases after 55 mock cases. The complete 27-artifact path retains its original assertions. Explicit export publishes the 15 editable exports and their byte-exact render-skin support; export/render union executes export once. Connecting these selections to the outer operation remains pending. Alias metadata selects leaves and explicit historical side effects from the one registry graph.

## Thin Gradle caller

Generate and commit a small alias metadata projection and thin script from the C# registry, with its canonical registry digest. Gradle reads that projection to register the names, allowing a clean `./gradlew tasks` without a prebuilt NUKE application. A local native consistency check must reject stale checked-in projections. No second handwritten alias or parameter table belongs in Kotlin. Unknown or stale metadata must fail before native effects. The root-owned public operation and exact alias/digest flags are not yet frozen.

All wrapper aliases depend on one Gradle forwarding task. At execution it collects the selected canonical alias names from the resolved Gradle task graph and invokes the native dotnet host once, passing the complete alias list, registry digest and admitted NUKE parameters as separate argv values. There are no semantic `dependsOn` edges between aliases, no `doLast` generators/checksums/ZIP code, no directory scan for expected views, and no Python/Node/Marp command. Preserve NUKE's exit status. The native planner owns all required-input checks, phase ordering and output receipts.

The dotnet host and build-project location are explicit prerequisites. Do not assume a provisioning shim on PATH. Invoking Gradle itself must use the approved native Java/Gradle route; this change must not introduce a Bash or PowerShell wrapper as another operational entrypoint.

## Mock-first gates

Before production conversion, prove correct recording-runner requests and individual negative controls for each argv item, alias, alias union, duplicate selection, parameter, registry digest, working directory, environment, receipt, cancellation and exit status. Unknown aliases, unsupported options and stale metadata must produce zero process/network/output effects. Requests containing all 13 aliases and overlapping subsets must plan one union without repeated tool admission, UI generation or rendering.

The same cases then bind the native planner/caller. Run each former alias and representative multi-alias requests through the actual Gradle-to-NUKE path. Compare selected stages, outputs, failures and side effects against the pinned original behavior and approved obsolete-runtime substitutions. No fake process trace establishes actual caller parity.

## Canonical documentation and model update

The delivered Android runtime remains the Kotlin/Compose design. The implemented workstation tooling is the single C# NUKE application. Preserve the earlier Avalonia/.NET application-stack and Python/Node/Marp build-stack decisions only in clearly labelled history. Retain the existing Structurizr C4 workspace, local PlantUML class-image workflow, editable Markdown/SVG and offline HTML deck. No Mermaid or Pele is introduced.

The current README and DSL now describe one C# NUKE workstation application with modules. The four explicit canonical source-data changes were independently pinned and their prior bytes retained. Android component identities, the 15 native view keys, six class-image views and canonical UI entities were preserved. Native Architecture003 rendered the current DSL into 21 SVGs plus six editable class files. Its bounded renderer success does not establish full outer preflight or Gradle caller parity.

Fixture input adapters now use an explicit before-to-after mapping for the four approved source changes; unknown hashes still fail. Saved SVG/deck expectations were not regenerated. The 21 original architecture SVGs are retained under `fixtures/architecture-svg-original`, and the package fixture loader reads that bundle with all original expected hashes and assertions. This separates historical tests from the requested current003 publication overlay. The first publication gate exposed that missing separation and remains retained; the corrected publication gate is pending. Do not declare the full new workflow available before its registry adapters and gates exist.

The general native document checker remains required for both the former Gradle `verifyDesignDocs` clauses and `tools/check_documentation.py`: full expected vector set, vector XML/text, no raster/script/renderer metadata, current-source restrictions, relative links, UI-copy byte equality, checksums and offline presentation assets. Pure projector checks alone do not satisfy that operation.
