# Android MCP design documentation

Canonical design: [Markdown](<docs/Android MCP Bridge Design.md>). [Screen definitions](docs/ui-screens-and-storyboards.md). [Storyboard deck](storyboards/storyboard.md). Design and offline tooling only: no deployed Kotlin Android app, phone actions or public service.

Use Gradle8.14.3 with Java21, the exact StructurizrCLI2025.11.09 image digest in toolchain-lock.json, PlantUML1.2025.10 and MarpCLI4.2.3. Existing local image supplies Java/Graphviz. Node26.8.2 was used for local Marp HTML. Dependencies reside under .tools and .gradle-home; no host installation is needed. The Gradle wrapper pins distribution SHA256. Keep these folders out of the portable archive; bootstrap verified dependencies into task-local caches first. The wrapper does not make a fresh cache magically offline-ready.

## Targets

Run ./gradlew --offline validateArchitecture generateArchitectureDiagrams generateUiAssets renderStoryboards verifyDesignDocs packageDesignDocs. The same invocation is used inside the pinned container with only this docs project mounted, NODE_BIN selecting a compatible existing read-only Node runtime, network disabled and no exposed ports. STRUCTURIZR_CLI defaults to the existing image's /usr/local/structurizr-cli/structurizr.sh. The README records the actual invocation and receipts once complete.

validateArchitecture depends on locally rendered code-view images and fails on invalid DSL. generateArchitectureDiagrams exports native C4 views through Structurizr then renders SVGs locally; image views scope the editable inline PlantUML class detail. generateUiAssets copies editable vector screen/storyboard source. renderStoryboards embeds SVG assets into a finite local Marp HTML deck. verifyDesignDocs fails on missing outputs, broken Markdown links or raster/script substitutions. packageDesignDocs creates a deterministic archive with only current docs/SVG/source/build configuration and validation receipts. No diagram is sent to a public renderer and no Structurizr workspace is uploaded.

## Tooling and license

The internal ARC port's pinned source and exact field/enum contract are documented in the design. MIT package metadata was verified upstream; authoritative notices remain a pre-distribution gate for copied/translated schema/code. This package contains the derived design contract, not the upstream source archive or a Kotlin port. Gradle/Structurizr/PlantUML/Marp are build dependencies with their own licenses; the archive includes configuration and wrapper provenance, not downloaded distributions or node_modules.

## Bootstrap and actual isolated build

On a machine with Python3/npm and the pinned existing renderer image available, run python3 tooling/bootstrap.py. It downloads only checksum-recorded Gradle/PlantUML task dependencies and performs npm ci with the included Marp integrity lock, without lifecycle scripts or global installation. Java21/Graphviz come from the documented image; this is separate from any Android app installation. Offline use requires a populated wrapper distribution and Marp cache.

The validated environment uses the official Gradle wrapper in the existing digest-pinned Structurizr container, user1000, network none, no exposed ports, task directory mounted at /workspace and an existing compatible Node binary at /tool-node/node. Node26.8.2 requires libatomic.so.1; an existing host copy is mounted read-only in that ephemeral container, its hash retained in toolchain-lock.json. This is an environment dependency, not a hidden package installation. The strict renderer-lock.json profile checks the exact JDK21.0.8, Node26.8.2, renderer jar hashes and committed/cached Marp lock. An environment mismatch fails rather than claiming byte reproducibility. Alternate runtimes require an explicitly reviewed new lock/profile. No task starts a public diagram server.

Execution and model/docs validation receipts are in validation/ in the archive. The portable ZIP omits .tools, .gradle-home, all source catalog data, reviewer receipts, old diagram outputs, Word/PDF and raster mockups.

Canonical UI source is ui-svg/*.svg; generateUiAssets verifies every manifest ID and copies identical bytes to docs/screens. Current package has35 unique docs SVGs:13 C4 views,5 class views,11 screens and6 storyboards. No Android application is generated.
