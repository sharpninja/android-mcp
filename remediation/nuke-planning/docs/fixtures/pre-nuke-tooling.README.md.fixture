# Android MCP design documentation

Canonical design: [Markdown](<docs/Android MCP Bridge Design.md>). [Screen definitions](docs/ui-screens-and-storyboards.md). [Storyboard deck](storyboards/storyboard.md). Design and offline tooling only: no deployed Kotlin Android app, phone actions or public service.

Use Gradle8.14.3 with Java21, the exact StructurizrCLI2025.11.09 image digest in toolchain-lock.json, PlantUML1.2025.10 and MarpCLI4.2.3. Existing local image supplies Java/Graphviz. Node26.8.2 was used for local Marp HTML. Dependencies reside under .tools and .gradle-home; no host installation is needed. The Gradle wrapper pins distribution SHA256. Keep these folders out of the portable archive; bootstrap verified dependencies into task-local caches first. The wrapper does not make a fresh cache magically offline-ready.

## Targets

Run ./gradlew --offline validateArchitecture generateArchitectureDiagrams generateUiAssets renderStoryboards verifyDesignDocs packageDesignDocs. The same invocation is used inside the pinned container with only this docs project mounted, NODE_BIN selecting a compatible existing read-only Node runtime, network disabled and no exposed ports. STRUCTURIZR_CLI defaults to the existing image's /usr/local/structurizr-cli/structurizr.sh. The README records the actual invocation and receipts once complete.

validateModuleGraph checks the proposed acyclic Kotlin ownership/import graph. validateModelContracts checks dispatch/revoke/refresh model edges, class responsibilities, API route/bearer consistency and non-placeholder UI contracts. validateArchitecture depends on that check and locally rendered code-view images and fails on invalid DSL. generateArchitectureDiagrams exports native C4 views through Structurizr then renders SVGs locally; image views scope the editable inline PlantUML class detail. generateUiAssets validates the canonical Markdown UI blueprint and projects vector screen/state/storyboard SVGs, manifest, reference Markdown and Marp source. renderStoryboards embeds SVG assets into a finite local Marp HTML deck. verifyDesignDocs fails on missing outputs, broken Markdown links or raster/script substitutions. packageDesignDocs creates a deterministic archive of current docs/SVG/source/build configuration, explicitly marked history notes and concise path-free model/docs validation receipts. No diagram is sent to a public renderer and no Structurizr workspace is uploaded.

## Tooling and license

The internal ARC port's pinned source and exact field/enum contract are documented in the design. MIT package metadata was verified upstream; authoritative notices remain a pre-distribution gate for copied/translated schema/code. This package contains the derived design contract, not the upstream source archive or a Kotlin port. Gradle/Structurizr/PlantUML/Marp are build dependencies with their own licenses; the archive includes configuration and wrapper provenance, not downloaded distributions or node_modules.

## Bootstrap and actual isolated build

On a machine with Python3/npm and the pinned existing renderer image available, run python3 tooling/bootstrap.py. It downloads only checksum-recorded Gradle/PlantUML task dependencies and performs npm ci with the included Marp integrity lock, without lifecycle scripts or global installation. Java21/Graphviz come from the documented image; this is separate from any Android app installation. Offline use requires a populated wrapper distribution and Marp cache.

The validated environment uses the official Gradle wrapper in the existing digest-pinned Structurizr container, user1000, network none, no exposed ports, task directory mounted at /workspace and an existing compatible Node binary at /tool-node/node. Node26.8.2 requires libatomic.so.1; an existing host copy is mounted read-only in that ephemeral container, its hash retained in toolchain-lock.json. This is an environment dependency, not a hidden package installation. The strict renderer-lock.json profile checks the exact JDK21.0.8, Node26.8.2, renderer jar hashes and committed/cached Marp lock. An environment mismatch fails rather than claiming byte reproducibility. Alternate runtimes require an explicitly reviewed new lock/profile. No task starts a public diagram server.

Execution and model/docs validation receipts are in validation/ in the archive. The portable ZIP omits .tools, .gradle-home, all source catalog data, reviewer receipts, old diagram outputs, Word/PDF and raster mockups.

Canonical UI behavior/state/storyboard source is [ui-blueprint.md](docs/ui-blueprint.md); tooling/render-ui.cjs defines editable vector layout. Generated SVGs remain vector-text exports. The exact current screen/control/state/step/export counts are recorded in build/receipts/ui-projection.json; state and step variants are linked from the generated UI reference. No Android application is generated.


Deployment lines between host client instances and the APK are logical protocol relationships carried by the explicit owner-established loopback ADB forward, not additional direct network ingress. The on-device agent is a separate-UID local process. Offline catalog tooling represents a workstation process; the documentation toolchain is a separate software system outside the Android runtime; an unlinked documentation instance denotes local build output only. Edit SVG labels through DSL/PlantUML sources and regenerate; textLength is renderer output, not an editor constraint on source.


### Wrapper distribution cache before offline execution

After `python3 tooling/bootstrap.py`, run the official wrapper once with network
access and the intended cache: `./gradlew -g .gradle-home --version`. The wrapper
downloads its pinned distribution from services.gradle.org and verifies the committed
distributionSha256Sum before execution. This step requires an existing compatible
JDK; run it inside the same pinned renderer environment if the host lacks one.
The bootstrap's `.tools/gradle-8.14.3` extraction is not the wrapper dists cache.
Only after the wrapper version check succeeds should the documented `--offline`
build be used. No Android build, global software install or public service is implied.

The portable validation directory contains only docs-validation.txt,
architecture-validation.txt, module-graph.json, model-contracts.json and
ui-projection.json. Full local invocation, historical build/HV receipts, screenshots
and machine paths are excluded. The checksum manifest pins all portable sources.
