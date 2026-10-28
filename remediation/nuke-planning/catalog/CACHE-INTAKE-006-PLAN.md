# Catalog caller of the shared native Source generation

Scope: continue the approved monolithic CatalogBuild migration with actual trusted Sources/Extract cache-hit consumption. This supersedes neither the existing no-index raw path nor its verified-unused historical-cache audit. Cache consumption is an additive typed path using a caller-pinned native generation. All work stays outside the app until checkpoint005 is copied and the parent grants a coherent copy window. No production extension starts before the exact consumer mock gate.

## Locked responsibility split

Sources owns one ISourcesExtractOperation and one ISourceExtractGenerationReader. The existing Publication projection is the only recipe/source-row parser. The generation reader admits the caller's exact trusted index SHA256/length before parsing it, then verifies the selected projection, producer/profile/context and each sealed cache identity through the sole ISealedSourceCache SPI. Catalog must not implement cache lookup, decode gzip/sidecars, parse a rehydration recipe or infer a route by extension.

Generation publication is cache-only. CacheSourceLimit counts selected logical cache-producing rows under the preserved legacy rule, independently of deduplicated physical source selection. Noncached or unindexed rows still resolve through the SAME Source facade ResolveAsync path. Catalog has no raw fallback beside it. A verified cache hit skips raw source reads and extraction, including when the consumer's raw file does not exist. A miss/raw route is extracted only by that facade and retains a separate native producer identity.

The current proposed Source contract has SourceExtractGenerationRequest(projection, profile, producer, trustedIndex, optionalJava), an IDisposable admitted generation and SourceExtractRow carrying RowIndex, SourceLength, actual SourceContext, canonical declaration-array RecordsJson, structural status, issues, metadata, optional sealed RawSourceBase64, cache receipt/seal and CacheHit. The Source owner controls the final ResolveAsync method and composition constructor. Exact API pins will be recorded before the mock copy; the earlier template changed while outside the app and is not treated as immutable evidence.

## Catalog admission and ingestion

1. Add one optional typed source intake to CatalogBuild. Open and admit the exact reader request before creating a catalog lease, private database or output artifact. Existing no-intake behavior remains unchanged. Index/profile/producer/projection admission failure has no catalog effects.
2. Map each current inventory source to its exact Publication RehydrationSourceRow using the original row index and complete canonical OriginalRow equality, preserving logical path and physical source membership. Use the projection's selected physical-source indices to select the corresponding logical rows. No loose filename, path-only or first-match fallback is accepted. Missing, extra, ambiguous or edited row context fails closed. This is association with a typed parsed source row, not another recipe parser.
3. For each selected row call the admitted facade exactly once. Do not directly call Catalog's dispatcher, read raw source/cache bytes or independently select a hit/miss. SourceLength supplies input_bytes without a raw read. Source facade owns miss verification, extraction, Java parser admission and any cache capability policy.
4. Parse the returned RecordsJson declaration array exactly once at private-stage ingestion through the existing strict JSON authority. Insert records/concerns in the same CatalogImportStage engine before resolving the next source. No catalog-sized extraction result list is introduced. Verify the returned row identity/context is the admitted row; do not accept a response for another row.
5. Catalog SQL source_id remains the Catalog acquisition/processing identity. Preserve the actual admitted SourceContext independently, including its recipe-SHA snapshot identity. Never rewrite it to the derived SQL source_id. Retain Source producer/profile, receipt/seal, hit/miss and source length as explicit audit metadata. Native cache records never get an old producer name, and a cache hit never gets a raw-re-extraction disposition.
6. A hit with issues, unsupported/malformed structural status or other whole-source diagnostic needs must include complete valid RawSourceBase64 covered by the trusted generation. Store that exact sealed raw evidence in issues. Missing/malformed/inconsistent raw evidence fails closed, with no empty bytes, lossy UTF8 replacement or raw-file reread. The Source reader owns validating that sealed evidence against the raw pin; Catalog consumes its admitted result and enforces the required presence.
7. Final publication rechecks the original inventory bytes. For raw miss/no-intake rows preserve the established source-file recheck. For a verified admitted cache hit the trusted index/seal establishes source provenance, so the final raw-file recheck is skipped as well as the initial read. Otherwise a missing raw file would still incorrectly fail a genuine hit. Do not silently skip checks for editable historical caches.
8. Preserve the current projection, occurrence batching, permission/linking/annotation semantics, schema/FK/count checks, streamed export and final database marker. Dispose the admitted Source generation after its use; runtime lifetime belongs to its factory/facade.

## Locked Java prerequisite behavior

The parent reaffirmed its earlier locked decision: upfront Factory.Create and optional Java ABI/native admission are ALLOWED on a verified cache hit to derive one runtime capability authority. Keep the current explicit selected java-native-manifest behavior and one SourcesRuntimeFactory registration authority. A hit must skip raw reads, Extract and WithTree; it does not have a parser-free ABI requirement. No new descriptor/capability service, second kind map, system parser, implicit build/fetch/install or copied native admission is allowed. The earlier parser-free suggestion was not approved and is not an expected test outcome.

## Mock/native fixture matrix

Use the existing three raw-source Build fixture and shared independent DB/export validator. Correct scripted reader responses and individually wrong controls precede any production change. Pin the final reader DTO before copying these tests.

- Complete sealed cache hits with absent consumer raw files: no Catalog dispatcher/raw-cache reads, same declarations/concerns/export assertions, one row resolution per selected row and exact original context retained independently from SQL identity.
- One malformed AIDL/unsupported hit: exact full raw Base64 and schema/status/issues retained, no raw reread, no false native raw-extraction claim.
- Missing or invalid sealed lossless evidence: fail closed and never publish a final database.
- Mixed cached and noncached/unindexed rows, including a logical cache limit: hits are consumed and raw misses go through the same facade exactly once. Physical aliases remain distinct logical context rows when the projection says so.
- Wrong caller index SHA/length/profile/producer: reader admission fails before Catalog effects.
- Edited inventory row, foreign row response or context: refuse association instead of borrowing the first matching path.
- Verified Java hit without raw source: succeeds through the actual registered Catalog caller with the already allowed upfront selected Java ABI admission. Assert no raw read/Extract/WithTree. A raw Java miss without required selected admission fails closed through the same Source factory/facade.
- Late trusted seal/data change: facade rejection never becomes Catalog's alternate raw extraction or a successful publication.

Native cache-hit fixtures retain raw producer originals in a separate producer-owned root. The consumer root starts with the required manifest/index/cache artifacts and no raw source files; nothing is deleted or moved to simulate a hit. Use the actual Source generation writer/reader and sealed SPI after their own gates, rather than handwritten cache files or a second lookup implementation. The same fixture assertions later bind actual registered Catalog/Build, with process captures and current DLL/host pins through the existing NativeProcessRunner helper.

## Evidence and limits

The existing 22 selected cursor/build/source/CLI assertions passed before this plan. The parent reports full checkpoint005 46,904/46,904 passed, including the exact raw-tamper native binding; independent receipt pinning follows before closing that normal assertion allocation. OpenExisting14 mock rows, annotation attachment native10 and this cache intake remain outside-app/pending. Full SDK memory, full historical-profile equivalence and HV acceptance are not established by a hit count. MCP audit/requirements access remains denied and was not retried; local plans do not update those stores.
