# NUKE migration checkpoint

This is an **incomplete migration checkpoint**, not a working replacement for the
whole legacy pipeline. One production C# NUKE application hosts the tools; the
`Tests` project is a test class library, not another generation CLI.

The implemented registry dispatches source planning/acquisition/extraction,
catalog building/validation, design generation and selected Gradle delegates,
publication checking and normal test gates. The authoritative operation and
parameter definitions are in [OperationRegistry.cs](Core/OperationRegistry.cs);
dispatch alone does not establish complete legacy parity. `Review/Prepare`,
complete package callers, release-manifest compatibility and remaining producer
behavior still need integration. Do not execute legacy Python or custom Node
programs.

## Bootstrap and current gate

Use the exact SDK in [global.json](global.json). NUKE and test dependency pins are
in the project files and `packages.lock.json`; package sources are restricted by
[NuGet.Config](NuGet.Config). No credential or machine-specific host path is
committed. Select an already-installed native .NET host, not a provisioning shim.

From `remediation/nuke-app`, in PowerShell:

```powershell
$dotnetHost = '/absolute/path/to/approved/native/dotnet'
& $dotnetHost restore Tests/AndroidMcp.Build.Tests.csproj --configfile NuGet.Config
& $dotnetHost run --no-restore -- --target Validate --root (Get-Location).Path --operation MockGate --dotnet-host $dotnetHost
```

Use `--operation Tests` for the full current normal suite. `MockGate` selects
assertion/fake-provider checks and cannot establish production parity. Exact
checkpoint counts, real CLI evidence and limitations are in the concise
[checkpoint gate receipt](../nuke-planning/review/checkpoint008-normal-gate.json).
Full TRX and process logs stay in ignored local test artifacts. The actual SDK
producer lifecycle also has a separate [bounded native proof](../nuke-planning/review/native-sdk-lifecycle-proof-008.json).

## Final command shape

The authoritative typed registry exposes `Sources`, `Catalog`, `Design`,
`Validate`, `Review`, and `Publish`, with a singular `--operation` parameter.
It owns legal combinations, parameter validation, dispatch and Gradle
compatibility data. Shared help generation consumes those same definitions.
There is no target per old script. Pending operations fail rather than selecting
an alternative implementation.

Gradle remains a thin facade for required architecture/document targets. Kotlin
remains the Android server/UI language. Source pins, schemas and UI blueprint
remain canonical; generated views do not become competing authorities.

## Source extraction tasks

`Sources/Extract` admits one content-pinned workflow request. The accepted command
parameters come from [OperationRegistry.cs](Core/OperationRegistry.cs); parameter
help, including the task choices and default, comes from
[Build.cs](Build.cs). Run the current application with `--help` to view that help.

Select `--extract-task InterfaceModules` to extract literal interface-module facts
from an admitted inventory and declared source list. The
[typed task request](Modules/Sources/InterfaceModuleWorkflowContracts.cs) owns its
fields. Omission selects the existing declaration workflow, whose request is
[defined here](Modules/Execution/SourcesExtractWorkflowContracts.cs).
`InterfaceHashes` uses its [own typed request](Modules/Sources/PublishedInterfaceHashWorkflowContracts.cs).
The shared [StoredArtifact definition](Core/ExecutionContracts.cs) owns the inner
input pins; there is no second request schema in this README.

```powershell
& $dotnetHost run --no-restore -- --target Sources --operation Extract --dotnet-host $dotnetHost --project-root $projectRoot --pipeline-request $requestRelativePath --pipeline-request-sha256 $trustedRequestSha256 --pipeline-request-length $trustedRequestBytes --extract-task InterfaceModules
```

Choose an admitted native host and an absolute project root. The request path is
relative to that root; obtain its exact SHA256 and byte length from the trusted
request-producing receipt. Those outer pins are checked before interpreting the
selected typed request. For `InterfaceModules`, the inventory and declared-source
inputs carry their own pins. Selected source bytes are checked against their
declared digests before extraction.

