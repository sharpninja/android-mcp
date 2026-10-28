# Source and cache callable boundaries

This is a read-only snapshot of actual C# call sites inspected after checkpoint004. It is not an operation registry or a parity acceptance claim. Core/OperationRegistry.cs remains the only legal category/operation vocabulary.

Observed source CLI routes in that snapshot:

- Sources/Plan has SourcesPlan executor and Build.Dispatch calls PlanSources.
- Sources/Fetch has SourcesFetch executor and Build.Dispatch calls FetchSources. It materializes raw selected source bytes and sealed acquisition receipts; it does not currently invoke declaration extraction or write C# declaration caches.
- Sources/NativeParser has NativeParser executor and Build.Dispatch calls BuildNativeParser. It builds the pinned native support library through the same monolithic app.
- Sources/Extract, Sources/Inventory and Sources/Provenance are absent from the registry. Enum members and pure source services do not make these CLI operations runnable.
- Catalog/Build has a registered CatalogBuild executor, but the inspected Build.Dispatch switch has no CatalogBuild case yet. The Catalog owner is staging its thin partial and root owns the shared switch. This explicit pending call-site observation must be rechecked after that copy.

Observed source producer composition:

- SourcesRuntimeFactory.Create(profile, optional Java admission) returns an owned ISourcesRuntime. Use its Dispatcher and Concerns; do not reproduce provider or alias lists in CLI adapters.
- The factory registers aidl, signature, manifest, hidl-source, sdk-api-index and one permission-reference metadata adapter. Optional Java adds java-source through the injected loader. All alias/scope decisions remain in SourcesExtractionDispatcher.
- Permission-reference emits no declaration rows. Its complete JSON is retained as permission_reference metadata. Callback/taxonomy/applicability and XML/formal/header helpers remain pure shared concerns; richer native ABI/APEX/hash/Soong services do not promote frozen raw Binder evidence into declarations.
- SourceFactory native15 bindings are copied and awaiting root's coordinated gate. Factory43mock controls and permission-reference13native fixtures already passed. JavaSourceRuntimeLoader is separately owned by the Java helper and is not yet a proven production prerequisite at this snapshot.

Observed cache APIs and missing composition:

- Modules/Sources/Acquisition/CacheContracts.cs owns ISourceCache.WriteAsync/ReadAsync and the sole SourceCacheRequest/SourceCacheReceipt shapes. SourceCache is Acquisition-owned. No second cache encoder, reader, fingerprint or historical proof implementation belongs in Sources.
- SourceCache.WriteAsync accepts a JSON array, canonicalizes records through ICanonicalJson, writes one compact record plus LF per JSONL line, compresses it, and seals a .csharp-cache.json sidecar. ProducerId must start csharp-, explicit source/recipe/profile/code/runtime pins are validated, and differing historical gzip bytes remain explicit derived-generation evidence. Strict historical-byte refusal precedes publication.
- SourceCache.ReadAsync verifies sidecar identity, current producer fingerprint, compressed bytes, JSONL framing/count and expected canonical records. Its request currently requires RecordsJson; a caller must provide an independently valid expected declaration payload. It is not a lazy producer callback or a standalone raw-source-to-cache orchestration service.
- The inspected non-test C# references do not call ISourceCache.WriteAsync or ReadAsync from a source operation or CatalogBuild. This missing caller is explicit pending work.
- CatalogBuild currently verifies any declared historical metadata-cache compressed SHA and retains it as verified-unused audit metadata. It re-extracts raw source through the dispatcher; records_consumed=false and historical_identity_claimed=false are explicit. It does not silently use that cache as native proof. This bounded raw-reextraction policy is not proof of the old complete extract/cache orchestration.

The original tools/rehydrate.py extract function was read as text at lines277 through347. Its remaining caller semantics include locked doctor/producer preflight, inventory metadata-cache selection, physical source limit, exact existing cache/derived sidecar replay, missing raw-source refusal, only Java/source-API cache producers, expected declaration counts, strict historical gzip-byte policy, derived generation notes and inventory publication. Legacy cache_fingerprint.current supports only java-source and sdk-api-index, includes Python/library/code identities, and must remain historical evidence rather than being relabeled as a C# producer. Existing native cache primitive fixtures do not by themselves prove this entire caller.

The next callable source-cache slice should compose the existing ISourcesRuntimeFactory, ISourceCache, historical cache reader, selected source manifest/recipe model and artifact/lease services through one operation facade. It must establish its mock gate before production, then bind the same fixtures to native providers. Root owns registry/NUKE parameter binding, Acquisition owns cache storage/policy, Sources owns extraction/composition, and Catalog owns consumption/admission. Metadata summaries such as Java import context remain separate from declaration-cache records and must not be silently omitted. SDK annotations ZIP input likewise requires an explicitly typed binary/XML evidence boundary; the current text dispatcher does not parse ZIP bytes.
