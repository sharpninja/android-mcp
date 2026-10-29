# Actual Sources/Snapshot CLI: private normal proof and guarded proposal

The immutable proposal is `handoffs/20261029T0142474735288/manifest.json`, SHA `b84293d06aa8b297f624f0042809e0bd92cca5a26ba0b91a5efb3f82fab821b6`:13files87496bytes. Its ten-file CLI segment is `cli-manifest.json`, SHA `c67691030349b35e845779a88631c8410c0d0ef208f2ed1d6fc33750c7ccf641`; the separately approved three-file help segment is `help-manifest.json`, SHA `a00ca0848c6ba174c02aa1e51467b5e02edc81ef9718b893d32c1284ddbdbb9f`. Every sealed source matches its passed-gate pin; exact prior sources/hash/size or absent-target guards are recorded. Originals remain retained.

The CLI diff is `cli.patch.proposed`, SHA `c99d1c4fd9d16e153270af7a01877c13b18cbfba9089e699639dfe5c7ac44e07`. The help diff is `help.patch.proposed`, SHA `fedb2f6c3e173a932567e47466809496a196e9f875ced731f653e80e6f4672db`. These were generated with read-only no-index diff, no repository index/history writes. Root MUST merge shared Build/OperationRegistry/OperationContracts changes onto its latest Validate/Design routes, rather than replacing entire files from the private checkpoint015-based copy. Append enum values after Root's current values. The help segment depends on the same declared-group property introduced by the CLI segment.

## Exact proof

`../20261029T0130289466198-SnapshotCliPlannerHelpMocks.json`, SHA `77fd29b9df58a76202290a1484b909e685f5bc59e97393836d3f8e08b0fad5f9`, records100passing mocks/negative controls before planner/dispatch/context/help implementation:22planner cases*2,24CLI observation cases*2,4help cases*2. Only enum/metadata contracts and fixture reuse visibility were added before that gate. The first attempt stopped at fixture compilation (missing brace, CS1513); it ran no tests. It is not positive gate evidence.

`../20261029T0139361062878-SnapshotActualCliAndRegressions.json`, SHA `094263f1113f05128f07c0a117ef1bd020e01f3e58e6f648589ea9f0ca336a06`, records467passed,0failed,0skipped: the same24 actual compiled child-CLI cases,22native planner/group cases,4native help cases,100mocks and317prior regressions. Existing Fetch and Seed actual CLI cases each passed; old help10native, prior Snapshot service and registry/planner regressions are included. The exact filter, individual outcomes, TRX pin, source pins and commands are in the receipt. A first native attempt stopped at app compilation because this NUKE has no ControlFlow.Fail (CS0117); the final code uses the existing exception/NukeExitCode normalization path after printing JSON. No assertions were weakened. Only NU1900 warnings about unavailable NuGet vulnerability data remained; audit was not disabled.

The native cases execute the current freshly built monolith with `dotnet exec`, separate runtime/input/output directories containing spaces, pinned tool identity and the existing NativeProcessRunner. They do not call a legacy launcher, test-only dispatch branch or child build. The explicit positive download is the already admitted immutable bundle mechanism used by existing Sources/Fetch CLI tests. No file-URI allowance, new transport, network listener, CA, hidden bypass or live download was introduced.

Fifty stdout/stderr captures for 24 cases and 25 child invocations (including one retained-output repeat) are copied/hash-verified under `actual-cli-captures/20261029T0142471523561/manifest.json`, SHA `7da9abcd84f5fa8cc8867fddffc867599e2c4e23ba1c6c060dc812658df3fe09`. Outcome authority remains the assertions and TRX, not inferred capture text. Twenty TRX files totaling 8,980,635 bytes are retained by `../retained-trx/20261029T0144199529748-manifest.json`, SHA `3b07cefa20442f4258ce3a40469e1c1644ed4ae292f95fe5780071c87842aed1`; all ignored originals remain.

## Minimal integration

One authoritative registry defines Sources/Snapshot. Required parameters: project-root,source-manifest,recipe-lock,output-root. Optional: generation,release,snapshot-mode,download,bundle-root,acquisition-profile and the existing pipeline-request/pipeline-request-sha256/pipeline-request-length trio. Limit,cache-policy and fetch-delay-seconds are unsupported. The trio's all-or-none semantics are declared once on OperationDefinition and enforced by the existing OperationPlanner, with empty default groups preserving prior definitions. Help derives all_or_none arrays and Supply together lines from the same metadata, omitting the field/text when empty.

