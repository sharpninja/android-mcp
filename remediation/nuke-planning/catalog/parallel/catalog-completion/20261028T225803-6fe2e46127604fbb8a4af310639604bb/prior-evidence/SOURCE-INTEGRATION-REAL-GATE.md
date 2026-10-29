# Native SourceIntegration normal verification

Direct TRX inspection confirms the same eighteen native cases now pass after root rebuilt the monolith. No Catalog/source/fixture/assertion change was made between the stale-binary run and this rerun.

- Earlier run: `artifacts/tests/20261028T0346131189227-d308cc0740eb4809bbff4eb25e337a36/tests.trx`, SHA256 `cca38ebf0f9d3cd12e237d2063667668b337b289d4296bb76aa6a560db9a6e3f`: 16 Passed, 2 Failed. Direct Build7 and CLI7 passed; SourceIntegration had 2 Passed and 2 Failed.
- Rerun: `artifacts/tests/20261028T0352420909095-3e0f1b76b9184fb9a984226b58f334b9/tests.trx`, SHA256 `2d79b3a0603bb3ce3546cedfdfba4ed20c759b63cfbbde1764da498e4c992a92`: 18 Passed, 0 failed/other outcomes. SourceIntegration4, direct Build7 and actual CLI7 all Passed.
- App assembly after bootstrap: `6d32d109e09685d7777cca1b3fcd41128f120d085a80a0de9671154e4322ea56`. The earlier fixture databases recorded `7b89b7acc14594ae7042d46516954f3fcfeed4fdbcbfb749cfb72a4a8aa77a2a`, matching the 03:39:12 binary. Consumer source was copied at 03:45:34.

The four original SourceIntegration assertions now bind to the real source runtime, Java loader/parser, CatalogBuild, canonical SQLite database and exports:

- Structured AIDL fields retain unknown wire tags; root kind/schema and no false structured semantic-gap result match the original assertions.
- Distinct Java/source and current JVM Entries share CAMERA only through the proven exact JVM key, which remains visible as source evidence.
- Java-kind cache SHA mismatch fails before extraction/publication and preserves inputs.
- Malformed AIDL retains its exact raw schema with unsupported status and nonzero issues.

The raw source fixtures are the original inputs. For Java/index cache transport, the native fixture supplies explicitly synthetic verified-unused cache bytes under the separately approved raw-re-extraction policy. They are never parsed, relabeled or claimed as historical producer bytes. The native source fingerprint is separate. This is semantic/raw-source integration proof, not historical cache byte parity.

Chronological prerequisites: eight Catalog integration mock rows and 732 Source AIDL metadata mock controls passed before their production changes; the Source producer's thirteen native metadata fixtures passed before the consumer gate. Root reports zero skipped cases and no build warnings on the successful rerun. No independent build/test/CLI process was launched by this child.

Normal verification is separate from hostile acceptance. Full historical profiles, the original test_build tampered-file variant, SDK annotation ZIP/eligibility attachment, effective inventory/view policy and whole-producer collection reduction remain pending. No requirement, TODO or migration completion is claimed.

See SOURCE-INTEGRATION-STALE-BINARY.md for the failed-run database evidence. Both receipts and both versions of the source/binaries remain retained. MCP persistence remains denied and was not retried.
