# SourceIntegration gate used the earlier app binary

The root's 03:46 gate had 16 Passed and 2 Failed out of 18 executed, with zero skipped. The failed assertions were the Java/current-JVM permission/link fields and the malformed-AIDL aidl_schemas row. No assertion was weakened and no production correction was applied in response.

Read-only inspection established the execution mismatch:

- The new consumer `Modules/Catalog/CatalogBuild.cs` was copied and hash-verified at 03:45:34, SHA256 `d02808237f730793767dc725ae96972cb3f378a95cdd5832456dc6f1b2ba4e28`.
- Both app and test-bin `AndroidMcp.Build.dll` files were last written at 03:39:12 and hashed `7b89b7acc14594ae7042d46516954f3fcfeed4fdbcbfb749cfb72a4a8aa77a2a`.
- Both failing fixture databases record that same old binary hash in catalog_meta.native_producer_sha256. Both have zero aidl_schemas rows.
- The Java fixture has CAMERA permission on the source-spelled Java Entry, unknown permission on the JVM Entry and no proven_current_sdk_jvm_identity. These are the earlier consumer's exact missing behaviors.
- `Build.RunTests` builds the test project with BuildProjectReferences=false. It therefore cannot supply the omitted app rebuild when launched from an earlier monolith.

Retained gate root:

`remediation/nuke-app/artifacts/gate-temporaries/20261028T0346030681719-c83ab7ddfb3549c9be6c5fd4303c35a3/`

The Java fixture is `native-catalog-build-6e694435c345498ab63870fc0653c7d8`; malformed AIDL is `native-catalog-build-df475e663ca14f8b9855500cc3ccad65`. Each database remains at `out/catalog.sqlite`.

Diagnosis used `Get-FileHash`, file timestamps and native `sqlite3 -readonly -json` queries only. No independent .NET build/test/process, source change or output mutation was performed. Root was informed that the monolith must be recompiled through the normal NUKE verification path before rerunning the same eighteen cases. Until that rerun, the new four consumer bindings remain unverified; the old seven CLI fixture proof is not a substitute.

Follow-up: root rebuilt the monolith and reran the unchanged eighteen cases. All eighteen Passed; SOURCE-INTEGRATION-REAL-GATE.md records both TRX hashes and the new assembly hash. This stale-binary run remains retained as failed evidence rather than being overwritten or counted as production success.