Build.SourcesSnapshot is composition only. Build.SourcesFetch shares its context with Snapshot through one optional mode and exposes its already created raw store. Fetch/Seed keep their existing required bundle and pacing. Snapshot's absent bundle is an explicit read-only missing store, never an unrelated directory fallback. Snapshot Restore uses canonical transport and post-source delay0; Snapshot Fetch wraps the existing transport with its established retry and post-source delay1. Build.SourcesInputs is reused unchanged and excluded from this delta. The only existing SourceSnapshot test helper change makes its Recipe builder public so CLI fixtures reuse the same source rows instead of cloning them.

Restore defaults to dry-run and exit0. Fetch dry-run prints its result then exits1, including when nothing is missing. Exact Restore inputs verify; corrupt bytes remain unchanged on refusal. Fetch existing inputs preserve the original existence-only policy. First downloaded row per Git blob wins. Explicit bundle acquisition writes only admitted winning sources/dependencies plus lease/seal, then a repeat verifies existing bytes without changing outputs. Missing/corrupt bundles never trigger mutable fallback or a final seal.

The optional envelope is a typed JSON file with `Pins` (a list of existing StoredArtifact fields RelativePath,Sha256,Length,MediaType) and optional `ProofRoot`. It uses the existing pinned artifact reader, canonical JSON and typed JsonSerializer pattern. The caller supplies the envelope's trusted SHA/length through the trio; each proof pin must already be independently declared by the caller. Production never discovers proof hashes and promotes them to authority. The proof root defaults to raw/output; an explicit different root uses the existing root/path policy. Project/recipe root remains separate. Lazy loading means default dry-run, exact reuse and explicit Generic read neither the envelope nor receipt/XML. Actual CLI refusal cases prove envelope hash, receipt hash and default/explicit proof-root binding before acquisition effects.

## Usage after Root integration

Use the built application and a NUKE runtime root containing .nuke. The approved native host is used for both exec and dotnet-host. Input paths may be absolute or project-root-relative; the complete lock recipe name is preserved.

```powershell
$dotnet = '<approved native dotnet>'
$assembly = '<built AndroidMcp.Build.dll>'
$runtime = '<NUKE root containing .nuke>'
$project = '<locked recipe input root>'
$raw = '<separate retained raw output root>'
$arguments = @('exec', $assembly, '--root', $runtime, '--target', 'Sources',
    '--operation', 'Snapshot', '--dotnet-host', $dotnet,
    '--project-root', $project, '--source-manifest', '<complete recipe relative path>',
    '--recipe-lock', '<complete lock relative path>', '--output-root', $raw)
& $dotnet @arguments
& $dotnet @arguments --snapshot-mode Fetch
& $dotnet @arguments --download --bundle-root '<explicit immutable bundle>'
```

For canonical external metadata admission, append all three pipeline-request options, naming the project-relative typed envelope and its caller-declared trusted SHA/byte length. Omit ProofRoot in the envelope to use raw/output; declare a different owner proof root explicitly when needed. The generated operation help describes the parameter group from the registry. The envelope is not a new rehydration parser or command.

## Remaining boundaries

This proves the admitted CLI/domain and bundle-based positive download. Ordinary Gitiles positive HTTP and successful live external metadata acquisition were not exercised through this CLI; existing shared codec/transport/service fixture gates provide their bounded implementation evidence. No live source availability or corpus reconstruction is claimed. The prior metadata handoff proves the successful shared range composition using scripted transport.

`PLATFORM-EVOLUTION-PROPOSAL.md` separately records the exact original truth/str behavior and suggests a minimal detached typed nonstring transport platform fact for the sole helper. No PlanCore/OriginalRow parser or new formatter was implemented here. Broader legacy nonstring platform values, existing stricter native admission/preflight policies and original unpinned legacy command inputs remain explicit before whole original restore/fetch producer closure. Root alone reconciles current/frozen original functions, helper/documented caller dispatch, canonical integration, full normal verification and readiness. No producer was removed and no second global inventory was created.

No canonical edits, Git writes, GitHub Actions, HV, Python, custom Node/Bash, raw markers/credentials, paid services or live source downloads occurred. MCP session logging remains incomplete under the denied-route restriction; no session IDs were invented.
