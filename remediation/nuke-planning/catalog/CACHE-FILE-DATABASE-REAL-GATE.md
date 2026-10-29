# Native cache audit and retained file database proof

Direct XML inspection of `remediation/nuke-app/artifacts/tests/20261028T0219538536256-d5f4f35381e64599b2d84f44cd52268f/tests.trx`, SHA256 `aa3a8e0544dbf881a1d71d623332b8d806d2220f3c3389a7171080975414f9f3`, confirms:

- CatalogBuildCacheRealTests: 2 Passed, 0 other outcomes.
- CatalogBuildRealTests: original 7 Passed, 0 other outcomes.
- CatalogFileDatabaseRealTests: 5 Passed, 0 other outcomes.
- SqliteSchemaRealTests: original 25 Passed, 0 other outcomes.

The same two cache fixtures and assertions used by the preceding four mock rows now execute against the real build. Declared cache bytes are SHA-verified and retained. Historical producer, context and claimed count remain visible as verified-unused audit metadata. Deliberately nongzip historical bytes with a stale producer/count are not parsed or relabeled: native declarations come from raw source. The original wrong-cache-SHA failure remains a passing negative scenario.

The same five file-database fixtures and assertions used by the preceding ten mock rows now execute the real shared CatalogDatabase initializer. They observe an actual file DataSource, committed/closed/reopened rows, the same Combined schemas, refusal without mutation of an existing stage, prerequisites read before file creation, retained stage on invalid SQL, integrity/FKs and immutable source rejection. Original SQL bytes and earlier C# source versions were copied, hash-verified and retained before replacement.

The combined run is not all passing: root reports 323 Passed and 1 unrelated browser overflow failure out of 324 executed, 0 skipped, 0 build warnings. No full gate, historical profile parity or requirement completion is claimed from this focused proof. Native streaming build bindings remain staged outside the app, awaiting a separate coordinated gate.

MCP audit writes remain denied and were not retried. Local migration continues under the parent's instruction; this file does not establish MCP persistence.
