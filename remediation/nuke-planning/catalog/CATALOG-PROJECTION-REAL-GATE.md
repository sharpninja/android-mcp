# Bounded native lifecycle and projection proof

Direct read-only XML inspection confirms:

- `CatalogLifecycleRealTests`: 13 Passed, 0 other outcomes. TRX `remediation/nuke-app/artifacts/tests/20261028T0119256618665-d75b83a13fe74f08bd3e031ccc404cb1/tests.trx`, SHA256 `2e7701c40facfdc1adb27d73c6051d4fad50b554223378fed0c4dede6b9e0890`.
- `ScopedProjectionRealTests.NativeProjectionUsesSameCasesAndAssertions`: 7 Passed, 0 other outcomes. TRX `remediation/nuke-app/artifacts/tests/20261028T0148326444997-37802e8bad8548208f6c931342604e2d/tests.trx`, SHA256 `947c8567bde641c3cf0300edc9052741a383bad5f0571db2879643143ed340b5`.
- The latter TRX also has 27 Passed SourceTypeFacts native rows: 13 XML identity, 7 formal names, 7 structural header. The Sources worker owns that producer and its original lexical regressions.
- The latter combined gate is **not all passing**: root reports 343 Passed and 9 browser failures out of 352 executed, with 0 skipped. The seven projection cases establish only the bounded original source-scope behavior. They do not establish a green full migration gate.

The projection implementation consumes the shared Sources formal/header producer, CatalogContracts, CatalogLifecycle, canonical serializer and strict JSON scalar comparer. It does not duplicate source parsing. The exact same seven fixture cases and assertions used by the preceding fourteen mock rows are bound to the native implementation.

Copied bytes, with both targets previously absent and originals retained:

- `Modules/Catalog/CatalogProjection.cs`: `5945130e2da66bf29081382a02be661fdf757c29f10a539a3c7445eab1e9b2ae`.
- `Tests/Catalog/ScopedProjectionRealTests.cs`: `062de3eae4f8e91c942ff144c320243d86738704162df89f3d432ffccb840903`.

Copy script analysis: 0 parser errors and 0 PSScriptAnalyzer Warning/Error findings. An initial attempted analyzer invocation used an unsupported parameter and performed no analysis; it was corrected to `-Path`, then the zero-finding analysis and WhatIf were rerun before the actual copy. No independent build/test invocation was made by this child.
