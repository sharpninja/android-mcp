# DocsVerify contract proposal

Status: staged design only. No validator implementation, application copy, full-corpus invocation or human validation is claimed.

## Shared prerequisites

Use `DocumentationModel` from the existing model reader, `IArchitectureViewIndexReader` from the existing composer/preparation authority, the shared strict JSON inspector, `IArtifactReferenceResolver`, the existing HTML decoder, `IContentHash`, and the existing package checksum format. Do not add a blueprint parser, view regex, entity table, filesystem resolver, hashing implementation or stage graph.

The 20261028T0705550670626-6757e49c183348f1b4a12ede34788204 TRX, SHA256 `71c8cad41981a50e75cbb8f2133f4b0a806d83d71d8a5bb4f9901d2b4f94eaef`, contains ArchitectureViewIndexRealTests 9 passed, ArtifactReferenceMockTests 96 passed, and JavaProducerIdentityMockTests 30 passed. The complete batch failed five CatalogCapture cases. Reference production and its same 48 cases were copied for a later native gate; that proof is still pending here.

## Proposed pure boundary

The input is an explicitly admitted immutable artifact union, the already parsed documentation model, the admitted DSL artifact and UI manifest, a validation profile, and a phase. The validator never enumerates directories, opens a source file, creates a package, writes a receipt or launches a process. The caller retains the artifact/source identities and writes the result through the shared store/canonical serializer.

`GeneratedDesign` is the first executable profile. It consumes the bounded current source/artifact union assembled by the documentation recipe. `BeforeManifest` checks that union before the package builder emits its checksum artifact. `AfterManifest` additionally requires and checks the admitted checksum artifact. The checksum artifact excludes itself, so validation does not introduce a checksum cycle.

Repository-wide checking requires a distinct admitted namespace contract before it becomes executable. A list of captured files does not establish directory existence or historical Markdown context. A profile label must be present in the result; bounded generated-design validation must not be reported as repository-wide validation.

The result carries structured issues using the existing `DesignIssue`, the selected profile and phase, the exact expected/actual SVG path sets, and the four original repository counters: checksums checked, Markdown links checked, rendered SVG count, and editable UI copies. Diagnostics preserve all per-artifact failures in a documented deterministic rule/path order. Mock fixtures must assert that order and every counter, rather than only a success flag.

## Expected artifacts and rules

1. Expected documentation SVG paths come from the same parsed model's screen/storyboard IDs, the admitted UI manifest's variant paths, and `IArchitectureViewIndexReader.Read(dsl).ImageSvgPaths`. Compare exact sets, reject duplicate/unsafe declarations, and retain missing and unexpected paths separately. Current 221 docs SVGs and 17 compatibility copies are fixture observations, never numeric policy tables.
2. Parse every admitted docs SVG using BCL XML with DTD processing prohibited and no external resolver. Require the SVG root/namespace and vector text; reject raster image elements, script elements and retained renderer SRC metadata. Preserve malformed-file diagnostics without silently dropping a file from the denominator.
3. Generated-design Markdown selection follows the retained Gradle docs/contracts/README/storyboards/code source scope. Resolve each selected link using `GradleMarkdown`; require an admitted file target. Reject current Mermaid fences. Repository Markdown is a separate scope/profile using `RepositoryMarkdown`, percent decoding once and accepting explicitly admitted directories.
4. Derive editable compatibility pairs from the same model screen/storyboard roles. Each ui-svg member must have a byte-identical docs/screens counterpart. Missing, differing and unexpected copies need explicit cases.
5. Repository offline HTML needs actual start-tag/attribute parsing for script, img, link, iframe, video and audio, plus the shared HTML decoder through `RepositoryOfflineAsset`. Preserve comments, raw script text and quoted `>` behavior in tokenizer cases. The Gradle raw external-asset/CSS URL scan is a separate legacy rule; a parsed HTML scan does not replace it. Preserve embedded SVG/image-count checks against the admitted storyboard Markdown/image references.
6. AfterManifest checks malformed lines, duplicate targets, unsafe targets, self references, missing targets and exact shared SHA256 against admitted bytes. The line format and self-exclusion remain the package builder's authority; do not invent another checksum serializer.

## Repository and fixture admission gap

`DocumentationSourceCollection` currently exposes captured file observations and identities, not directory inventory or historical Markdown context. Its source planner is bounded to nine project files, three required contract snapshots and three optional canonical contracts. Do not infer directories or a whole repository namespace from its receipt.

Retained raw Markdown fixtures preserve their original relative links. Moving or renaming those files is not a valid parity fix. A future repository profile must receive pinned source-context mappings with an explicit logical namespace/version and a complete admitted target closure, plus an explicit directory inventory. Mapping only the fixture's base path to a canonical filename does not prove that historical link targets are available or correct. Until admitted context/targets exist, retain the errors or report the profile unavailable. Never skip historical fixtures by basename or silently rewrite their bytes.

## Mock-first sequence

First declare the generated-design input/result interfaces and build contract fixtures using existing model/view fixtures. Include one complete admitted current corpus and minimal positive cases, then each SVG set/XML/vector/raster/script/SRC failure, link/profile distinction, compatibility-copy failure, each HTML tag/reference form, CSS/image embedding rule, checksum failure, exact counter and ordering assertion. Each assertion needs an independent incorrect response. Use the same fixture/assertion functions for the artifact-only implementation after mock proof. CLI/recipe composition and an actual paired corpus invocation follow the same-case native gate. Repository-context admission remains separately explicit and cannot be closed by the generated-design gate.