Output locations belong to the typed request. For `InterfaceModules` and
`InterfaceHashes`, `GenerationStoreRoot` identifies a separate output store from
the retained project inputs; `GenerationRoot` selects a fresh relative directory
inside that store. Preserve existing generations, including partial failed
attempts, and select a new generation directory for a retry. The module workflow
creates its directory before parsing selected build files, so that directory can
remain after a later error. A stored `index.json` is the commit receipt; output
file or directory existence alone does not establish success.

Interface-module output is literal source evidence. Build files are not executed,
unresolved selections remain explicit, and declared module versions do not prove
installed Android/APEX versions or runtime access. The report retains these limits
and does not claim complete coverage.
## Permission metadata export

The read-only exporter uses the selected sealed catalog directory. The output
must be a separate empty directory outside that entire release tree. It emits
`permissions.jsonl`, `permission-expression-samples.json`, and
`permission-coverage.json`. Unknown OS authority and uninferred MCP grants remain
explicit; the coverage document reports `complete: false`.

```powershell
& $dotnetHost run --no-restore -- --target Catalog --operation Export --dotnet-host $dotnetHost --project-root /absolute/catalog-contract-root --source-catalog /absolute/sealed-release --output-root /absolute/new-permission-export
```

The original metadata queries, bounds, nulls and independent expected facts have
native fixture and actual CLI proof. C# JSON output has its own producer
identity; this is not a historical byte-identity promise. The chronological
[export proof](../nuke-planning/catalog/permission-export-native-proof.json)
distinguishes mock, native, CLI and full-suite scope.

## Offline release parts

Both operations belong to `Publish`; they never upload, commit or push. All
artifact paths and generation paths are relative to one explicit `--project-root`.
Existing generations and final files are rejected, including identical retries.
Use a fresh generation for each attempt and preserve any partial output.

```powershell
& $dotnetHost run --no-restore -- --target Publish --operation Split --dotnet-host $dotnetHost --project-root /absolute/release-workspace --source-artifact sealed.bin --artifact-sha256 <trusted-whole-sha256> --generation parts --part-bytes 1073741824
& $dotnetHost run --no-restore -- --target Publish --operation Reassemble --dotnet-host $dotnetHost --project-root /absolute/release-workspace --parts-manifest parts/manifest.json --manifest-sha256 <trusted-manifest-sha256> --generation assembly --destination-artifact restored.bin
```

Obtain each trusted pin from the producing receipt or an independently verified
source. Split reports the manifest pin. Reassembly reports the artifact and a
success marker written last; file existence alone is not a successful commit.
The shared streaming store retains verified and uncommitted copies rather than
moving or deleting originals, so allow disk space for the input, parts,
retained copies and final output. These commands use the native Linux reader;
the concrete Windows kernel adapter is not implemented. The original eight
codec assertions, four retention/cancellation cases and actual CLI roundtrip
are verified separately from remaining malformed-manifest compatibility and
whole-producer closure. No Python codec is executed.

## Remaining migration work

- Close every legacy behavior and caller against the existing shared services;
  normal fixture proof does not establish an entire producer or corpus.
- Finish complete documentation package composition and validation-receipt
  callers, remaining release-manifest producer compatibility and complete
  original-method closures. Native Linux file-reader and codec callers now have
  explicit integration proof; Windows runtime support is not inferred.
- Complete the head-bound review preparation and official audit runtime
  composition without invoking an HV before the PR is ready.
- Reconcile all documented rehydration, validation, publication and review
  entrypoints; retain originals locally and publish no caches or corpora.
- Supply fresh full unit/integration TRX and review the completed migration and
  design-contract impacts in the PR. No Google API completeness scoring.

The migration belongs on `feat/nuke-tooling-migration`, with a draft PR to main.
Mark it ready only after migration completion and normal verification; review
and remediation then occur in the PR. Do not run an HV before readiness, merge
the PR, add GitHub Actions, or push migration changes directly to main.
