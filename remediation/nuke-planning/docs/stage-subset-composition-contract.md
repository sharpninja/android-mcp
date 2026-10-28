# Stage-subset composition boundary

`Core/DesignStagePlanner.cs` is the sole stage dependency and alias authority. Documentation composition consumes its admitted ordered DesignStagePlan. It must not encode a second dependency list or translate every architecture alias to a full render. Registry digest and requested aliases are validated before source/tool/output effects. Overlapping alias selections execute each selected stage once.

The existing full ArchitectureProjectionComposer remains the normal-verification baseline for Architecture002. Subset support must preserve that path's current 27-public-artifact result when ArchitectureRender is selected. Existing 12 pipeline assertions and 12 transport assertions remain unchanged. Add subset assertions through the same preparation, transport, admission, stores, lease and generation runtime, then bind the real composer only after the mock gate.

That 27-artifact boundary is the inner architecture service, not the entire future legacy-alias union. ArchitectureRender alias requests inherit ModelContracts and UiAssets from the authoritative graph. The outer recipe must include the existing 222-artifact UI projection once as well as the 27 architecture artifacts, yielding 249 artifacts and 238 SVGs for the current full union. Derive the actual sets from service outputs rather than hardcoding these counts. Preserve the existing inner-service assertions while adding explicit outer-union assertions. The existing bounded Design/Architecture CLI proof has not yet integrated that larger alias contract.

Each selected stage has a concrete existing service boundary:

- Tools consumes the lock/profile authority, shared tool acquirer and native image admission. Its evidence is retained under the admitted receipt/work/cache roots. No Node/Marp check is revived.
- UiAssets calls DocumentationModelReader/DocumentationProjector once and yields the existing 222-artifact projection, including offline HTML and editable Markdown. No second storyboard renderer is introduced.
- ModuleGraph invokes ModuleGraphValidator's issue gate. Declared-class metadata is populated by that same authority.
- ModelContracts consumes the shared parsed model and declared-class metadata plus the admitted source observations. Obtain metadata without importing a ModuleGraph issue-gate dependency when the planner did not select that stage. It calls ModelContractValidator's original clauses and emits the concise validation receipt.
- CodeExtract uses ArchitectureAssetPreparation for the six editable PlantUML files, deriving expected image views from canonical DSL rather than a second list.
- CodeRender invokes only the existing RenderCode transport phase and vector normalization. It yields six class SVGs alongside selected extraction output.
- ArchitectureValidate invokes only the ValidateWorkspace transport phase after the stages already selected by the authoritative plan. It emits the validation receipt; it must not export/render native views.
- ArchitectureExport invokes only ExportWorkspace and the existing local rendering-profile insertion. It retains editable model-generated exports and does not invoke RenderNative.
- ArchitectureRender invokes RenderNative for the derived 15 native view keys, normalizes vector outputs and publishes those 15 SVGs alongside the selected class artifacts.
- DocsVerify runs the shared native documentation checker over the source/generated union. It must not require the checksum file that the later Manifest stage creates.
- Manifest calls one checksum projection shared with the existing package builder. It excludes itself and must not create an archive.
- Package consumes that same admitted source/artifact union, checksum projection and publication policy, then uses the existing deterministic ZIP builder. Do not duplicate package/checksum/path/hash loops.

The recipe's Prepare function computes its complete output paths before any generator/tool process. The one immutable-generation runtime performs output-collision checks, source consistency checks, sorted writes and final manifest commit. The work runtime may retain uncommitted diagnostic files after a failure, as already specified; public output roots remain fresh and immutable. An extraction-only operation cannot require Docker/Java, locks or a browser simply because a larger union would need them. The input planner selects required sources and host prerequisites from the admitted selected stages, not from Gradle task edges.

Mock cases must assert each of the 13 alias selections against the authoritative planner's stages, representative overlapping unions, each renderer phase's absence/presence, exact output sets, source acquisition/model parse/tool admission counts, stage ordering, preflight failure zero effects, cancellation and unchanged inputs. Independent negative controls must reject an extra renderer phase, omitted stage, duplicate execution, early export/render, validation bypass, package/checksum cycle and a full-render shortcut for extraction/validation/export-only requests.

The actual Gradle adapter still requires generated metadata plus a single typed native request and real alias invocation receipts. This contract does not claim that adapter or the general document checker is implemented. Root owns registry/dispatch/parameter integration and the shared read-only routed source collector; Documentation owns the recipes and existing service composition. Canonical source migration has its own before/after data gate and must not be hidden inside a generation step.
