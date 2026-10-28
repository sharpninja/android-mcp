# Android MCP design documentation

Canonical design: [Markdown](<docs/Android MCP Bridge Design.md>). [Screen definitions](docs/ui-screens-and-storyboards.md). [UI blueprint](docs/ui-blueprint.md). [Editable storyboard source](storyboards/storyboard.md). Kotlin server and Compose UI remain design only; no Android application or device operation is delivered by this tooling.

The workstation toolchain is one native C# NUKE application in `../nuke-app`. A singular `--operation` selects a category operation. One typed registry owns legal parameters, preflight and dispatch; its private stage DAG owns ordering. Shared services implement source acquisition, catalog tooling, documentation projection, validation and packaging. There are no separate operational script entrypoints.

## Available native generation

Invoke the admitted native dotnet host with `run --project ../nuke-app/AndroidMcp.Build.csproj -- --root ../nuke-app --target Design --operation Assets --project-root <absolute-design-root> --output-root <fresh-absolute-output-root>`. The optional `--generation` sets the stable generation identity. Paths shown in angle brackets are operator inputs, not literal arguments. The output root is immutable: generation inputs are never overwritten, and the receipt manifest is committed last.

`Design/Assets` projects the canonical blueprint and design Markdown through one C# SVG/HTML renderer. The preserved corpus has 11 screens, 88 controls, 76 states, six storyboards and 107 ordered step panels, with 108 offline slides. The generation declares 222 artifacts including 217 SVGs; six class SVGs plus 15 native C4 SVGs belong to architecture generation. Editable Markdown and SVG remain deliverables. Browser-only inline navigation JavaScript is allowed in the self-contained HTML; no Node or Marp process runs.

`Design/Architecture` requires explicit project/output/cache/work/receipt roots, an admitted Docker identity, renderer user and online/offline tool-access policy. Its native implementation uses the pinned Structurizr CLI2025.11.09 image, Java21 and PlantUML1.2025.10. The local Docker daemon is explicit, the environment/configuration are isolated, image pull is forbidden, and rendering runs with network disabled. Online tool acquisition verifies published checksums; offline mode requires exact retained proof and cached bytes. Missing pins, fonts or tools fail with the prerequisite instead of silently changing the renderer.

Current native generation produced 21 SVGs and six editable class PlantUML files. The two reported diagram layout findings were corrected, rendered, directly inspected and promoted to the canonical sources and exports. Repository-only evidence paths are `../nuke-planning/docs/evidence/vqa-current-source-validation.json` and `../nuke-planning/docs/evidence/vqa-layout-canonical-promotion-applied.json`; these are not links within the portable design package. Model-contract and private-stage services have native fixture proof; complete portable package composition and all legacy validation-receipt callers remain migration work. These bounded proofs do not establish full legacy parity or hostile-review acceptance.

## Declared compatibility contract

The authoritative C# stage planner retains 13 Gradle alias names and 12 private stages. The generated `build.gradle.kts` adapter reads `tooling/nuke-design-stages.json` and forwards the complete alias selection once to `Design/GradleCompatibility`. Generation algorithms and ordering remain in the monolith. `Design/GradleProjection` regenerates these two adapter files. An actual offline Gradle invocation of `extractCodeViews` delegated to the current C# application and reproduced the six selected PlantUML outputs; its repository-only execution receipt at `../nuke-planning/docs/evidence/gradle-delegate-native-001.json` records the exact admitted tools, selected scope and nonfatal deprecation warning. This selected-alias proof does not claim that every packaging alias is complete.

`Validate` owns public validation operations. Generators call those same validation services as prerequisites. Category operations and Gradle aliases must select the same private stages, executing overlapping work once. Source/build configuration is the default portable distribution; downloaded binaries and caches are excluded.

## Sources, history and licenses

Current architecture uses one Structurizr C4 workspace and editable inline PlantUML class definitions. UI behavior belongs in the canonical blueprint, with one shared parsed model; generated copies are projections. The Android runtime remains the designed Kotlin/Compose APK, with its existing ARC/Binder/SDK/paired-ADB authority boundaries unchanged.

The earlier Python bootstrap, CJS generators/validators, Node/Marp presentation build and separate Python catalog container are obsolete historical tooling. Their retained source/receipts provide parity evidence; they are not current execution instructions. Historical fixtures are preserved under `../nuke-planning/docs/fixtures/pre-nuke-tooling.*`. Saved SVG/deck expectations have not been regenerated from the migrated implementation.

Upstream ARC notices and all pinned dependency licenses remain distribution requirements. Native generation uses local files and renderers; no diagram is sent to a public renderer and no workspace is uploaded. Portable packaging and its complete validation gates must pass before a package is described as ready.
