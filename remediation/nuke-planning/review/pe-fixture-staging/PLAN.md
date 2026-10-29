# Stable shared PE fixture

The full checkpoint-016 suite exposed `SDK_CHILD_CODE_PIN_INVALID` in the nonce interface test. Its two independently built `NativeFactsFixture.Pe` images use the same module ID but omit `deterministicIdProvider`. The .NET 10 PEBuilder default is a time-based provider, whose stamp is written to the COFF header. Production pin refusal is correct.

The correction is confined to the shared test helper: supply a deterministic content ID using its already explicit module ID and stamp zero. No production reader, hash, admission or identity check changes. Retain the failed full gate and original helper bytes. Existing fixture-dependent assertions remain unchanged.

Before changing the helper, prove the independent contract with correct and individually incorrect hash, module, timestamp and length observations. Then bind the same assertions to two actual fixture images, inspect both PE headers/module IDs and shared content hashes, and rerun the affected SDK child/native-facts tests. A fresh unfiltered full gate is required before checkpoint publication. This is normal verification, not HV or whole migration completion.

Source: https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Reflection.Metadata/src/System/Reflection/PortableExecutable/PEBuilder.cs (constructor defaults to GetTimeBasedProvider; Serialize patches contentId.Stamp into COFF header).
