# Native import and projection cursor plan

Scope: continue FR-BUILD-004 and TEST-BUILD-004/007 after the bounded seven-case build, verified-unused cache audit, file-backed factory and streamed export increments. This plan does not introduce another app, operation, parser, canonical formatter, hash or artifact store. The parent owns all NUKE executions and shared-file copy windows. The mock gate and bounded native result are recorded below; full migration and HV acceptance remain pending.

## Current limitation and observable result

Before this increment, the file-backed build streamed output cursors through the shared store, but retained a list of every source's raw bytes/extraction result, every normalized declaration, and every selected Entry/inventory entity. CatalogProjection also received the whole declaration list. Bounded export writes do not establish bounded whole-producer memory.

The next result is source-at-a-time staging and database-backed declaration traversal. Imported documents and private raw extraction records live in retained SQLite files. No catalog-sized method-declaration, selected Entry or inventory array is needed by the build. The type index, formal-name sets, structured field shapes, type-contract cache, source metadata and one identity's occurrences remain necessary working state. A single extractor still returns a materialized per-source result through the existing Sources API. No constant-memory or arbitrary-size-input claim is authorized.

## Locked seams and processing order

1. Keep the existing CatalogBuild public operation and exact required CLI parameters. Continue the current Source runtime factory/dispatcher, Source permission and type helpers, CatalogContracts, CatalogLifecycle, CatalogProjection, DeviceResolver and shared artifact store.
2. Preserve all source/inventory/cache SHA checks and exact upstream Git blob framing. Cache bytes remain verified-unused. Read only one raw source byte array/extraction result at a time; the manifest and source metadata can remain resident. Use the shared stream SHA helper for file rechecks rather than introducing incremental crypto code.
3. Create a retained private native extraction database outside the final artifact set. Its one versioned native staging schema stores source ID, record ordinal, raw record JSON, source concerns and issues. It uses the same CreateNew/SQLite initialization policy as CatalogDatabase. Keep the canonical publication DDL unchanged. Do not put private extraction tables in final catalog.sqlite.
4. Serialize each source's result completely into this private stage before requesting the next source. Retain the separate C# producer identity. Native staging records never acquire an old Python processing identity. Commit private source evidence independently of the catalog import transaction, so a later AST/schema failure preserves the already captured source evidence.
5. Derive the permission symbol table from the staged raw declaration/concern facts. Normalize declarations into the canonical catalog database by reading a cursor, not by reparsing source or accumulating all documents. Duplicate exact occurrence locators still fail through the existing primary key.
6. Add a CatalogProjection overload accepting a replayable `Func<IEnumerable<CatalogDeclaration>>` cursor factory. The existing list constructor delegates to the same engine for the seven original fixture assertions. Build type/formal indexes, structured fields and type contracts through repeatable database passes. Do not copy the field/header/parser or contract-digest rules.
7. Enumerate binding IDs in SQL order. Read and sort only that identity's occurrence documents. Preserve the original stable ordering: SQL source_id/line order first, then source-rank and Unicode-scalar path order. A same-rank/path tie keeps the former source_id/line order. Preserve all provenance, permission absence/conflict evidence and declaration variants.
8. Insert selected Entry and inventory documents immediately into registry tables. Validate through cursors and reconcile SQL counts. Export from the same tables through the already proven streaming store. Do not rebuild Entry/inventory arrays for schema checks, catalog JSON or aliases.
9. Finish all readers and close the canonical stage before final database copy. Retain both private stages, source inputs and failed output fragments. Stream/hash the final marker through the shared store; no moves or deletion of original evidence.

## Mock-first tests and real binding

Use the same original three-source fixture and duplicate-source fixture. Add an independent scripted provider before implementation, with literal expected binding/occurrence/raw-record counts and correct output identities. Pin an ordering fixture with two occurrences sharing rank/path but different source IDs and lines.

A test-only dispatcher wrapper records each extraction access window and rejects reads from a prior result after the next extraction begins. This checks that the build completes its required record reads before moving to the next source; it does not change the public extractor lifetime contract or prove garbage-collector behavior. The real binding uses the actual Sources dispatcher under that observer. Independently reopen the retained private record stage, reconcile its rows with source reports, and prove final catalog.sqlite has no private extraction tables. Inject an AST/import failure after source capture and prove raw evidence remains while no final marker appears.

Each expected count, hash, ordering, cursor/window observation, retained-stage fact and final-marker observation gets its own wrong-value/omission/exception control inside the passing mock harness. Native implementation starts only after the root acknowledges the green mock TRX. Bind the same fixtures and assertions to the real service. Keep original Build7, Cache2, FileDatabase5, PersistentBuild4, ScopedProjection7, CatalogContracts38, SQLite25 and canonical/profile/mixed native regressions in the coordinated gate. Derive actual denominators from its TRX.

Peak working-set scalability remains a separate unknown until a representative local source-manifest execution is measured. Neither a matching test count, a source access window, nor a 64 KiB output-write bound establishes that broader property.

## Bounded normal verification receipt

The parent acknowledged eight cursor mock rows before native implementation, in `20261028T0407098868588-becf69ae3604402a9b7cee75dc62ce88/tests.trx`. The six-file native copy was source/prior/postcopy hash guarded; the four replaced files remain under `evidence/*before-import-cursor-<priorSHA>.cs.txt`, and the two new files did not replace earlier paths.

The actual TRX `remediation/nuke-app/artifacts/tests/20261028T0424232640492-03ffd5ef8730442b87e2f3ec3703fa2f/tests.trx`, SHA256 `851aa1c3e536950e5b6dd876de1f960d47482c8844cc0a0e37bbebb7e52f2d6e`, contains Cursor4, Build7, SourceIntegration4 and actual registered CLI7 passing native cases. The four cursor cases reopen the retained raw stage, check eleven/nineteen captured records, ten/eighteen declarations, ten selected identity batches with largest batch one/two, no post-window raw reads, private-table exclusion, AST-failure retention and nonempty-output refusal. The same fixture expectations were retained. No garbage-collector or constant-memory assertion was substituted.

The entire batch has 2,202 passed and six failed BrowserQaCli cases: external, svg, navigation, fonts, image and overflow each expected exit1 and observed255. These failures remain visible; this is not a whole-batch green claim. PersistentBuild/cache classes are absent from this particular TRX. `evidence/catalog-import-cursor-real-gate.json` records the selected denominators, current six source hashes, complete batch failures and remaining limitations. No HV or migration completion state changes follow from this bounded normal result.

## Audit limitation

MCP actions/decisions and live memories remain blocked by the recorded automatic approval rejection. This local plan records the proposed implementation seam and pending tests; it does not update a TODO/requirement status or substitute for a persisted MCP turn. No retry, alternate transport, Python, Node, external reviewer, GitHub Action or device action belongs to this increment.
