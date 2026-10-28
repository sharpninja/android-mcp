# Bounded native source-to-catalog proof

Direct TRX inspection confirms the initial seven native CatalogBuild scenarios passed without changing the preceding mock inputs or assertions. The real adapter creates the original signature/manifest/AIDL source bytes, invokes the actual C# dispatcher and build service, then independently opens the produced SQLite database and reads the exported Entries, permission expressions, inventory, inventory manifest and catalog JSON. It validates schemas, integrity/FKs, export/database agreement, immutable binding rejection, deny-by-default aliases, the separate native producer fingerprint and retained source inputs.

TRX: `remediation/nuke-app/artifacts/tests/20261028T0211023187862-404015623d6f434b946f4388f0f8ca8f/tests.trx`.

SHA256: `47f6499a92d3e6155e9be9b3c80c6cee079b72847f3501d7cca3bbd502c38b24`.

- `CatalogBuildRealTests`: 7 Passed, 0 other outcomes.
- `CatalogBuildCacheMockTests`: 4 Passed, 0 other outcomes. Cache extension production followed this gate.
- `CatalogFileDatabaseMockTests`: 10 Passed, 0 other outcomes. File-backed factory production followed this gate.
- `CatalogPersistentBuildMockTests`: 8 Passed, 0 other outcomes. Streaming build production remains pending the shared root-owned streaming store.
- Root's complete supplied scope: 245 executed, 245 passed, 0 failed, 0 skipped; build reported no warnings.

The initial implementation uses an in-memory database and materialized records/exports. This receipt is not full historical producer parity, full-format coverage, runtime authority or a scalability claim. The separate file-backed/streaming increment, source annotation/import concerns and effective inventory/dispatch composition remain pending. The service's exported `complete` field remains false.

Native cache follow-on semantics are explicitly approved: verify the declared cache SHA, retain historical producer/context/count metadata as verified-unused, extract from raw source with the new C# fingerprint, and never parse or relabel the historical cache records. Wrong declared cache SHA still fails without a final catalog marker. The same two mock cases now have staged native bindings; no real result is claimed here for them.

MCP audit writes and live memory loading remain blocked by the previously recorded automatic approval rejection. No retry or alternate route was used. The parent was informed; this receipt does not substitute for a persisted session-log turn and does not mark a tracked requirement complete.
