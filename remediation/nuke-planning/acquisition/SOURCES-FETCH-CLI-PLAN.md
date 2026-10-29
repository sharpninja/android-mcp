# Sources Fetch native CLI composition

Root's facade16mock and operation-route8mock contracts passed in549/549 zero-failure/skip/warning gate `20261028T0047366853112-ac0d0a73a57a4c16af68b0b7d4c04ad5/tests.trx` before implementation. The facade calls the sole Publication planner exactly once, rejects invalid/null projection and cancellation before batch, forwards original typed projection/generation/receipt/cache policy/token, and returns the same result.

The CLI partial reads recipe and lock using the shared input FileArtifactStore. Both Artifact names are normalized relative to ProjectRoot, retaining `rehydration/fixture.json` instead of basename. BundleRoot is explicit and separate; output must differ from input and bundle. Root owns Build partial declaration, CLI properties, parameter forwarding and registry dispatch. No second recipe parser, legacy producer, downloaded source or wrapper is executed.

Before live invocation, the added SourcesFetchCliMockTests prove the E2E assertions against a correct scripted observation and individually wrong exits, bytes, pins, receipt fields, repeated output hashes, root isolation, bundle/input retention and invocation arguments. Same real provider will bind those assertions only after this mock gate passes.

The miniature E2E reuses RehydrationContractCases.Request and changes both abc/def rows to immutable required_local_snapshot bundles with explicit fixture revisions. URLs remain example.invalid. Inputs reside in a directory with spaces; both CLI input filenames are absolute while the committed lock names the nested relative recipe path. A successful first run verifies both pinned raw files and snapshot copies, exact recipe SHA, all2row/physical denominators, canonical profile, raw completeness and no historical byte identity. A second run verifies the sealed receipt and every output file are byte-identical.

The real probe uses the CURRENT compiled app assembly through native dotnet exec and existing NativeProcessRunner, with test-owned --root and .nuke directory. It never invokes build/run or invokes a child test build. Process captures, runtime root, input root, bundle root and output root all belong to the fixture. Parent .nuke/logs are not selected as the child runtime root. No canonical upstream retrieval, software installation, provider action, Python, Node or Bash is authorized by this test.

Mock gate, facade real gate, CLI E2E execution and parent final full regression/hostile acceptance remain distinct receipts. No completion claim is based on staging code or mock-only success.

## Verified native execution

The facade5real, planner4real and CLI2mock tests passed in389/389 zero-failure/skip/warning gate `20261028T0102474714065-bc5d9ccd943b4575b540464757ba5474/tests.trx`. The SAME CLI case then passed in162/162 zero-failure/skip/warning gate `20261028T0107211682794-34e445229b164538ab98bd496eede090/tests.trx`, SHA256 `8524a8dc1a3721d6d5863dd7eadf8e43d8603e761fd8a66facca70b8fee54339`. One native test executed two actual child Sources/Fetch invocations; the second invocation was not a second fixture or regeneration.

Retained stdout reports first `Reused:false` at local01:07:33, then `Reused:true` at01:07:35. Both identify generation `cli-bundle-generation` and `sources/acquisition.json`, length4149, SHA256 `138faa5148679c86e912fc912f1deb028bf060bc71283456e23b83ef00c23fdb`. Each reports2total/selected/processed rows,2total/selected physical sources, raw `Complete:true`, and `HistoricalByteIdentityClaimed:false`. Both stderr captures are empty. SAME assertions verified all6output paths and hashes unchanged, plus both input files and both bundle originals retained. The6output paths include the two raw blobs, two copied snapshots, immutable receipt and zero-byte lease file.

Byte-exact captures, locked recipe/lock, input bundle, generated outputs and isolated `.nuke` logs are copied beneath `sources-fetch-cli-live-evidence/`; originals remain in `/tmp/android-mcp-sources-fetch/dc03579afdb2425c9e8aeb6c7cd9578f/fixture with spaces`. Hashes and direct JSON summaries are recorded in `sources-fetch-cli-live-evidence.json`. Root's original24mock gate, the389case native/mock gate and162case actual CLI gate are pinned in `sources-fetch-gate-evidence.json`. Runtime argv is supported by the native probe source and SAME argument assertions; no independent OS argv capture is claimed.

## Usage boundary

For an already compiled application with its runtimeconfig/deps files beside it, the PowerShell invocation has this shape. Paths are caller-supplied; the runtime root must already be an admitted NUKE control root. The test used its own fresh `.nuke` directory and explicit test runtimeconfig/deps with the current compiled app assembly.

```powershell
& $nativeDotNet exec $compiledApp --root $runtimeRoot --target Sources --operation Fetch --dotnet-host $nativeDotNet --project-root $inputRoot --source-manifest rehydration/fixture.json --recipe-lock rehydration/fixture.lock.json --bundle-root $bundleRoot --output-root $outputRoot --generation source-acquisition --release fixture-release
```

The manifest and lock names resolve beneath ProjectRoot and retain their complete relative paths; the lock's `recipe_file` is `rehydration/fixture.json`, not `fixture.json`. BundleRoot is explicit. OutputRoot differs from input and bundle. Optional `--limit`, `--acquisition-profile` and `--cache-policy` use the existing typed planner/batch policies; defaults are all selected source paths, CanonicalRestore and PreserveAliases. Generation defaults to `source-acquisition`; receipt path is `sources/acquisition.json`.

Current CLI resume is enabled by default. Explicit NoResume caller semantics remain root's tracked later increment. This command acquires raw sources and optionally imports locked historical caches; it does not execute legacy producers, build the catalog database, validate/export the full catalog, recover unavailable immutable snapshots from mutable URLs, or claim historical gzip byte identity. Root retains full regression and hostile acceptance.
