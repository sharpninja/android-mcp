# Sources Plan CLI increment

The route requires `project-root`, `source-manifest`, `recipe-lock`, and `output-root`. Optional parameters are `release`, `limit`, and `acquisition-profile`. The private partial signature is `Task PlanSources()`. Root owns the operation registry, main Build properties and dispatch. No tool identity, bundle root, cache policy or generation parameter is needed for pure planning.

The thin `SourcesPlanOperation` accepts the existing `RehydrationPlanRequest`. It calls the sole `IRehydrationPlanner.Plan` once, rejects invalid/null projection and cancellation, and refuses a plan that claims network activity, completion or historical byte identity. It returns the original typed plan and writes its exact original `rehydration-plan.json` Artifact once. No second recipe parser or metadata reinterpretation is introduced.

Fetch and Plan share `Build.SourcesInputs.cs:ReadSourcesRecipe` and the existing `InputRelative`/FileArtifactStore admission. Nested root-relative recipe names remain intact for `lock.recipe_file`. Plan requires a separate new or empty output root; existing files are retained and rejected before planning output writes. Stdout includes the typed full projection, snapshots, archive requirements and caveats, exposing missing canonical ETag/platform/XML/size proof, missing immutable snapshot bundle path and unknown historical cache count. The pure manifest records no network, no source completion and no historical byte identity.

BDPv4 chronology: SourcesPlanMockTests has9correct cases plus9per-property/interaction rejection groups. Root's route adds8mock cases. All26scoped cases passed in `20261028T0154077236557-3233d7b90fdd48bb94b9a0736fb7db07/tests.trx`, SHA256 `a1fc8f6e7d27b8563d0a6c2e5c80e5f0ff9ac71950b4ed00bee1525efd85cbdd`, before the production unit was written. That mixed88case run had79PASS/9BrowserFAIL/0skip; root owns those foreign failures and authorized this scoped26/26 increment. It is not a green whole-project receipt.

The exact5file production unit was copied at local02:01:32: native facade, SAME9real bindings, Plan partial, shared input reader and replacement of the existing Fetch partial with the reader extraction. No duplicate Fetch partial was added. Native real results remain a separate gate.

SourcesPlanCliMockTests has2tests: a pure-plan/fresh-output positive case and a complete per-field/proof/argv rejection group. It reuses the existing5logical/4physical projection fixture with canonical archive proof absent, immutable snapshot bundle path null and historical cache count absent. It checks pure manifest flags, typed prerequisite codes, no invented cache pin, unchanged input bytes, only one output artifact, correct nested input names and isolated dotnet-exec invocation. A second invocation must reject the nonempty output while preserving the original manifest.

The actual CLI adapter is written only after this mock gate passes. It will use the current compiled monolith through NativeProcessRunner and native dotnet exec, with a test-owned runtime `.nuke` root, no child build, and no supplied source/bundle bytes. Successful planning proves that missing acquisition prerequisites are reported rather than fetched or invented. The expected rejection of the second invocation proves fresh output admission.

This increment does not seed, acquire, extract, build, validate or export the source corpus. Actual remaining acquisition caller responsibilities are mapped in `ACQUISITION-LEGACY-REMAINDER.md`; no speculative tests or new NoResume flag are added for that mapping. Final full regression and hostile acceptance remain root-owned.

## Actual CLI proof and usage

The245/245 passing regression at `20261028T0211023187862-404015623d6f434b946f4388f0f8ca8f/tests.trx` verified all9Plan native cases, CLI2mock assertion proof and Fetch's actual bundled/resume case after helper extraction. Its SHA256 is `47f6499a92d3e6155e9be9b3c80c6cee079b72847f3501d7cca3bbd502c38b24`.

The SAME native Plan CLI case passed in `20261028T0219538536256-d5f4f35381e64599b2d84f44cd52268f/tests.trx`. That mixed run had323PASS/1Browser QA-overflow FAIL/0skip; it is not a whole-project green receipt. The CLI produced one1762byte `rehydration-plan.json`, SHA256 `a96e149194e1da53a53a2ac2b061580f615bae17039504ed38b1e73ca2f86140`. Actual stdout reported5logical rows,4unique/selected physical paths, and Network/Complete/HistoricalByteIdentityClaimed all false. It retained typed missing canonical ETag/platform/XML/size codes1-4, immutable snapshot code0 with null bundle path and historical cache count code7 with null cache pin. No source or bundle bytes were supplied to the fixture.

The second identical CLI invocation was expected to fail because output was no longer empty. SAME native assertions verified the exact fresh-output error, unchanged plan bytes and retained recipe/lock. Actual captures, locked inputs, native manifest and isolated `.nuke` logs are copied beneath `sources-plan-cli-live-evidence/`, with identities and complete stdout metadata in `sources-plan-cli-live-evidence.json`. Originals remain under `/tmp/android-mcp-sources-plan/6317e8dacbef4c7992af27880b2e7d8a/fixture with spaces`.

Root then normalized xUnit2031 without changing data or assertion cardinality (`Assert.Single(sequence.Where(predicate))` to `Assert.Single(sequence,predicate)`). The same actual Plan CLI case passed again in the5/5 confirmation gate `20261028T0223152914317-0bf971c4fb434c9498b849143d54c365/tests.trx`, SHA256 `cb6f22287861e15e7c86d19c57eafd3059d21427f255a52fae34d2ae3ea18590`. Current native test source SHA256 is `93a8c975b0f1bd713e3e0a46c02dba572d3d2261723674d3aa27655fabd85ef4`. This confirms the10/10 scoped native cases; the broader Browser failure remains separate root-owned work.

With an already compiled application and adjacent runtimeconfig/deps files, the PowerShell operation has this shape. The test used the current compiled app through explicit test runtimeconfig/deps and a fixture-owned NUKE runtime root.

```powershell
& $nativeDotNet exec $compiledApp --root $runtimeRoot --target Sources --operation Plan --dotnet-host $nativeDotNet --project-root $inputRoot --source-manifest rehydration/fixture.json --recipe-lock rehydration/fixture.lock.json --output-root $freshOutputRoot --release fixture-release --limit 4 --acquisition-profile CanonicalRestore
```

The runtime root is execution control, distinct from inputs and plan output. Recipe and lock resolve beneath ProjectRoot with complete relative names. No bundle, compiler identity or producer execution is required. Missing prerequisites are reported as data; a successful plan is not a claim that acquisition can proceed. Existing nonempty plan output is preserved and rejected; select a fresh output root for another plan generation.
