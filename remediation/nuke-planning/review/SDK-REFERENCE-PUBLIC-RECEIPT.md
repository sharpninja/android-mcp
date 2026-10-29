# Normal SDK reference-query receipt

Observed terminal: 2026-10-28T08:42:09 local, exit 0. Normal native NUKE `Validate --operation Toolchain --toolchain-check SdkReferenceCopy` returned `Code: OK`, `Copies: 1`. This was a local SDK operation, with no reviewer or provider call.

- Installed SDK: 10.0.403. Selected dotnet SHA256: `634efbf2414144d140d6f59d167342c8b3b53b4c34346ed786dd0470ddc9a3cb`.
- Request: 2,171 bytes, SHA256 `fc14376c7cbeaa13d680d1ce3bb1c098f78af90dcfd779b5da73814cea155c75`.
- Exact terminal receipt: `artifacts/tests/sdk-reference-proof-20261028T084144/operation-terminal.json`, SHA256 `4514903abe51fe2e956e35c354979d19c3034a90aeaf2c20748c891795c8846d`.
- Native reference descriptor: 805 bytes, SHA256 `8343f5d0a99fb94dc3bd027a5300ff11a66f4a853ed7877222d13ca2ee127c06`; binds the Debug/net10.0 host project output to its test-project reference-copy destination.
- Full private native stdout retained: 281,953 bytes, SHA256 `548182dc15f37caaed1d7bf960e842eaa5893ad2fd2b02dfaa157251665d71c5`. Native and outer stderr were empty. The complete raw captures remain local.

Scope: one actual native resolved-reference copy metadata query. This does not prove source-input evaluation, executing test-host origin, full build freshness, or HV acceptance. All earlier failed attempts remain retained. The exclusive .NET window was released after terminal observation; no duplicate query was launched.

Receipt accuracy: 100% for the listed observed hashes, lengths and terminal fields. Query-operation completeness: 100%; overall migration completeness is pending the explicit remaining boundaries above. No hostile verdict is claimed.
