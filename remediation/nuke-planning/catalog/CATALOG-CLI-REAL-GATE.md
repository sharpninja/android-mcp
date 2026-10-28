# Actual registered Catalog CLI verification

Direct XML inspection confirms seven Passed and zero other outcomes for `CatalogBuildCliRealTests.CurrentRegisteredCatalogCliUsesSameBuildCasesAndAssertions` in:

`remediation/nuke-app/artifacts/tests/20261028T0302529522864-c3e395fb41ad45b0816e8f453e075b40/tests.trx`

SHA256: `68bc01c0479541d379d116750e26afee8fb589f7988f27c00e7efc4c88d84424`.

The complete supplied root gate has 3379 Passed, zero failures/skips and no build warnings. The same TRX has original direct Build7, cache2 and streamed build4 native bindings Passed. Earlier CLI14 mocks passed before this native binding.

The native probe invokes the current compiled monolith through the shared NativeProcessRunner, with the native dotnet host content pin, current DLL pin, current test runtimeconfig/depsfile, an isolated `.nuke` runtime and the registered Catalog/Build operation. It uses the existing three-source fixture and independent DB/export validator through an execution delegate. It does not invoke build/run/restore, a shell or an old producer. Inputs, stdout and stderr captures are retained. The probe checks the registered outcome, process exit, argument boundaries, captures and the same seven bounded build assertions.

The seven capture pairs remain under the test-owned root:

`remediation/nuke-app/artifacts/gate-temporaries/20261028T0302397020711-32dea0cb314c4061a173525f1c70551c/native-catalog-build-*/cli process receipts/`

Each matching directory contains `stdout.bin` and `stderr.bin`. This path was discovered by reading retained files after the gate; no extra CLI process or test was launched by this child.

This is normal bounded functional verification, not hostile acceptance or a full producer-parity/scalability claim. Java manifest admission, AIDL schema concern import, Java/current-JVM permission links, annotation ZIP attachment, effective inventory/view policy and whole-producer collection reduction remain separately allocated. The current SHA failure fixture changes the declared hash; the original test_build tampered-file setup remains an explicit additional parity case rather than silently relabeled proof.

No tracked requirement is marked complete. HV remains deferred until the completed migration and PR readiness. MCP audit persistence remains blocked by the recorded rejection and was not retried.
