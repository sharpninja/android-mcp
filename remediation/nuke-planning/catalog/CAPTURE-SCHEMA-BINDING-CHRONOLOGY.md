# Effective capture schema binding chronology

2026-10-28T07:12:02. Normal verification only. No migration completion or HV acceptance is claimed.

The preserved run `remediation/nuke-app/artifacts/tests/20261028T0705550670626-6757e49c183348f1b4a12ede34788204/tests.trx`, SHA256 `71c8cad41981a50e75cbb8f2133f4b0a806d83d71d8a5bb4f9901d2b4f94eaef`, contains 2,014 executed results: 2,009 passed, five failed, zero other/unexecuted outcomes. All five failures are the original effective-overlay assertion families:

- EFFECTIVE-01 suppression is separate from version existence.
- EFFECTIVE-02 local addition remains version-unknown.
- EFFECTIVE-03 structured field override changes transitive schema.
- EFFECTIVE-04 current document pin is separate from schema.
- EFFECTIVE-05 suppressed dependency type denies known admission.

Each threw `InvalidDataException: Invalid InventoryEntity contract` at `CatalogInventory.Capture` after attaching catalog_state, catalog_revision and catalog_document_sha256. The seven checked-loader/transaction guards passed. The failed TRX and the exact wrong-binding fixture are retained; the whole gate is not represented as passing.

The exact fixture error was an injected resolver constructed with `contracts/DeviceResolution.schema.json`. That historical Inventory2.1 definition has additionalProperties=false and does not declare the three effective catalog fields. It is appropriate for immutable producer inventory and remains unchanged in CatalogBuild's existing export validation.

The preserved original `tooling/device_resolution.py` validator explicitly loads `contracts/DeviceView.schema.json`. The existing native Resolver27 and DeviceViewStorage31 bindings use that same active DeviceView3 authority. Its InventoryEntity definition explicitly declares all three fields, and its Snapshot/ResolvedEntry shape also contains the selected contract and effective catalog pins. The canonical file is SHA256 `706d34871610f7e97ed0a00edd63bfb8961d39747a705835ed0b942ff27f807f`.

The parent accepted a fixture-only correction. `Tests/Catalog/CatalogEffectiveInventoryRealTests.cs` changed from SHA256 `c74791f832f1a92e6bc8a4d371ef7a3a9b014900b254d146f4d3140c33b8d3e7` to `eff67066376802599a737a7bf160f44d2477c76d9867bbfd0845087ac286e4ce`. It now selects the active DeviceView schema and asserts the canonical pin. No schema, Capture implementation, emitted field, expected case, source fixture or policy assertion changed. The same twelve native cases and twenty-four mock rows remain required.

`Apply-EffectiveSchemaBindingFix.ps1` passed PowerShell parser and PSScriptAnalyzer Warning/Error checks, was run with WhatIf, retained/hash-verified the prior fixture at `evidence/CatalogEffectiveInventoryRealTests.before-schema-binding-c74791f832f1a92e6bc8a4d371ef7a3a9b014900b254d146f4d3140c33b8d3e7.cs.txt`, and verified staged source/postcopy bytes. The copy was acknowledged at 2026-10-28T07:11:18. The parent owns the rerun; no independent .NET execution occurred here. A passing rerun receipt is still pending at this document's creation.

MCP logging remains blocked by the earlier denied operation and was not retried. Local chronology is not a persisted MCP turn or a requirement/TODO status update.

2026-10-28T07:29:24. The corrected rerun is now directly verified: `remediation/nuke-app/artifacts/tests/20261028T0720298912423-105ed2516dc4421593dbc94c20c481e5/tests.trx`, SHA256 `f17dbabde923745f5ba57f37c126148dd4ff3eca19a3f913f26da8c19576ff95`, has 1,799 passed, zero failed and zero skipped results. Its exact effective-inventory classes contain the same twelve native cases and twenty-four mock rows, all passed. `evidence/capture-normal-method-closure.json` retains the five complete original method bodies and the production/test/schema pins. This closes those five normal assertion scopes only. The earlier five failures remain part of the chronology; whole migration and HV acceptance remain pending.
