# Workstation source-data migration fixtures

`pre-nuke-tooling.*` are exact retained historical source bytes, including obsolete execution instructions. Do not execute those instructions. The files preserve the earlier Python/Node/Marp/Gradle workstation model for provenance and controlled input-hash comparisons.

`current-tooling.*` are independently authored proposed canonical source bytes. They are test inputs and expected source-data migration results, not output captured from a renderer. They describe one C# NUKE workstation application and explicitly identify the thin Gradle caller and other incomplete operations as pending. The Android Kotlin/Compose design is unchanged.

The four-file migration is guarded by ToolingDocumentationMigrationMockTests. Its source and post-migration bindings use the same snapshot assertions. Canonical source copies and test input-hash repins are separate guarded actions after the mock gate. Saved original inventory/type-index records, 96 declared class names, six inline class PlantUML blocks, generated UI navigation, 217 SVG fixture bytes and deck expectations remain unchanged. The intended C4 workstation topology/labels require their own new native generation receipt; retained old rendered diagrams remain historical evidence.

The migration does not introduce a Markdown/SVG behavior authority, a new renderer, an Android C# runtime, another operational application, a competing Gradle graph, or permission to run legacy Python/Node tooling.
